using System.Collections.Generic;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    public class PRMod : Mod
    {
        public static PRSettings Settings { get; private set; }

        private Vector2 scroll;
        private float contentHeight = 1400f;

        public PRMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PRSettings>();
        }

        public override string SettingsCategory()
        {
            return "PR.ModTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect view = new Rect(0f, 0f, inRect.width - 24f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scroll, view);

            Listing_Standard list = new Listing_Standard();
            list.Begin(view);

            DoGamesSection(list);
            DoPlaySection(list);
            DoMasterySection(list);
            DoSessionSection(list);

            foreach (MiniGameDef game in Games)
            {
                if (!Settings.IsEnabled(game)) continue;

                MiniGameWorker sample = game.WorkerSample;
                if (sample == null) continue;

                Section(list, game.LabelCap);
                sample.DoSettings(list);
            }

            contentHeight = list.CurHeight + 24f;
            list.End();

            Widgets.EndScrollView();
        }

        private static List<MiniGameDef> Games
        {
            get { return DefDatabase<MiniGameDef>.AllDefsListForReading; }
        }

        private void DoGamesSection(Listing_Standard list)
        {
            List<MiniGameDef> games = Games;
            if (games.Count <= 1) return;

            Section(list, "PR.Settings.Group.Games".Translate());
            Note(list, "PR.Settings.Games.Desc".Translate());

            foreach (MiniGameDef game in games)
            {
                bool enabled = Settings.IsEnabled(game);
                bool was = enabled;

                list.CheckboxLabeled(game.LabelCap, ref enabled, game.description);
                if (enabled != was) Settings.SetEnabled(game, enabled);
            }
        }

        private void DoPlaySection(Listing_Standard list)
        {
            Section(list, "PR.Settings.Group.Play".Translate());
            list.CheckboxLabeled("PR.Settings.Pause".Translate(), ref Settings.pauseWhilePlaying,
                "PR.Settings.Pause.Desc".Translate());
            list.CheckboxLabeled("PR.Settings.Immersion".Translate(), ref Settings.immersionMode,
                "PR.Settings.Immersion.Desc".Translate());

            // 몰입 모드의 두 번째 칸. 부모가 꺼져 있으면 그리지도 않는다 -
            // 무효화 블록이 쓰는 것과 같은 문법이다.
            if (Settings.immersionMode) DoTogetherSection(list);
            list.CheckboxLabeled("PR.Settings.LinkSkill".Translate(), ref Settings.linkToPawnSkill,
                "PR.Settings.LinkSkill.Desc".Translate());
            list.CheckboxLabeled("PR.Settings.Sounds".Translate(), ref Settings.sounds);

            list.Label("PR.Settings.ThinkTime".Translate(Settings.botThinkSeconds.ToString("0.00")));
            Settings.botThinkSeconds = list.Slider(Settings.botThinkSeconds, 0f, 1.5f);

            if (list.ButtonText("PR.Btn.Records".Translate()))
                Find.WindowStack.Add(new Dialog_Leaderboard(null));
        }

        /// <summary>
        /// 몰입 모드 2칸. 취향이 갈리는 값만 올린다 - 끄는 쪽이 언제나 안전하고,
        /// 기본값만 두면 설계한 그대로다.
        /// </summary>
        private void DoTogetherSection(Listing_Standard list)
        {
            list.CheckboxLabeled("PR.Settings.Together".Translate(), ref Settings.playTogether,
                "PR.Settings.Together.Desc".Translate());

            if (!Settings.playTogether) return;

            // 판이 가구에 남지 않으면 "밥 먹고 와서 이어 둔다" 가 성립하지 않는다.
            if (!Settings.saveSessions)
            {
                Note(list, "PR.Settings.Together.NeedsSave".Translate());
                return;
            }

            list.Label("PR.Settings.Together.Range".Translate(Settings.playTogetherRange));
            Settings.playTogetherRange = Mathf.RoundToInt(list.Slider(Settings.playTogetherRange, 5f, 80f));

            list.CheckboxLabeled("PR.Settings.Together.Children".Translate(), ref Settings.playTogetherChildren);
            list.CheckboxLabeled("PR.Settings.Together.Visitors".Translate(), ref Settings.playTogetherVisitors);
            list.CheckboxLabeled("PR.Settings.Together.Personality".Translate(), ref Settings.playTogetherPersonality,
                "PR.Settings.Together.Personality.Desc".Translate());
        }

        private void DoMasterySection(Listing_Standard list)
        {
            Section(list, "PR.Settings.Group.Mastery".Translate());
            Note(list, "PR.Settings.Mastery.Explain".Translate(
                (Mastery.BonusPerTier * 100f).ToString("0"),
                Mastery.BonusPercentText(GameRecord.MaxTiers)));

            list.CheckboxLabeled("PR.Settings.MasteryBonus".Translate(), ref Settings.masteryBonus);
            list.CheckboxLabeled("PR.Settings.PlayThought".Translate(), ref Settings.playThought,
                "PR.Settings.PlayThought.Desc".Translate());

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component != null)
            {
                foreach (MiniGameDef game in Games)
                {
                    if (!game.hasMatch || !Settings.IsEnabled(game)) continue;

                    int tier = component.MasteryTier(game);
                    if (tier <= 0) continue;

                    Note(list, "PR.Settings.Mastery.Current".Translate(
                        game.LabelCap, tier, game.difficultyCount, Mastery.BonusPercentText(tier)));
                }
            }

            if (Settings.HasColonyImpact)
            {
                if (list.ButtonText("PR.Settings.DisableImpact".Translate()))
                    Settings.DisableColonyImpact();
            }
            else
            {
                Note(list, "PR.Settings.NoImpact".Translate());
            }
        }

        private void DoSessionSection(Listing_Standard list)
        {
            Section(list, "PR.Settings.Group.Session".Translate());
            list.CheckboxLabeled("PR.Settings.SaveSessions".Translate(), ref Settings.saveSessions,
                "PR.Settings.SaveSessions.Desc".Translate());

            if (Settings.saveSessions)
            {
                list.CheckboxLabeled("PR.Settings.Invalidate".Translate(), ref Settings.invalidateSessions,
                    "PR.Settings.Invalidate.Desc".Translate());

                if (Settings.invalidateSessions)
                {
                    list.CheckboxLabeled("    " + "PR.Invalidation.Combat.Label".Translate(), ref Settings.invalidateOnCombat);
                    list.CheckboxLabeled("    " + "PR.Invalidation.Cleaning.Label".Translate(), ref Settings.invalidateOnCleaning);
                    list.CheckboxLabeled("    " + "PR.Invalidation.Repair.Label".Translate(), ref Settings.invalidateOnRepair);
                    list.CheckboxLabeled("    " + "PR.Invalidation.Damaged.Label".Translate(), ref Settings.invalidateOnDamage);
                    list.CheckboxLabeled("    " + "PR.Invalidation.Moved.Label".Translate(), ref Settings.invalidateOnMove);
                    list.CheckboxLabeled("    " + "PR.Invalidation.Expired.Label".Translate(), ref Settings.invalidateOnExpiry);

                    if (Settings.invalidateOnExpiry)
                    {
                        list.Label("PR.Settings.ExpiryDays".Translate(Settings.sessionExpiryDays));
                        Settings.sessionExpiryDays = Mathf.RoundToInt(list.Slider(Settings.sessionExpiryDays, 1f, 15f));
                    }
                }
            }

            list.Label("PR.Settings.UndoLimit".Translate(Settings.undoLimit));
            Settings.undoLimit = Mathf.RoundToInt(list.Slider(Settings.undoLimit, 0f, 10f));
            list.CheckboxLabeled("PR.Settings.NoUndoHard".Translate(), ref Settings.noUndoOnHardDifficulty);
        }

        // ---------- 표시 유틸 ----------

        public static void Section(Listing_Standard list, string label)
        {
            list.Gap(8f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            list.Label(label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            list.GapLine(2f);
        }

        public static void Note(Listing_Standard list, string text)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            list.Label(text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
    }
}
