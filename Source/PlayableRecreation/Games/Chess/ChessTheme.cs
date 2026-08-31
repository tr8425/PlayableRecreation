using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Chess
{
    /// <summary>
    /// 체스판의 색과 기물 그림. 기물은 알파 실루엣 한 장씩이고 색은 여기서 입힌다 -
    /// 흑백 두 벌을 따로 그리지 않는 이유이자, 어느 칸 위에서도 테두리로 떠 보이게 하는 방법이다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ChessTheme
    {
        public static readonly Color LightSquare = new Color(0.76f, 0.70f, 0.58f);
        public static readonly Color DarkSquare = new Color(0.35f, 0.29f, 0.24f);
        public static readonly Color Frame = new Color(0.19f, 0.15f, 0.12f);
        public static readonly Color Coord = new Color(1f, 1f, 1f, 0.30f);

        public static readonly Color WhitePiece = new Color(0.95f, 0.93f, 0.87f);
        public static readonly Color BlackPiece = new Color(0.13f, 0.12f, 0.13f);
        public static readonly Color WhiteEdge = new Color(0.26f, 0.22f, 0.18f);
        public static readonly Color BlackEdge = new Color(0.70f, 0.66f, 0.60f);

        public static readonly Color Selected = new Color(0.95f, 0.80f, 0.30f, 0.42f);
        public static readonly Color LastMove = new Color(0.90f, 0.78f, 0.35f, 0.20f);
        public static readonly Color MoveDot = new Color(0.26f, 0.50f, 0.33f, 0.60f);
        public static readonly Color CaptureRing = new Color(0.78f, 0.28f, 0.22f, 0.65f);
        public static readonly Color Check = new Color(0.80f, 0.19f, 0.15f, 0.45f);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.62f);

        /// <summary><see cref="Core.Piece.Kind"/> 로 색인한다. 0번은 빈 칸이라 비워 둔다.</summary>
        public static readonly Texture2D[] Pieces =
        {
            null,
            ContentFinder<Texture2D>.Get("PR/Chess/pawn"),
            ContentFinder<Texture2D>.Get("PR/Chess/knight"),
            ContentFinder<Texture2D>.Get("PR/Chess/bishop"),
            ContentFinder<Texture2D>.Get("PR/Chess/rook"),
            ContentFinder<Texture2D>.Get("PR/Chess/queen"),
            ContentFinder<Texture2D>.Get("PR/Chess/king"),
        };
    }

    /// <summary>바닐라 UI 사운드를 빌려 쓴다.</summary>
    [StaticConstructorOnStartup]
    public static class ChessSounds
    {
        public static readonly SoundDef Select = PRSounds.Lookup("Tick_Tiny");
        public static readonly SoundDef Move = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Capture = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Check = PRSounds.Lookup("TinyBell");
    }
}
