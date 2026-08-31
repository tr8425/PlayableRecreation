using System;
using System.Collections.Generic;

namespace Chess.Core
{
    /// <summary>
    /// 알파베타 + 정지 탐색. 한 번에 다 하지 않고 **깊이 하나씩** 훑는다 -
    /// 창이 매 프레임 <c>Step</c> 을 한 번씩 부르면 뜸들이는 사이에 저절로 깊어진다.
    ///
    /// 중간에 끊겨도 직전 깊이에서 찾은 수가 남아 있으므로 언제 멈춰도 둘 수는 있다.
    /// </summary>
    public sealed class ChessSearch
    {
        private const int MaxPly = 40;

        /// <summary>깊이 하나를 훑는 데 허용하는 노드 수. 한 프레임이 멈춰 보이지 않을 만큼.</summary>
        private const long NodeCap = 220000;

        private readonly ChessBoard board;
        private readonly int maxDepth;
        private readonly double blunder;
        private readonly Random rng;

        private readonly ChessMove[][] stack = new ChessMove[MaxPly][];
        private readonly ChessMove[] killers = new ChessMove[MaxPly];

        private readonly List<ChessMove> rootMoves = new List<ChessMove>();
        private readonly List<int> rootScores = new List<int>();

        private long nodes;
        private bool exhausted;
        private bool chosen;
        private ChessMove choice;

        public int CompletedDepth { get; private set; }

        /// <summary>더 깊이 볼 것이 남았는가.</summary>
        public bool Done
        {
            get { return exhausted || CompletedDepth >= maxDepth; }
        }

        public long Nodes
        {
            get { return nodes; }
        }

        public ChessSearch(ChessBoard board, int maxDepth, double blunder, Random rng)
        {
            this.board = board;
            this.maxDepth = maxDepth < 1 ? 1 : maxDepth;
            this.blunder = blunder;
            this.rng = rng ?? new Random();

            for (int i = 0; i < MaxPly; i++) stack[i] = new ChessMove[ChessRules.MaxMoves];
        }

        /// <summary>
        /// 실제로 둘 수. 탐색이 끝난 뒤 한 번만 정해지고, 그 뒤로는 바뀌지 않는다.
        /// 아래 단계에서는 여기서 일부러 최선이 아닌 수를 고른다 - 기계처럼 두지 않게.
        /// </summary>
        public ChessMove Chosen
        {
            get
            {
                if (chosen) return choice;

                chosen = true;
                choice = Pick();
                return choice;
            }
        }

        private ChessMove Pick()
        {
            if (rootMoves.Count == 0) return default(ChessMove);
            if (rootMoves.Count == 1 || blunder <= 0.0 || rng.NextDouble() >= blunder) return rootMoves[0];

            // 초보는 아무 수나, 그 위는 차선수를 둔다.
            if (blunder >= 0.5) return rootMoves[rng.Next(rootMoves.Count)];

            return rootMoves[1];
        }

        /// <summary>다음 깊이를 한 번 훑는다.</summary>
        public void Step()
        {
            if (Done) return;

            int depth = CompletedDepth + 1;
            nodes = 0;

            if (!SearchRoot(depth))
            {
                // 노드 예산을 넘겼다. 직전 깊이의 결과를 그대로 쓴다.
                exhausted = true;
                return;
            }

            CompletedDepth = depth;

            // 외통이 보이면 더 깊이 볼 이유가 없다.
            if (rootScores.Count > 0 && Math.Abs(rootScores[0]) > ChessEval.MateScore - 100) exhausted = true;
        }

        private bool SearchRoot(int depth)
        {
            ChessMove[] moves = stack[0];
            int count = ChessRules.GenerateLegal(board, moves);

            if (count == 0)
            {
                rootMoves.Clear();
                rootScores.Clear();
                return true;
            }

            Order(moves, count, 0);

            List<ChessMove> found = new List<ChessMove>(count);
            List<int> scores = new List<int>(count);

            int alpha = -ChessEval.MateScore * 2;

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                board.Make(ref move);
                int score = -AlphaBeta(depth - 1, 1, -ChessEval.MateScore * 2, -alpha);
                board.Unmake(in move);

                if (nodes > NodeCap) return false;

                found.Add(move);
                scores.Add(score);

                if (score > alpha) alpha = score;
            }

