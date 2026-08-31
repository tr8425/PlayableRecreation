using System;

namespace Billiards.Core
{
    /// <summary>
    /// 시뮬레이터가 쓰는 2차원 벡터. UnityEngine.Vector2 를 쓰지 않는 이유는 하나다 -
    /// 이 폴더가 Verse 도 Unity 도 모르는 채로 단위 테스트에 올라가야 하기 때문이다.
    /// </summary>
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length
        {
            get { return (float)Math.Sqrt(X * X + Y * Y); }
        }

        public float SquareLength
        {
            get { return X * X + Y * Y; }
        }

        public bool IsZero
        {
            get { return X == 0f && Y == 0f; }
        }

        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length <= 1e-9f ? Zero : new Vec2(X / length, Y / length);
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator *(Vec2 a, float k) { return new Vec2(a.X * k, a.Y * k); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.X, -a.Y); }

        public static float Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }

        public static Vec2 FromAngle(float radians, float length)
        {
            return new Vec2((float)Math.Cos(radians) * length, (float)Math.Sin(radians) * length);
        }

        public float Angle
        {
            get { return (float)Math.Atan2(Y, X); }
        }

        public override string ToString()
        {
            return X.ToString("0.000") + ", " + Y.ToString("0.000");
        }
    }
}
