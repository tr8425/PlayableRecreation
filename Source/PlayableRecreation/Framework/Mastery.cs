using RimWorld;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 숙련도. 난이도를 하나 클리어할 때마다 영구적으로 한 단계 오르고, 식민자가
    /// (바닐라 여가로) 그 게임을 할 때 얻는 여가·경험치를 아주 조금씩 올려준다.
    ///
    /// 같은 판을 반복해서 파밍할 여지가 없도록 **난이도별 최초 1회만** 인정한다.
    /// 게임마다 따로 쌓인다 — 우르를 마스터해도 편자는 처음부터다.
    /// </summary>
    public static class Mastery
    {
        /// <summary>단계당 증가율. 5단계 전부 모아도 +20%.</summary>
        public const float BonusPerTier = 0.04f;

        /// <summary>보너스를 얹는 주기(틱).</summary>
        public const int IntervalTicks = 250;

        /// <summary>바닐라 여가 획득 기본치(틱당). JoyUtility 의 값과 같다.</summary>
        public const float BaseJoyPerTick = 0.36f / 1000f;

        public static int CurrentTier(MiniGameDef game)
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            return component != null ? component.MasteryTier(game) : 0;
        }

        public static string BonusPercentText(int tier)
        {
            return (tier * BonusPerTier * 100f).ToString("0");
        }

        /// <summary>플레이어가 이겼을 때 호출. 처음 깬 난이도면 알림을 띄운다.</summary>
        public static void RecordClear(MiniGameDef game, int tier)
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null || !component.RecordClear(game, tier)) return;

            int now = component.MasteryTier(game);

            Messages.Message(
                "PR.Mastery.Cleared".Translate(
                    game.TierLabel(tier), game.label, now, game.difficultyCount, BonusPercentText(now)),
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