            // 점수 내림차순. 차선수를 고를 수 있어야 하므로 목록째 정렬해 둔다.
            for (int i = 1; i < found.Count; i++)
            {
                ChessMove move = found[i];
                int score = scores[i];
                int j = i - 1;

                while (j >= 0 && scores[j] < score)
                {
                    found[j + 1] = found[j];
                    scores[j + 1] = scores[j];
                    j--;
                }

                found[j + 1] = move;
                scores[j + 1] = score;
            }

            rootMoves.Clear();
            rootScores.Clear();
            rootMoves.AddRange(found);
            rootScores.AddRange(scores);

            return true;
        }

        private int AlphaBeta(int depth, int ply, int alpha, int beta)
        {
            nodes++;
            if (nodes > NodeCap) return alpha;

            if (depth <= 0) return Quiesce(ply, alpha, beta);
            if (ply >= MaxPly - 2) return ChessEval.Evaluate(board);

            ChessMove[] moves = stack[ply];
            int count = ChessRules.GenerateLegal(board, moves);

            if (count == 0)
            {
                // 외통은 빠를수록 좋고 늦을수록 나쁘다. 스테일메이트는 무승부.
                return ChessRules.InCheck(board, board.SideToMove) ? -ChessEval.MateScore + ply : 0;
            }

            if (board.Halfmove >= 100) return 0;

            Order(moves, count, ply);

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                board.Make(ref move);
                int score = -AlphaBeta(depth - 1, ply + 1, -beta, -alpha);
                board.Unmake(in move);

                if (nodes > NodeCap) return alpha;
                if (score <= alpha) continue;

                alpha = score;

                if (score < beta) continue;

                // 컷오프를 만든 조용한 수는 형제 노드에서도 잘 듣는다.
                if (!move.IsCapture) killers[ply] = move;
                return beta;
            }

            return alpha;
        }

        /// <summary>
        /// 잡는 수만 계속 본다. 깊이 끝에서 잡기 한복판을 그대로 평가하면
        /// 퀸을 내주고 나서 "재료가 앞선다"고 착각하기 때문이다.
        /// </summary>
        private int Quiesce(int ply, int alpha, int beta)
        {
            nodes++;
            if (nodes > NodeCap) return alpha;

            int stand = ChessEval.Evaluate(board);
            if (stand >= beta) return beta;
            if (stand > alpha) alpha = stand;

            if (ply >= MaxPly - 2) return alpha;

            ChessMove[] moves = stack[ply];
            int count = ChessRules.GeneratePseudo(board, moves, true);

            Order(moves, count, -1);

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                ChessSide mover = board.SideToMove;

                board.Make(ref move);

                if (ChessRules.InCheck(board, mover))
                {
                    board.Unmake(in move);
                    continue;
                }

                int score = -Quiesce(ply + 1, -beta, -alpha);
                board.Unmake(in move);

                if (nodes > NodeCap) return alpha;
                if (score >= beta) return beta;
                if (score > alpha) alpha = score;
            }

            return alpha;
        }

        // ---------- 수 순서 ----------

        /// <summary>
        /// 좋아 보이는 수부터 본다. 알파베타는 순서가 전부다 -
        /// 잡는 수(싼 기물로 비싼 기물을), 그다음 킬러, 그다음 나머지.
        /// </summary>
        private void Order(ChessMove[] moves, int count, int ply)
        {
            int[] scores = new int[count];
            ChessMove killer = ply >= 0 ? killers[ply] : default(ChessMove);

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                int score = 0;

                if (move.To == board.EnPassant && move.IsEnPassant) score = 1000;

                sbyte victim = board.Squares[move.To];
                if (victim != Piece.None)
                {
                    sbyte attacker = board.Squares[move.From];
                    score = 10000 + ChessEval.Material[Piece.Kind(victim)] * 10
                            - ChessEval.Material[Piece.Kind(attacker)];
                }
                else if (ply >= 0 && move.From == killer.From && move.To == killer.To)
                {
                    score = 9000;
                }

                if (move.Promotion != 0) score += 8000 + ChessEval.Material[move.Promotion];

                scores[i] = score;
            }

            for (int i = 1; i < count; i++)
            {
                ChessMove move = moves[i];
                int score = scores[i];
                int j = i - 1;

                while (j >= 0 && scores[j] < score)
                {
                    moves[j + 1] = moves[j];
                    scores[j + 1] = scores[j];
                    j--;
                }

                moves[j + 1] = move;
                scores[j + 1] = score;
            }
        }
    }
}
