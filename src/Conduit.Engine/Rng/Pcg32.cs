namespace Conduit.Engine.Rng
{
    /// <summary>
    /// PCG32 (Melissa O'Neill, pcg-random.org, minimal C implementation, Apache-2.0).
    /// In-repo because System.Random's algorithm is not a stable cross-version contract (D3).
    /// Mutable struct on purpose: it is a field of DuelState and part of the state hash.
    /// </summary>
    public struct Pcg32
    {
        private ulong _state;
        private ulong _inc;

        public Pcg32(ulong seed, ulong seq = 54)
        {
            _state = 0;
            _inc = (seq << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniform in [0, boundExclusive). Modulo method: bias is negligible for our bounds (≤ deck size) and it is deterministic, which is what matters.</summary>
        public int NextInt(int boundExclusive) => (int)(NextUInt() % (uint)boundExclusive);

        public (ulong State, ulong Inc) Snapshot() => (_state, _inc);
    }
}
