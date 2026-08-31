namespace Ur.Core
{
    /// <summary>4면체 주사위 4개의 결과. 각 주사위는 0 또는 1.</summary>
    public readonly struct UrRoll
    {
        /// <summary>하위 4비트가 각 주사위의 눈(0/1).</summary>
        public readonly byte Faces;

        public UrRoll(int faces)
        {
            Faces = (byte)(faces & 0xF);
        }

        public bool Die(int index)
        {
            return ((Faces >> index) & 1) != 0;
        }

        /// <summary>합계 0~4. 이 값이 이동 칸 수가 된다.</summary>
        public int Total
        {
            get
            {
                return (Faces & 1) + ((Faces >> 1) & 1) + ((Faces >> 2) & 1) + ((Faces >> 3) & 1);
            }
        }

        public override string ToString()
        {
            return Total.ToString();
        }
    }

    /// <summary>
    /// 결정론적 주사위. 눈은 (시드, 순번)의 순수 함수라서 무르기로 순번을 되감으면
    /// 반드시 같은 눈이 다시 나온다. 주사위 세이브스컴을 구조적으로 차단한다(DESIGN.md P4).
    /// </summary>
    public static class UrDice
    {
        public const int DiceCount = 4;

        /// <summary>눈 0~4의 정확한 확률. 이항분포 B(4, 1/2). AI 기댓값 계산에 그대로 쓴다.</summary>
        public static readonly double[] Probability =
        {
            1.0 / 16.0, 4.0 / 16.0, 6.0 / 16.0, 4.0 / 16.0, 1.0 / 16.0
        };

        public static UrRoll Roll(int seed, int sequenceIndex)
        {
            unchecked
            {
                // 두 값을 섞은 뒤 murmur3 finalizer 로 눈사태 효과를 준다.
                uint h = (uint)seed * 2654435761u ^ ((uint)sequenceIndex + 1u) * 2246822519u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;

                // 서로 멀리 떨어진 4개 비트를 뽑아 각 주사위로 쓴다.
                uint b0 = h & 1u;
                uint b1 = (h >> 8) & 1u;
                uint b2 = (h >> 16) & 1u;
                uint b3 = (h >> 24) & 1u;
                return new UrRoll((int)(b0 | (b1 << 1) | (b2 << 2) | (b3 << 3)));
            }
        }
    }

    /// <summary>세션이 들고 다니는 주사위 스트림. 시드만 고정하면 전체 대국이 재현된다.</summary>
    public sealed class UrDiceStream
    {
        public int Seed { get; private set; }

        public UrDiceStream(int seed)
        {
            Seed = seed;
        }

        public UrRoll Roll(int sequenceIndex)
        {
            return UrDice.Roll(Seed, sequenceIndex);
        }
    }
}
