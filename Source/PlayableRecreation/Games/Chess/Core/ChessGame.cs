using System.Collections.Generic;

namespace Chess.Core
{
    /// <summary>
    /// 한 판. 판 하나에 기보와 되돌리기와 무승부 판정을 얹은 것이 전부다.
    /// 창은 이 클래스만 알면 되고, 탐색과 규칙은 그 아래에 있다.
    /// </summary>
    public sealed class ChessGame
    {
        private readonly ChessMove[] legal = new ChessMove[ChessRules.MaxMoves];
        private readonly List<ulong> history = new List<ulong>();
        private readonly List<ChessMove> played = new List<ChessMove>();
        private readonly List<string> notation = new List<string>();

        public ChessBoard Board { get; private set; }
        public int LegalCount { get; private set; }
        public ChessResult Result { get; private set; }

        /// <summary>플레이어가 잡은 색. 흑을 잡으면 상대가 먼저 둔다.</summary>
        public ChessSide PlayerSide { get; private set; }

        public ChessMove[] Legal
        {
            get { return legal; }
        }

        public IReadOnlyList<string> Notation
        {
            get { return notation; }
        }

        public IReadOnlyList<ChessMove> Played
        {
            get { return played; }
        }

        public bool IsOver
        {
            get { return Result != ChessResult.Ongoing; }
        }

        /// <summary>이긴 쪽. 무승부이거나 아직 안 끝났으면 null.</summary>
        public ChessSide? Winner
        {
            get
            {
                if (Result != ChessResult.Checkmate) return null;
                return ChessBoard.Other(Board.SideToMove);
            }
        }

        public bool PlayerToMove
        {
            get { return Board.SideToMove == PlayerSide; }
        }

        public int Ply
        {
            get { return played.Count; }
        }

        public ChessGame(ChessSide playerSide) : this(ChessBoard.StartFen, playerSide)
        {
        }

        public ChessGame(string fen, ChessSide playerSide)
        {
            Board = ChessBoard.FromFen(fen);
            PlayerSide = playerSide;

            history.Add(ChessZobrist.Of(Board));
            Refresh();
        }

        private void Refresh()
        {
            LegalCount = ChessRules.GenerateLegal(Board, legal);
            Result = ChessRules.Result(Board, LegalCount, Repetitions());
        }

        private int Repetitions()
        {
            if (history.Count == 0) return 0;

            ulong current = history[history.Count - 1];
            int seen = 0;

            for (int i = 0; i < history.Count; i++)
                if (history[i] == current) seen++;

            return seen;
        }

        /// <summary>그 수가 지금 둘 수 있는 수인지 확인하고 둔다.</summary>
        public bool Play(int from, int to, int promotion)
        {
            for (int i = 0; i < LegalCount; i++)
            {
                ChessMove move = legal[i];
                if (move.From != from || move.To != to) continue;
                if (move.Promotion != 0 && move.Promotion != promotion) continue;

                Play(move);
                return true;
            }

            return false;
        }

        public void Play(ChessMove move)
        {
            notation.Add(ChessNotation.San(Board, move, legal, LegalCount));

            ChessMove applied = move;
            Board.Make(ref applied);
            played.Add(applied);
            history.Add(ChessZobrist.Of(Board));

            Refresh();
        }

        /// <summary>수를 물린다. 무르기는 내 차례로 돌아와야 하므로 보통 두 수씩이다.</summary>
        public void Undo(int plies)
        {
            for (int i = 0; i < plies && played.Count > 0; i++)
            {
                ChessMove move = played[played.Count - 1];
                played.RemoveAt(played.Count - 1);
                notation.RemoveAt(notation.Count - 1);
                history.RemoveAt(history.Count - 1);

                Board.Unmake(in move);
            }

            Refresh();
        }

        /// <summary>승격이 필요한 수인가. 창이 무엇으로 바꿀지 물어봐야 하는지 알아야 한다.</summary>
        public bool NeedsPromotion(int from, int to)
        {
            for (int i = 0; i < LegalCount; i++)
                if (legal[i].From == from && legal[i].To == to && legal[i].Promotion != 0) return true;

            return false;
        }

        /// <summary>그 칸에서 갈 수 있는 곳들. 판 위에 표시하는 데 쓴다.</summary>
        public bool CanMoveFrom(int square)
        {
            for (int i = 0; i < LegalCount; i++)
                if (legal[i].From == square) return true;

            return false;
        }

        // ---------- 세이브 ----------

        public string Fen
        {
            get { return Board.ToFen(); }
        }

        /// <summary>세이브에서 되살린다. 되풀이 판정을 위해 지나온 해시를 함께 받는다.</summary>
        public static ChessGame Restore(string fen, ChessSide playerSide, List<ulong> keys)
        {
            ChessGame game = new ChessGame(fen, playerSide);

            if (keys != null && keys.Count > 0)
            {
                game.history.Clear();
                game.history.AddRange(keys);
                game.Refresh();
            }

            return game;
        }

        public List<ulong> HistoryKeys
        {
            get { return new List<ulong>(history); }
        }
    }
}
