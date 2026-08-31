using Ur.Core;
using UnityEngine;
using Verse;

namespace Ur
{
    /// <summary>
    /// 4면체 주사위 4개. 표시된 꼭짓점이 위로 오면 1(채워진 원), 아니면 0(빈 고리).
    /// </summary>
    public static class UrDiceWidget
    {
        public const float DieSize = 28f;
        public const float DieGap = 8f;
        private const float TotalWidth = 92f;

        public static float Width
        {
            get { return UrDice.DiceCount * (DieSize + DieGap) - DieGap + TotalWidth; }
        }

        public static void Draw(Rect area, UrRoll roll, bool revealed)
        {
            float diceWidth = UrDice.DiceCount * (DieSize + DieGap) - DieGap;
            float x = area.x + (area.width - (diceWidth + TotalWidth)) / 2f;
            float y = area.y + (area.height - DieSize) / 2f;

            for (int i = 0; i < UrDice.DiceCount; i++)
            {
                Rect die = new Rect(x + i * (DieSize + DieGap), y, DieSize, DieSize);
                bool marked = revealed && roll.Die(i);

                GUI.color = revealed ? (marked ? UrTheme.DieMarked : UrTheme.DieBlank) : UrTheme.DieHidden;
                GUI.DrawTexture(die, marked ? UrTextures.Disc : UrTextures.Ring);
                GUI.color = Color.white;
            }

            Rect totalRect = new Rect(x + diceWidth, area.y, TotalWidth, area.height);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = revealed ? UrTheme.ActiveTurn : UrTheme.DieHidden;
            Widgets.Label(totalRect, revealed ? "= " + roll.Total : "= ?");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
