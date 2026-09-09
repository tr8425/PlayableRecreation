using System.Collections.Generic;
using Chess.Core;
using PlayableRecreation;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Chess
{
    /// <summary>
    /// 체스. 판이 스스로 굴러가지 않는 대신 상대가 생각을 한다 -
    /// 그 생각을 프레임워크의 <c>Tick</c> 에 한 깊이씩 나눠 실어, 창이 멈춰 보이지 않게 한다.
    /// </summary>
    public class ChessGameWorker : MiniGameWorker
    {
        private enum Stage
        {
            Player,
            Promotion,
            Thinking,
        }

        /// <summary>난이도별 탐색 깊이와, 일부러 최선을 비켜 둘 확률.</summary>
        private static readonly int[] Depths = { 1, 2, 3, 4, 5 };
        private static readonly double[] Blunders = { 0.55, 0.28, 0.12, 0.04, 0.0 };

        private static readonly int[] PromotionChoices = { Piece.Queen, Piece.Rook, Piece.Bishop, Piece.Knight };

        private const float CoordBand = 16f;

        private ChessGame game;
        private ChessSearch search;
        private System.Random rng;

        private Stage stage;
        private int selected = -1;
        private int promoteFrom = -1;
        private int promoteTo = -1;
        private float thinkUntil;

        private int lastFrom = -1;
        private int lastTo = -1;

        private bool flipped;
        private Rect boardRect;
        private float cell;

        private readonly List<string> log = new List<string>();
        private int lastNotationCount = -1;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return game != null ? game.Ply : 0; }
        }

        public override int Rounds
        {
            get { return game != null ? (game.Ply + 1) / 2 : 0; }
        }

        public override bool IsOver
        {
            get { return game != null && game.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return game != null && game.Winner.HasValue && game.Winner.Value == game.PlayerSide; }
        }

        /// <summary>기물 하나 내주지 않고 이긴 판.</summary>
        public override bool Flawless
        {
            get { return game != null && CapturesBy(ChessBoard.Other(game.PlayerSide)) == 0; }
        }

        /// <summary>
        /// 포 하나치 밀리고 있다면 지고 있는 것으로 본다. 한 기물 차는 아직 판이 아니다.
        /// <c>ChessEval.Evaluate</c> 는 둘 차례인 쪽에서 본 점수라 부호를 맞춰 준다.
        /// </summary>
        public override bool? Losing
        {
            get
            {
                if (game == null || game.IsOver) return null;

                int score = ChessEval.Evaluate(game.Board);
                if (game.Board.SideToMove != game.PlayerSide) score = -score;

                return score < -LosingMargin;
            }
        }

        /// <summary>마이너스 하나만큼. 그 아래는 흔히 드나드는 폭이다.</summary>
        private const int LosingMargin = 300;

        public override bool CanUndo
        {
            get { return game != null && !game.IsOver && stage == Stage.Player && game.Ply >= 2; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            // 색은 판마다 바뀐다. 흑을 잡으면 상대가 먼저 둔다.
            ChessSide side = ChessSettings.AlwaysWhite || (seed & 1) == 0 ? ChessSide.White : ChessSide.Black;

            game = new ChessGame(side);
            Reset(seed);
            BeginTurn(0f);
        }

        public override void Resume(MiniGameSaveData data)
        {
            ChessSaveData saved = data as ChessSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            game = saved.ToGame();
            Reset(Rand.Int);
            BeginTurn(0f);
        }

        public override MiniGameSaveData MakeSaveData()
        {
            return game != null && !game.IsOver ? new ChessSaveData(game) : null;
        }

        private void Reset(int seed)
        {
            rng = new System.Random(seed);
            search = null;
            selected = -1;
            promoteFrom = -1;
            promoteTo = -1;
            lastFrom = -1;
            lastTo = -1;
            flipped = game.PlayerSide == ChessSide.Black;
            lastNotationCount = -1;
        }

        // ---------- 진행 ----------

        private void BeginTurn(float now)
        {
            selected = -1;

            if (game.IsOver) return;

            if (game.PlayerToMove)
            {
                search = null;
                stage = Stage.Player;
                return;
            }

            int tier = Mathf.Clamp(Tier, 0, Depths.Length - 1);

            search = new ChessSearch(game.Board.Clone(), Depths[tier], Blunders[tier], rng);
            thinkUntil = now + Mathf.Max(0.25f, PRMod.Settings.botThinkSeconds);
            stage = Stage.Thinking;
        }

        public override void Tick(float now)
        {
            if (game == null || game.IsOver || stage != Stage.Thinking) return;

            // 한 프레임에 한 깊이. 끝까지 갈 때까지 뜸을 들이는 셈이 된다.
            if (search != null && !search.Done) { search.Step(); return; }
            if (now < thinkUntil) return;

            ChessMove move = search != null ? search.Chosen : default(ChessMove);

            if (move.IsNull) { stage = Stage.Player; return; }   // 둘 수가 없다면 이미 끝난 판이다

            Commit(move, now);
        }

        private void Commit(ChessMove move, float now)
        {
            bool capture = move.IsCapture;

            lastFrom = move.From;
            lastTo = move.To;

            game.Play(move);

            if (!game.IsOver && ChessRules.InCheck(game.Board, game.Board.SideToMove))
                PRSounds.Play(ChessSounds.Check);
            else
                PRSounds.Play(capture ? ChessSounds.Capture : ChessSounds.Move);

            BeginTurn(now);
        }

        public override void Undo()
        {
            if (!CanUndo) return;

            game.Undo(2);

            lastFrom = -1;
            lastTo = -1;
            selected = -1;

            if (game.Played.Count > 0)
            {
                ChessMove previous = game.Played[game.Played.Count - 1];
                lastFrom = previous.From;
                lastTo = previous.To;
            }

            PRSounds.Play(ChessSounds.Select);
            BeginTurn(Time.realtimeSinceStartup);
        }

        // ---------- 버튼 ----------

        /// <summary>체스는 누를 버튼이 없다. 판을 클릭하는 것이 전부다.</summary>
        public override string ActionLabel
        {
            get { return null; }
        }

        // ---------- 조작 ----------

        private bool TryMove(int from, int to)
        {
            if (game.NeedsPromotion(from, to))
            {
                promoteFrom = from;
                promoteTo = to;
                stage = Stage.Promotion;
                return true;
            }

            for (int i = 0; i < game.LegalCount; i++)
            {
                ChessMove move = game.Legal[i];
                if (move.From != from || move.To != to) continue;

                Commit(move, Time.realtimeSinceStartup);
                return true;
            }

            return false;
        }

        private void ChoosePromotion(int kind)
        {
            for (int i = 0; i < game.LegalCount; i++)
            {
                ChessMove move = game.Legal[i];
                if (move.From != promoteFrom || move.To != promoteTo || move.Promotion != kind) continue;

                promoteFrom = -1;
                promoteTo = -1;
                stage = Stage.Player;

                Commit(move, Time.realtimeSinceStartup);
                return;
            }
        }

        private void HandleClicks()
        {
            if (!Widgets.ButtonInvisible(boardRect)) return;

            int square = SquareAt(Event.current.mousePosition);
            if (square < 0) return;

            if (selected >= 0 && square != selected && TryMove(selected, square)) return;

            sbyte piece = game.Board.Squares[square];

            if (piece != Piece.None && Piece.SideOf(piece) == game.PlayerSide && game.CanMoveFrom(square))
            {
                selected = selected == square ? -1 : square;
                PRSounds.Play(ChessSounds.Select);
                return;
            }

            selected = -1;
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            if (game == null) return;

            LayoutBoard(area);
            DrawSquares();
            DrawMarks();
            DrawPieces(game.Board);
            DrawCoordinates();

            if (stage == Stage.Promotion) DrawPromotion();
            else if (stage == Stage.Player) HandleClicks();
        }

        /// <summary>주어진 자리에 정사각형으로 최대한 크게 앉힌다. 칸 크기는 정수로 떨어뜨린다.</summary>
        private void LayoutBoard(Rect field)
        {
            float band = ChessSettings.ShowCoordinates ? CoordBand : 0f;

            float size = Mathf.Max(64f, Mathf.Min(field.width - band, field.height - band));
            cell = Mathf.Floor(size / 8f);
            size = cell * 8f;

            boardRect = new Rect(Mathf.Floor(field.center.x - size * 0.5f + band * 0.5f),
                                 Mathf.Floor(field.center.y - size * 0.5f - band * 0.5f),
                                 size, size);
        }

        private Rect SquareRect(int square)
        {
            int file = Chess88.File(square);
            int rank = Chess88.Rank(square);

            int column = flipped ? 7 - file : file;
            int row = flipped ? rank : 7 - rank;

            return new Rect(boardRect.x + column * cell, boardRect.y + row * cell, cell, cell);
        }

        private int SquareAt(Vector2 point)
        {
            if (!boardRect.Contains(point)) return -1;

            int column = Mathf.Clamp((int)((point.x - boardRect.x) / cell), 0, 7);
            int row = Mathf.Clamp((int)((point.y - boardRect.y) / cell), 0, 7);

            return Chess88.Square(flipped ? 7 - column : column, flipped ? row : 7 - row);
        }

        private void DrawSquares()
        {
            Widgets.DrawBoxSolid(boardRect.ExpandedBy(3f), ChessTheme.Frame);

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int square = Chess88.Square(file, rank);
                    Widgets.DrawBoxSolid(SquareRect(square),
                        (file + rank) % 2 == 0 ? ChessTheme.DarkSquare : ChessTheme.LightSquare);
                }
            }
        }

        /// <summary>직전 수, 고른 기물, 갈 수 있는 칸, 그리고 체크. 판 위에 얹는 것은 이게 전부다.</summary>
        private void DrawMarks()
        {
            if (lastFrom >= 0) Widgets.DrawBoxSolid(SquareRect(lastFrom), ChessTheme.LastMove);
            if (lastTo >= 0) Widgets.DrawBoxSolid(SquareRect(lastTo), ChessTheme.LastMove);

            ChessBoard board = game.Board;

            if (!game.IsOver && ChessRules.InCheck(board, board.SideToMove))
            {
                int king = board.KingSquare[(int)board.SideToMove];
                if (king >= 0) Widgets.DrawBoxSolid(SquareRect(king), ChessTheme.Check);
            }

            if (selected < 0) return;

            Widgets.DrawBoxSolid(SquareRect(selected), ChessTheme.Selected);
            if (!ChessSettings.ShowLegalMoves) return;

            for (int i = 0; i < game.LegalCount; i++)
            {
                ChessMove move = game.Legal[i];
                if (move.From != selected) continue;

                DrawTarget(SquareRect(move.To), move.IsCapture);
            }
        }

        private static void DrawTarget(Rect box, bool capture)
        {
            if (capture)
            {
                GUI.color = ChessTheme.CaptureRing;
                GUI.DrawTexture(box.ContractedBy(box.width * 0.04f), PRTextures.Outline);
            }
            else
            {
                GUI.color = ChessTheme.MoveDot;
                GUI.DrawTexture(box.ContractedBy(box.width * 0.36f), PRTextures.Dot);
            }

            GUI.color = Color.white;
        }

        private void DrawPieces(ChessBoard board)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int square = Chess88.Square(file, rank);
                    sbyte piece = board.Squares[square];

                    if (piece != Piece.None) DrawPiece(SquareRect(square), piece);
                }
            }
        }

        /// <summary>실루엣 한 장을 두 번 그린다 - 조금 키워 테두리색으로, 그 위에 제 색으로.</summary>
        private static void DrawPiece(Rect box, sbyte piece)
        {
            int kind = Piece.Kind(piece);
            Texture2D texture = ChessTheme.Piece(kind);
            bool white = Piece.IsWhite(piece);

            if (texture == null) { DrawPieceLetter(box, kind, white); return; }

            Rect inner = box.ContractedBy(box.width * 0.07f);

            GUI.color = white ? ChessTheme.WhiteEdge : ChessTheme.BlackEdge;
            GUI.DrawTexture(inner.ExpandedBy(inner.width * 0.035f), texture);

            GUI.color = white ? ChessTheme.WhitePiece : ChessTheme.BlackPiece;
            GUI.DrawTexture(inner, texture);

            GUI.color = Color.white;
        }

        /// <summary>그림을 못 찾았을 때. 예쁘지는 않아도 판은 읽힌다.</summary>
        private static void DrawPieceLetter(Rect box, int kind, bool white)
        {
            GUI.color = white ? ChessTheme.WhitePiece : ChessTheme.BlackPiece;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(box, ChessTheme.Letter(kind));

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void DrawCoordinates()
        {
            if (!ChessSettings.ShowCoordinates) return;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ChessTheme.Coord;

            for (int i = 0; i < 8; i++)
            {
                int file = flipped ? 7 - i : i;
                int rank = flipped ? i : 7 - i;

                Widgets.Label(new Rect(boardRect.x + i * cell, boardRect.yMax, cell, CoordBand),
                    ((char)('a' + file)).ToString());

                Widgets.Label(new Rect(boardRect.x - CoordBand, boardRect.y + i * cell, CoordBand, cell),
                    ((char)('1' + rank)).ToString());
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawPromotion()
        {
            Widgets.DrawBoxSolid(boardRect, ChessTheme.Overlay);

            float size = cell * 1.25f;
            float width = size * PromotionChoices.Length;

            Rect row = new Rect(boardRect.center.x - width * 0.5f, boardRect.center.y - size * 0.5f, width, size);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(row.x, row.y - 28f, row.width, 24f), "CHS.Promotion".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            for (int i = 0; i < PromotionChoices.Length; i++)
            {
                Rect box = new Rect(row.x + i * size, row.y, size, size);

                Widgets.DrawBoxSolid(box, Mouse.IsOver(box) ? ChessTheme.Selected : ChessTheme.LastMove);
                DrawPiece(box, Piece.Make(PromotionChoices[i], game.PlayerSide));

                if (Widgets.ButtonInvisible(box)) ChoosePromotion(PromotionChoices[i]);
            }
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (game == null) return string.Empty;

                if (game.IsOver) return Verdict();

                if (stage == Stage.Promotion) return "CHS.Status.Promotion".Translate().ToString();
                if (stage == Stage.Thinking) return "CHS.Status.Thinking".Translate().ToString();

                return ChessRules.InCheck(game.Board, game.PlayerSide)
                    ? "CHS.Status.Check".Translate().ToString()
                    : "CHS.Status.Your".Translate().ToString();
            }
        }

        private string Verdict()
        {
            switch (game.Result)
            {
                case ChessResult.Checkmate:
                    return (PlayerWon ? "CHS.Status.WinPlayer" : "CHS.Status.WinOpponent").Translate().ToString();
                case ChessResult.Stalemate:
                    return "CHS.Status.Stalemate".Translate().ToString();
                case ChessResult.FiftyMove:
                    return "CHS.Status.FiftyMove".Translate().ToString();
                case ChessResult.Repetition:
                    return "CHS.Status.Repetition".Translate().ToString();
                default:
                    return "CHS.Status.Material".Translate().ToString();
            }
        }

        /// <summary>기보. 번호는 언제나 백부터 센다 - 흑을 잡았어도 마찬가지다.</summary>
        public override IReadOnlyList<string> Log
        {
            get
            {
                if (game == null) return log;
                if (game.Notation.Count == lastNotationCount) return log;

                lastNotationCount = game.Notation.Count;
                log.Clear();

                for (int i = 0; i < game.Notation.Count; i += 2)
                {
                    string white = game.Notation[i];
                    string black = i + 1 < game.Notation.Count ? game.Notation[i + 1] : string.Empty;

                    log.Add(string.Format("{0,3}.  {1,-8} {2}", i / 2 + 1, white, black));
                }

                return log;
            }
        }

        public override void FillTallies(int[] tallies)
        {
            if (game == null || tallies.Length < 3) return;

            tallies[0] = CapturesBy(game.PlayerSide);
            tallies[1] = CapturesBy(ChessBoard.Other(game.PlayerSide));
            tallies[2] = ChecksBy(game.PlayerSide);
        }

        /// <summary>첫 수는 언제나 백의 것이므로, 짝수 번째가 백이다.</summary>
        private static bool MovedBy(int ply, ChessSide side)
        {
            return (ply % 2 == 0) == (side == ChessSide.White);
        }

        private int CapturesBy(ChessSide side)
        {
            if (game == null) return 0;

            IReadOnlyList<ChessMove> played = game.Played;
            int count = 0;

            for (int i = 0; i < played.Count; i++)
                if (MovedBy(i, side) && played[i].IsCapture) count++;

            return count;
        }

        private int ChecksBy(ChessSide side)
        {
            if (game == null) return 0;

            IReadOnlyList<string> notation = game.Notation;
            int count = 0;

            for (int i = 0; i < notation.Count; i++)
            {
                if (!MovedBy(i, side) || notation[i].NullOrEmpty()) continue;

                char tail = notation[i][notation[i].Length - 1];
                if (tail == '+' || tail == '#') count++;
            }

            return count;
        }

        public override void DoSettings(Listing_Standard list)
        {
            ChessSettings.DoSettings(list);
        }

        // ---------- 튜토리얼 도식 ----------

        /// <summary>도식용 위치. 규칙을 설명하는 그림도 진짜 규칙으로 그린다.</summary>
        private static readonly string[] FigureFens =
        {
            "R5k1/5ppp/8/8/8/8/8/6K1 b - - 1 1",     // 백이 뒷줄에 몰아넣은 외통
            "7k/8/8/3N4/8/8/8/7K w - - 0 1",         // 나이트 하나가 갈 수 있는 곳
            ChessBoard.StartFen,
        };

        private ChessBoard[] figures;

        private ChessBoard Figure(int index)
        {
            if (figures == null) figures = new ChessBoard[FigureFens.Length];
            return figures[index] ?? (figures[index] = ChessBoard.FromFen(FigureFens[index]));
        }

        public override void DrawTutorialFigure(Rect area, int page)
        {
            bool wasFlipped = flipped;
            Rect wasBoard = boardRect;
            float wasCell = cell;

            flipped = false;

            switch (page)
            {
                case 0:
                    DrawStatic(area, Figure(0), -1, Chess88.Square(6, 7));
                    break;
                case 1:
                    DrawStatic(area, Figure(1), Chess88.Square(3, 4), -1);
                    break;
                case 2:
                    DrawStatic(area, Figure(2), Chess88.Square(4, 1), -1);
                    break;
                default:
                    DrawStatic(area, Figure(2), -1, -1);
                    break;
            }

            flipped = wasFlipped;
            boardRect = wasBoard;
            cell = wasCell;
        }

        private void DrawStatic(Rect area, ChessBoard board, int from, int checkSquare)
        {
            float size = Mathf.Max(64f, Mathf.Min(area.width, area.height));
            cell = Mathf.Floor(size / 8f);
            size = cell * 8f;

            boardRect = new Rect(Mathf.Floor(area.center.x - size * 0.5f),
                                 Mathf.Floor(area.center.y - size * 0.5f), size, size);

            DrawSquares();

            if (checkSquare >= 0) Widgets.DrawBoxSolid(SquareRect(checkSquare), ChessTheme.Check);

            if (from >= 0)
            {
                Widgets.DrawBoxSolid(SquareRect(from), ChessTheme.Selected);

                ChessMove[] moves = new ChessMove[ChessRules.MaxMoves];
                int count = ChessRules.GenerateLegal(board, moves);

                for (int i = 0; i < count; i++)
                    if (moves[i].From == from) DrawTarget(SquareRect(moves[i].To), moves[i].IsCapture);
            }

            DrawPieces(board);
        }
    }
}
