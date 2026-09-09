using RimWorld;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 성격이 판에 비치는 자리 (명세 §10.2).
    ///
    /// <b>대부분은 이미 공짜다.</b> 두 사람을 붙여 놓기만 하면 바닐라가 성격대로 굴린다 —
    /// 직설적은 부정적 상호작용 가중치가 2.3 배이고, 다정다감은 그 확률이 아예 0 이며,
    /// 사이코패스는 사교로 기분이 흔들리지 않는다. 우리는 그 말을 창으로 옮기기만 한다(§7.2).
    ///
    /// 여기 있는 것은 바닐라가 대신해 주지 않는 두 조각뿐이다 — 다정한 상대의 무르기 한 칸과,
    /// 화가 나면 판을 엎는 것.
    ///
    /// <b>판의 규칙은 성격으로 바뀌지 않는다.</b> 무르기 한 칸은 원래 플레이어가 설정에서
    /// 만지는 값이라 예외지만, 그 밖에 성격이 말의 움직임이나 확률을 건드리기 시작하면
    /// 아홉 게임 × 성격 수만큼의 규칙이 생기고 전적이 무슨 뜻인지 알 수 없게 된다.
    /// </summary>
    public static class TogetherPersonality
    {
        /// <summary>강한 공격성 유전자. 바이오텍이 없으면 이 Def 자체가 없다.</summary>
        private const string HyperAggressiveGene = "Aggression_HyperAggressive";

        /// <summary>
        /// 지고 있는 판을 엎을지 0.5 초마다 재는 확률. 낮다 — 이건 늘 일어나는 일이 아니라
        /// 한 번 겪으면 기억에 남는 일이어야 한다. 2 분쯤 밀리면 절반 조금 못 미친다.
        /// </summary>
        private const float FlipChancePerCheck = 0.002f;

        private static GeneDef hyperAggressive;
        private static bool geneLookedUp;

        public static bool Enabled
        {
            get
            {
                PRSettings settings = PRMod.Settings;
                return Together.Enabled && settings != null && settings.playTogetherPersonality;
            }
        }

        // ---------- 다정다감 ----------

        /// <summary>
        /// 다정한 상대는 좀 봐준다 — 이 판에만 무르기가 한 칸 늘어난다.
        ///
        /// 전적이 흐려지지 않는다. <c>undoLimit</c> 은 원래 플레이어가 설정에서 바꾸는 값이라
        /// 원장이 애초에 무르기로 정규화되어 있지 않고, 무르기 0 승(완봉)은 그대로 안전하다.
        /// </summary>
        public static int UndoBonus(Pawn opponent)
        {
            if (!Enabled || opponent == null) return 0;
            if (!PRMod.Settings.playTogetherKindUndo) return 0;

            return HasTrait(opponent, TraitDefOf.Kind) ? 1 : 0;
        }

        // ---------- 강한 공격성 ----------

        /// <summary>
        /// 지고 있는 판을 엎을 때가 됐는가. 창이 0.5 초마다 묻는다.
        ///
        /// "판 엎기"의 바닐라 이름은 <b>사교 다툼</b>이다. 우리가 엎는 연출을 만들지 않고
        /// 바닐라 정신 상태를 그대로 켠다 — 그러면 두 Job 이 끊기고, 판은 §7.1 의 결합으로
        /// 저절로 닫힌다. 새 Def 도 Harmony 도 필요 없다.
        /// </summary>
        public static bool ShouldFlip(Pawn seated, Pawn opponent, MiniGameWorker worker)
        {
            if (!Enabled || !PRMod.Settings.playTogetherBoardFlip) return false;
            if (seated == null || opponent == null || worker == null) return false;
            if (seated == opponent) return false;

            // 사용자의 그림은 **질 것 같으면** 엎는 것이다. 판세를 모르는 게임은 엎지 않는다.
            bool? losing = worker.Losing;
            if (!losing.HasValue || !losing.Value) return false;

            if (!IsHyperAggressive(opponent)) return false;
            if (opponent.InMentalState || seated.InMentalState) return false;

            if (opponent.interactions == null || !opponent.interactions.SocialFightPossible(seated))
                return false;

            return Rand.Chance(FlipChancePerCheck);
        }

        /// <summary>실제로 엎는다. 문구는 우리 것이고 나머지는 바닐라가 한다.</summary>
        public static void Flip(Pawn seated, Pawn opponent)
        {
            if (opponent == null || opponent.interactions == null || seated == null) return;

            opponent.interactions.StartSocialFight(seated, "PR.Together.BoardFlip");
        }

        // ---------- 조각 ----------

        private static bool HasTrait(Pawn pawn, TraitDef trait)
        {
            return pawn.story != null && pawn.story.traits != null && pawn.story.traits.HasTrait(trait);
        }

        /// <summary>
        /// 바이오텍이 없으면 이 유전자 Def 가 아예 없다. 이름으로 조용히 찾아 한 번만 기억한다 —
        /// DefOf 로 두면 DLC 없는 판에서 로그에 붉은 줄이 남는다.
        /// </summary>
        private static bool IsHyperAggressive(Pawn pawn)
        {
            if (pawn.genes == null) return false;

            if (!geneLookedUp)
            {
                hyperAggressive = DefDatabase<GeneDef>.GetNamedSilentFail(HyperAggressiveGene);
                geneLookedUp = true;
            }

            return hyperAggressive != null && pawn.genes.HasActiveGene(hyperAggressive);
        }
    }
}
