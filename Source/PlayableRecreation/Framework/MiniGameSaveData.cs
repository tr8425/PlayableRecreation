using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 가구 위에 남는 판의 알맹이. 프레임워크는 이걸 열어보지 않고 통째로 세이브에 넣는다.
    /// Scribe_Deep 이 실제 타입을 적어두므로 게임마다 자유롭게 파생하면 된다.
    /// </summary>
    public abstract class MiniGameSaveData : IExposable
    {
        public abstract void ExposeData();
    }
}
