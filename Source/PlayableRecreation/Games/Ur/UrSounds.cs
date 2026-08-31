using PlayableRecreation.UI;
using Verse;

namespace RoyalGameOfUr
{
    /// <summary>
    /// 전용 오디오 에셋은 없다. 바닐라 UI 사운드를 빌려 쓰되,
    /// 정의를 못 찾으면 조용히 넘어가 다른 버전·다른 모드 구성에서도 깨지지 않게 한다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UrSounds
    {
        public static readonly SoundDef Roll = PRSounds.Lookup("Crunch");
        public static readonly SoundDef Move = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Rosette = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Capture = PRSounds.Lookup("Designate_Cancel");
        public static readonly SoundDef BearOff = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Pass = PRSounds.Lookup("ClickReject");
    }
}
