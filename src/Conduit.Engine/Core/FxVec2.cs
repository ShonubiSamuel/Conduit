namespace Conduit.Engine.Core
{
    /// <summary>
    /// Fixed-point 2D position/vector in integer milli-units (1000 mu = 1 metre). Sim truth (D8).
    /// No floats anywhere. Distance COMPARISONS use LenSq (long); a real length is taken only for
    /// movement normalisation, via the deterministic IntSqrt (D9).
    /// </summary>
    public readonly struct FxVec2
    {
        public readonly int X, Y;
        public FxVec2(int x, int y) { X = x; Y = y; }

        public static readonly FxVec2 Zero = new FxVec2(0, 0);

        public static FxVec2 operator +(FxVec2 a, FxVec2 b) => new FxVec2(a.X + b.X, a.Y + b.Y);
        public static FxVec2 operator -(FxVec2 a, FxVec2 b) => new FxVec2(a.X - b.X, a.Y - b.Y);
        public static FxVec2 operator *(FxVec2 a, int s) => new FxVec2(a.X * s, a.Y * s);

        /// <summary>Squared length as long — the only distance primitive used for comparisons.</summary>
        public long LenSq() => (long)X * X + (long)Y * Y;

        /// <summary>Squared distance to another point (long).</summary>
        public long DistSq(FxVec2 o) { long dx = X - o.X, dy = Y - o.Y; return dx * dx + dy * dy; }

        /// <summary>Integer dot product (long) — used for facing/flanking sign tests (D10).</summary>
        public long Dot(FxVec2 o) => (long)X * o.X + (long)Y * o.Y;

        /// <summary>Deterministic integer length (floor of the true length).</summary>
        public int Len() => FxMath.ISqrt(LenSq());

        /// <summary>This vector scaled so its length is ~<paramref name="scale"/> mu (integer, truncated).
        /// Zero maps to Zero. Used for facing directions and movement steps.</summary>
        public FxVec2 ScaledTo(int scale)
        {
            long lenSq = LenSq();
            if (lenSq == 0) return Zero;
            int len = FxMath.ISqrt(lenSq);
            if (len == 0) return Zero;
            return new FxVec2((int)((long)X * scale / len), (int)((long)Y * scale / len));
        }

        public bool Equals(FxVec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object? obj) => obj is FxVec2 o && Equals(o);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public static class FxMath
    {
        /// <summary>Deterministic integer square root: floor(sqrt(v)) for v &gt;= 0. Bit-by-bit; exact,
        /// branch-stable, identical on every platform (D9). Not used in distance comparisons.</summary>
        public static int ISqrt(long v)
        {
            if (v <= 0) return 0;
            ulong x = (ulong)v;
            ulong res = 0;
            ulong bit = 1UL << 62;
            while (bit > x) bit >>= 2;
            while (bit != 0)
            {
                if (x >= res + bit) { x -= res + bit; res = (res >> 1) + bit; }
                else res >>= 1;
                bit >>= 2;
            }
            return (int)res;
        }
    }
}
