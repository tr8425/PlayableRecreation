using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Punching
{
    /// <summary>펀칭백의 색. 가죽과 밧줄, 그리고 판정의 세 가지 온도.</summary>
    public static class PunchingTheme
    {
        public static readonly Color Bag = new Color(0.55f, 0.36f, 0.26f);
        public static readonly Color BagSeam = new Color(0.38f, 0.25f, 0.18f);
        public static readonly Color Rope = new Color(0.62f, 0.56f, 0.44f);

        public static readonly Color Perfect = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color Good = new Color(0.58f, 0.68f, 0.80f);
        public static readonly Color Miss = new Color(0.72f, 0.35f, 0.30f);

        public static readonly Color TapeBack = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color TapeLine = new Color(0.95f, 0.88f, 0.60f);
        public static readonly Color TapeCue = new Color(1f, 1f, 1f, 0.22f);
        public static readonly Color Note = new Color(0.84f, 0.78f, 0.66f);
        public static readonly Color NoteKo = new Color(0.93f, 0.75f, 0.38f);
    }

    /// <summary>
    /// 바닐라 소리를 빌려 쓴다. 진짜 주먹 소리(Pawn_Melee_Punch_*)는 월드 사운드라
    /// 카메라 재생용 서브사운드가 없다 - 빨간 로그만 남기고 소리는 안 난다.
    /// 그래서 다른 게임들이 검증한 UI 사운드로만 고른다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PunchingSounds
    {
        public static readonly SoundDef Cue = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Hit = PRSounds.Lookup("Crunch");
        public static readonly SoundDef Whiff = PRSounds.Lookup("ClickReject");
        public static readonly SoundDef Ko = PRSounds.Lookup("TinyBell");
    }
}
