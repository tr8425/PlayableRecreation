using UnityEngine;
using Verse;
using Verse.Sound;

namespace PlayableRecreation.UI
{
    /// <summary>창 껍데기가 쓰는 색. 판 안쪽의 색은 게임이 알아서 정한다.</summary>
    public static class PRTheme
    {
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Paused = new Color(0.55f, 0.85f, 0.55f, 0.9f);
        public static readonly Color Running = new Color(0.92f, 0.78f, 0.42f, 0.9f);
        public static readonly Color ActiveTurn = new Color(0.95f, 0.88f, 0.60f);
    }

    /// <summary>
    /// 전용 오디오 에셋은 없다. 바닐라 UI 사운드를 빌려 쓰되,
    /// 정의를 못 찾으면 조용히 넘어가 다른 버전·다른 모드 구성에서도 깨지지 않게 한다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PRSounds
    {
        public static readonly SoundDef Win = Lookup("Message_PositiveEvent");
        public static readonly SoundDef Lose = Lookup("Message_NegativeEvent");
        public static readonly SoundDef Reject = Lookup("ClickReject");

        public static SoundDef Lookup(string defName)
        {
            return DefDatabase<SoundDef>.GetNamedSilentFail(defName);
        }

        public static void Play(SoundDef def)
        {
            if (def == null || !PRMod.Settings.sounds) return;
            def.PlayOneShotOnCamera();
        }
    }
}
