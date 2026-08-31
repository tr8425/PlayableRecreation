using RoyalGameOfUr.Core;

namespace RoyalGameOfUr.AI
{
    /// <summary>
    /// AI 봇. Verse 의존이 없어 단위 테스트와 대량 대전 시뮬레이션이 가능하다.
    /// 난이도 T0~T4 는 이 인터페이스의 서로 다른 구현으로 제공된다. (DESIGN.md §5.1)
    /// </summary>
    public interface IUrAi
    {
        /// <summary>난이도 이름 키(번역 키가 아니라 식별자).</summary>
        string Id { get; }

        /// <summary>
        /// 합법수 중 하나의 인덱스를 고른다. legal 배열은 앞의 legalCount 개만 유효하다.
        /// legalCount 는 항상 1 이상으로 호출된다.
        /// </summary>
        int ChooseMove(in UrGameState state, int roll, UrMove[] legal, int legalCount);
    }
}
