using UnityEngine;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 판을 한 걸음 진행시키는 키.
    ///
    /// 스페이스가 아니다. 스페이스는 콜로니를 멈추는 키이고, 판을 여는 동안에도 그것은
    /// 플레이어의 것이어야 한다. 여기서 가져다 쓰면 주사위를 굴릴 때마다 콜로니가
    /// 멈췄다 흘렀다 하고, 우리 창이 그 키를 삼키지 못하는 순간 - 상대 차례처럼 -
    /// 곧장 시간이 뒤집힌다. 게임 속도를 건드리지 않는다는 약속이 그렇게 깨진다.
    ///
    /// 엔터는 바닐라에서 창을 확인하는 키일 뿐, 세계에는 아무 일도 하지 않는다.
    /// 창이 <c>closeOnAccept</c> 를 꺼 두었으므로 눌러도 창이 닫히지 않는다.
    /// </summary>
    public static class PRKeys
    {
        /// <summary>이번 이벤트가 진행 키인가. 실제로 쓰는 쪽에서 Use() 를 부른다.</summary>
        public static bool ActionPressed()
        {
            if (Event.current.type != EventType.KeyDown) return false;

            return Event.current.keyCode == KeyCode.Return
                || Event.current.keyCode == KeyCode.KeypadEnter;
        }
    }
}
