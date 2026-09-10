using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>판 시작 전 난이도 선택. 마지막 선택은 게임마다 따로 기억된다.</summary>
    public class Dialog_Difficulty : Window
    {
        private const float RowHeight = 54f;
        private const float RowGap = 8f;

        private readonly MiniGameDef game;
        private readonly Thing board;
        private readonly Pawn seatedPawn;

        /// <summary>고른 상대. 없으면 예전과 같은 창이다.</summary>
        private readonly Pawn opponentPawn;

        /// <summary>연습 판은 숙련도에도 전적에도 기록하지 않는다. 규칙을 익히는 용도.</summary>
        private bool practice;

        public override Vector2 InitialSize
        {
            get { return new Vector2(460f, 100f + game.difficultyCount * (RowHeight + RowGap) + 88f); }
        }

        public Dialog_Difficulty(MiniGameDef game, Thing board, Pawn seatedPawn)
            : this(game, board, seatedPawn, null)
        {
        }

        public Dialog_Difficulty(MiniGameDef game, Thing board, Pawn seatedPawn, Pawn opponentPawn)
        {
            this.game = game;
            this.board = board;
            this.seatedPawn = seatedPawn;
            this.opponentPawn = opponentPawn;

            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;      // 고르는 동안은 멈춘다. 시간이 흐르는 것은 판이 시작된 뒤부터.
        }

        private string LastKey
        {
            get { return game.defName + ".tier"; }
        }

        /// <summary>실력을 재는 사람. 상대가 있으면 맞은편에 앉은 그 사람이다 (D2).</summary>
        private Pawn Measured
        {
            get { return GameEntry.Measured(seatedPawn, opponentPawn); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f),
                "PR.Difficulty.Title".Translate(game.LabelCap));
            Text.Font = GameFont.Small;

            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(inRect.x, inRect.y + 34f, inRect.width, 22f), "PR.Difficulty.Hint".Translate());
            GUI.color = Color.white;

            float y = inRect.y + 62f;

            // 실력에 부치는 칸은 잠근다. 아래로는 얼마든지 내려갈 수 있고 위로만 못 간다.
            Pawn measured = Measured;
            int cap = GameEntry.CapForPawn(game, measured);
            int skill = GameEntry.EffectiveSkill(game, measured);

            int last = Mathf.Min(cap,
                game.ClampTier(PRMod.Settings.GetInt(LastKey, game.difficultyCount / 2)));

            for (int tier = 0; tier < game.difficultyCount; tier++)
            {
                bool locked = tier > cap;

                Rect row = new Rect(inRect.x, y, inRect.width, RowHeight);
                y += RowHeight + RowGap;

                Widgets.DrawMenuSection(row);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                if (tier == last)
                {
                    GUI.color = PRTheme.ActiveTurn;
                    Widgets.DrawBox(row, 2);
                    GUI.color = Color.white;
                }

                GUI.color = locked ? PRTheme.Dim : Color.white;
                Widgets.Label(new Rect(row.x + 12f, row.y + 4f, row.width - 24f, 24f), game.TierLabel(tier));
                GUI.color = Color.white;

                Text.Font = GameFont.Tiny;
                GUI.color = PRTheme.Dim;

                // 잠긴 칸은 설명 대신 **왜 잠겼는지**를 적는다. 이유가 안 보이면 그냥 고장이다.
                string under = locked && measured != null
                    ? "PR.Difficulty.TooHard".Translate(
                        measured.LabelShortCap, game.linkedSkill.LabelCap,
                        skill, GameEntry.SkillNeededFor(game, tier)).ToString()
                    : game.TierDesc(tier);

                Widgets.Label(new Rect(row.x + 12f, row.y + 26f, row.width - 24f, 22f), under);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                if (locked || (tier != 0 && practice))
                {
                    // 연습은 항상 가장 낮은 단계로 — 표시만 흐리게 하고 클릭은 그대로 받는다.
                    // 잠긴 칸은 흐리게 덮고 클릭도 안 받는다.
                    Widgets.DrawBoxSolid(row, new Color(0f, 0f, 0f, 0.35f));
                }

                if (Widgets.ButtonInvisible(row) && !locked) Start(tier);
            }

            Rect practiceRow = new Rect(inRect.x, y + 6f, inRect.width, 26f);
            Widgets.CheckboxLabeled(practiceRow, "PR.Difficulty.Practice".Translate(), ref practice);
            TooltipHandler.TipRegion(practiceRow, "PR.Difficulty.Practice.Desc".Translate());
        }

        private void Start(int tier)
        {
            // 창을 그리는 사이에 실력이 바뀌었을 수도 있다. 마지막으로 한 번 더 자른다.
            if (!practice) tier = Mathf.Min(tier, GameEntry.CapForPawn(game, Measured));

            if (practice) tier = 0;
            else
            {
                PRMod.Settings.SetInt(LastKey, tier);
                PRMod.Settings.Write();
            }

            Close(false);
            GameEntry.Commit(game, board, seatedPawn, opponentPawn, tier, practice);
        }
    }
}
