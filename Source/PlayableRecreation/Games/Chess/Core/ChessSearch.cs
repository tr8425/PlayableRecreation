using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chess.Core
{
    /// <summary>
    /// 알파베타 + 정지 탐색. 한 번에 다 하지 않고 조금씩 훑는다 -
    /// 창이 매 프레임 <c>Step</c> 을 한 번씩 부르면 뜸들이는 사이에 저절로 깊어진다.
    ///
    /// 나누는 단위는 깊이가 아니라 **뿌리의 수 하나**다. 깊이로 나누면 한 프레임이
    /// 통째로 그 깊이를 떠맡게 되는데, 복잡한 중반에서 깊이 5는 3분의 1초가 넘는다 -
    /// 그 한 프레임이 그대로 멈춤으로 보인다. 뿌리에서 나누면 프레임은 수 하나만 지고,
    /// 나머지는 다음 프레임이 이어받는다. 깊이는 그대로 다 판다.
    ///
    /// 중간에 끊겨도 직전 깊이에서 찾은 수가 남아 있으므로 언제 멈춰도 둘 수는 있다.
    /// </summary>
    public sealed class ChessSearch
    {
        private const int MaxPly = 40;

        /// <summary>깊이 하나에 허용하는 노드 수. 넘기면 그 깊이를 접고 직전 깊이의 수를 쓴다.</summary>
        private const long NodeCap = 1200000;

        /// <summary>한 프레임에 쓸 시간. 이 안에 끝낸 뿌리 수까지만 보고 나머지는 다음 프레임에 잇는다.</summary>
        private const long BudgetMillis = 8;

        /// <summary>
        /// 뿌리 수 하나의 가지가 아무리 커도 여기서 자른다. 프레임을 지키는 마지막 선이다.
        /// 잘린 수는 alpha 를 그대로 돌려주므로 "더 나을 것 없음"으로 보인다 -
        /// 실제보다 좋아 보이는 쪽으로는 틀리지 않는다.
        /// </summary>
        private const long CeilingMillis = 25;

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

        // ---------- 한 깊이를 여러 프레임에 걸쳐 훑기 위한 자리 ----------

        private readonly Stopwatch clock = new Stopwatch();
        private readonly List<ChessMove> partial = new List<ChessMove>();
        private readonly List<int> partialScores = new List<int>();

        private int pendingDepth;
        private int pendingCount;
        private int pendingIndex;
        private int pendingAlpha;

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

        /// <summary>
        /// 이번 프레임의 몫만큼 훑는다. 깊이 하나가 한 프레임에 안 끝나면 다음 프레임이 잇는다.
        /// </summary>
        public void Step()
        {
            if (Done) return;

            if (pendingDepth == 0) BeginDepth(CompletedDepth + 1);

            clock.Reset();
            clock.Start();

            ChessMove[] moves = stack[0];

            // 아무리 무거운 판에서도 한 수는 본다. 그래야 앞으로 나아간다.
            do
            {
                if (pendingIndex >= pendingCount) break;

                ChessMove move = moves[pendingIndex++];
                board.Make(ref move);
                int score = -AlphaBeta(pendingDepth - 1, 1, -ChessEval.MateScore * 2, -pendingAlpha);
                board.Unmake(in move);

                partial.Add(move);
                partialScores.Add(score);

                if (score > pendingAlpha) pendingAlpha = score;

                if (nodes <= NodeCap) continue;

                // 이 깊이는 너무 무겁다. 직전 깊이에서 찾은 수를 그대로 쓴다.
                exhausted = true;
                pendingDepth = 0;
                return;
            }
            while (clock.ElapsedMilliseconds < BudgetMillis);

            if (pendingIndex < pendingCount) return;   // 남은 것은 다음 프레임에

            Commit();
        }

        private void BeginDepth(int depth)
        {
            pendingDepth = depth;
            pendingIndex = 0;
            pendingAlpha = -ChessEval.MateScore * 2;
            nodes = 0;

            partial.Clear();
            partialScores.Clear();

            pendingCount = ChessRules.GenerateLegal(board, stack[0]);
            Order(stack[0], pendingCount, 0);
        }

        /// <summary>한 깊이를 다 훑었다. 여기서만 결과가 바뀐다 - 반쯤 훑은 목록은 절대 새지 않는다.</summary>
        private void Commit()
        {
            // 점수 내림차순. 차선수를 고를 수 있어야 하므로 목록째 정렬해 둔다.
            for (int i = 1; i < partial.Count; i++)
            {
                ChessMove move = partial[i];
                int score = partialScores[i];
                int j = i - 1;

                while (j >= 0 && partialScores[j] < score)
                {
                    partial[j + 1] = partial[j];
                    partialScores[j + 1] = partialScores[j];
                    j--;
                }

                partial[j + 1] = move;
                partialScores[j + 1] = score;
            }

            rootMoves.Clear();
            rootScores.Clear();
            rootMoves.AddRange(partial);
            rootScores.AddRange(partialScores);

            CompletedDepth = pendingDepth;
            pendingDepth = 0;

            // 외통이 보이면 더 깊이 볼 이유가 없다.
            if (rootScores.Count > 0 && Math.Abs(rootScores[0]) > ChessEval.MateScore - 100) exhausted = true;
        }

        /// <summary>
        /// 더 볼 수 있는가. 노드 수는 이 깊이 전체의 한도고, 시계는 이 프레임의 마지막 선이다.
        /// 시계를 노드마다 읽으면 시계가 더 비싸므로 512 노드에 한 번만 본다.
        /// </summary>
        private bool OutOfTime()
        {
            if (nodes > NodeCap) return true;

            return (nodes & 511L) == 0L && clock.ElapsedMilliseconds >= CeilingMillis;
        }

        private int AlphaBeta(int depth, int ply, int alpha, int beta)
        {
            nodes++;
            if (OutOfTime()) return alpha;

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

                if (OutOfTime()) return alpha;
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
            if (OutOfTime()) return alpha;

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

                if (OutOfTime()) return alpha;
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
