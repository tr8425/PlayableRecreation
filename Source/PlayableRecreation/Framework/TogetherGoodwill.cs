using RimWorld;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 방문객과 한 판 두고 났을 때 그 팩션의 우호도가 움직인다 (명세 §11).
    ///
    /// 고정값 한 번이 아니라 **판이 어떻게 끝났는지**를 본다. 계산식처럼 읽히면
    /// 이야기가 아니라 표가 되므로 크기에 난수를 얹는다 — 다만 <b>부호에는 얹지 않는다.</b>
    /// "잘 뒀는데 사이가 나빠졌다"는 배울 것이 없는 결과다.
    ///
    /// 우호도는 은으로도 못 사는 전략 자원이라 파밍을 막는 두 가지를 설정으로 내리지 않는다:
    /// 난이도가 크기를 정하고, 한 방문에 한 번만 움직인다.
    /// </summary>
    public static class TogetherGoodwill
    {
        // ---------- 표 (§11.1) ----------
        //
        // 이 값들은 그대로 쓰이지 않는다. 난이도로 한 번, 설정 배율로 한 번,
        // 난수로 한 번 곱한 뒤에 정수로 떨어진다.

        /// <summary>접전 끝에 끝났다. 손님이 즐거웠다 — 이게 최고값이다.</summary>
        private const int CloseGame = 8;

        /// <summary>평범하게 끝났거나 손님이 이겼다. 시간은 잘 보냈다.</summary>
        private const int Ordinary = 4;

        /// <summary>손님이 압도적으로 졌다. 손님을 부른 자리가 아니게 됐다.</summary>
        private const int Crushed = -6;

        /// <summary>우리 쪽이 판을 엎고 일어섰다.</summary>
        private const int Walkout = -3;

        /// <summary>난수의 폭. 크기에만 얹는다.</summary>
        private const float SpreadMin = 0.7f;
        private const float SpreadMax = 1.3f;

        /// <summary>
        /// 한 판이 기록된 직후에 부른다. 상대가 방문객이 아니면 아무 일도 하지 않는다.
        /// 연습 판·승부 없는 게임은 애초에 <c>RecordResult</c> 가 여기까지 오지 않는다.
        /// </summary>
        public static void Apply(MiniGameDef game, Pawn opponent, MatchResult result)
        {
            PRSettings settings = PRMod.Settings;
            if (!Together.Enabled || settings == null || !settings.playTogetherGoodwill) return;

            Faction faction = VisitorFaction(opponent);
            if (faction == null) return;

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return;

            int amount = Outcome(result);

            // 마이너스를 아예 안 보고 싶은 사람이 있다. 끄면 0 이지, 뒤집히지 않는다.
            if (amount < 0 && !settings.playTogetherGoodwillDrop) amount = 0;

            int delta = Scale(amount, game, result.Tier, settings.playTogetherGoodwillScale);

            // 방문 1회당 한 번. 여러 판을 뒀다면 **가장 나중 판**을 쓴다 —
            // 앞 판에서 준 만큼을 빼고 이번 판의 값으로 갈아 끼우므로, 만회할 기회가 남는다.
            int already = component.GoodwillApplied(faction);
            int change = delta - already;
            if (change == 0) return;

            int before = faction.GoodwillWith(Faction.OfPlayer);

            // 우리가 직접 한 줄을 띄우므로 바닐라 문구는 끈다 — 같은 말을 두 번 하지 않는다.
            if (!faction.TryAffectGoodwillWith(Faction.OfPlayer, change, false)) return;

            component.SetGoodwillApplied(faction, delta);

            int after = faction.GoodwillWith(Faction.OfPlayer);
            if (after == before) return;

            Announce(opponent, faction, delta != 0 ? delta : change, result, before, after);
        }

        /// <summary>
        /// 판을 표의 한 줄로 옮긴다.
        ///
        /// <b>"압도적 패배"의 주어가 뒤집혀 있다.</b> 플레이어가 크게 지는 게 아니라
        /// **손님이 크게 지는 쪽**이 마이너스다. 이야기로는 손님을 뭉개는 게 실례이고,
        /// 설계로는 못해서 벌을 받는 구조가 아니게 된다.
        ///
        /// 지금 우리가 실제로 구별할 수 있는 것은 <see cref="MatchResult.Flawless"/> 하나다 —
        /// "이긴 쪽이 한 점도 내주지 않았다". 그래서 손님이 이긴 판은 접전인지 압승인지
        /// 나누지 않는데, 표에서 그 둘의 값이 어차피 같아서 손해가 없다.
        /// </summary>
        private static int Outcome(MatchResult result)
        {
            if (result.Resigned) return Walkout;
            if (result.Won) return result.Flawless ? Crushed : CloseGame;

            return Ordinary;
        }

        /// <summary>
        /// 난이도가 크기를 정한다. 맨 아래 칸에서 압승만 반복하는 것이 최적 전략이 되면
        /// 체스판이 동맹 자판기가 되므로, **낮은 칸에서는 폭이 커도 거의 안 움직인다.**
        /// </summary>
        private static int Scale(int amount, MiniGameDef game, int tier, int percent)
        {
            if (amount == 0 || percent <= 0) return 0;

            float value = amount * DifficultyFactor(game, tier) * (percent / 100f);
            value *= Rand.Range(SpreadMin, SpreadMax);

            return Mathf.RoundToInt(value);
        }

        private static float DifficultyFactor(MiniGameDef game, int tier)
        {
            int count = game != null ? game.difficultyCount : 1;
            if (count <= 1) return 1f;

            return Mathf.Clamp01((tier + 1) / (float)count);
        }

        /// <summary>상대가 우호도를 가진 손님인가. 우리 식구도, 적도, 야생도 아니어야 한다.</summary>
        private static Faction VisitorFaction(Pawn opponent)
        {
            if (opponent == null) return null;

            Faction player = Faction.OfPlayer;
            if (player == null) return null;

            Faction faction = opponent.Faction;
            if (faction == null || faction == player || faction.IsPlayer) return null;
            if (!faction.HasGoodwill || faction.temporary) return null;
            if (faction.HostileTo(player)) return null;

            return faction;
        }

        private static void Announce(Pawn opponent, Faction faction, int shown, MatchResult result,
            int before, int after)
        {
            string key;
            if (shown > 0) key = "PR.Together.Goodwill.Up";
            else if (result.Resigned) key = "PR.Together.Goodwill.Walkout";
            else key = "PR.Together.Goodwill.Down";

            Messages.Message(
                key.Translate(opponent.LabelShortCap, faction.Name, before.ToString(), after.ToString()),
                opponent,
                shown > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent,
                false);
        }
    }
}
