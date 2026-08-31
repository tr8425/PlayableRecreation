namespace Billiards.Core
{
    /// <summary>
    /// 당구대의 치수와 물리 상수. 전부 무차원 단위다 - 대는 가로 2, 세로 1.
    /// 화면 크기와 무관하게 같은 샷이 같은 결과를 내야 하므로 픽셀은 여기 들어오지 않는다.
    /// </summary>
    public static class PoolTable
    {
        public const float Width = 2f;
        public const float Height = 1f;

        public const float BallRadius = 0.028f;
        public const float PocketRadius = 0.055f;

        /// <summary>구르는 마찰. 단위/초^2 로 속도를 깎는다.</summary>
        public const float Friction = 0.52f;

        /// <summary>이보다 느려지면 멈춘 것으로 친다.</summary>
        public const float RestSpeed = 0.012f;

        /// <summary>쿠션 반발. 1이면 완전탄성.</summary>
        public const float CushionRestitution = 0.90f;

        /// <summary>공끼리의 반발.</summary>
        public const float BallRestitution = 0.96f;

        /// <summary>플레이어가 낼 수 있는 최대 초속.</summary>
        public const float MaxShotSpeed = 3.2f;

        /// <summary>시뮬레이션 고정 스텝. 결과가 프레임률에 흔들리지 않게 한다.</summary>
        public const float Step = 1f / 480f;

        /// <summary>이 시간이 지나도 안 멈추면 강제로 세운다. 무한 루프 방지.</summary>
        public const float MaxShotSeconds = 25f;

        public static readonly Vec2[] Pockets =
        {
            new Vec2(0f, 0f),
            new Vec2(Width * 0.5f, 0f),
            new Vec2(Width, 0f),
            new Vec2(0f, Height),
            new Vec2(Width * 0.5f, Height),
            new Vec2(Width, Height),
        };

        /// <summary>큐볼이 서는 자리. 파울 뒤에도 여기로 돌아온다.</summary>
        public static readonly Vec2 HeadSpot = new Vec2(Width * 0.25f, Height * 0.5f);

        /// <summary>랙의 앞머리. 9번이 다시 놓일 때도 여기를 기준으로 한다.</summary>
        public static readonly Vec2 FootSpot = new Vec2(Width * 0.72f, Height * 0.5f);
    }
}
