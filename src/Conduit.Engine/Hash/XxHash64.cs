using System;

namespace Conduit.Engine.Hash
{
    /// <summary>xxHash64 (Yann Collet, BSD-2). Used only for internal state-integrity hashing —
    /// both sides of any comparison run this same implementation, so self-consistency is the contract.</summary>
    public static class XxHash64
    {
        private const ulong P1 = 11400714785074694791UL;
        private const ulong P2 = 14029467366897019727UL;
        private const ulong P3 = 1609587929392839161UL;
        private const ulong P4 = 9650029242287828579UL;
        private const ulong P5 = 2870177450012600261UL;

        public static ulong Hash(ReadOnlySpan<byte> data, ulong seed = 0)
        {
            int len = data.Length;
            int i = 0;
            ulong h;

            if (len >= 32)
            {
                ulong v1 = seed + P1 + P2, v2 = seed + P2, v3 = seed, v4 = seed - P1;
                while (i <= len - 32)
                {
                    v1 = Round(v1, ReadU64(data, i)); i += 8;
                    v2 = Round(v2, ReadU64(data, i)); i += 8;
                    v3 = Round(v3, ReadU64(data, i)); i += 8;
                    v4 = Round(v4, ReadU64(data, i)); i += 8;
                }
                h = Rotl(v1, 1) + Rotl(v2, 7) + Rotl(v3, 12) + Rotl(v4, 18);
                h = MergeRound(h, v1); h = MergeRound(h, v2);
                h = MergeRound(h, v3); h = MergeRound(h, v4);
            }
            else h = seed + P5;

            h += (ulong)len;

            while (i <= len - 8) { h ^= Round(0, ReadU64(data, i)); h = Rotl(h, 27) * P1 + P4; i += 8; }
            if (i <= len - 4) { h ^= ReadU32(data, i) * P1; h = Rotl(h, 23) * P2 + P3; i += 4; }
            while (i < len) { h ^= data[i] * P5; h = Rotl(h, 11) * P1; i++; }

            h ^= h >> 33; h *= P2; h ^= h >> 29; h *= P3; h ^= h >> 32;
            return h;
        }

        private static ulong Round(ulong acc, ulong input) => Rotl(acc + input * P2, 31) * P1;
        private static ulong MergeRound(ulong acc, ulong val) => (acc ^ Round(0, val)) * P1 + P4;
        private static ulong Rotl(ulong x, int r) => (x << r) | (x >> (64 - r));
        private static ulong ReadU64(ReadOnlySpan<byte> d, int i) =>
            d[i] | ((ulong)d[i + 1] << 8) | ((ulong)d[i + 2] << 16) | ((ulong)d[i + 3] << 24) |
            ((ulong)d[i + 4] << 32) | ((ulong)d[i + 5] << 40) | ((ulong)d[i + 6] << 48) | ((ulong)d[i + 7] << 56);
        private static uint ReadU32(ReadOnlySpan<byte> d, int i) =>
            d[i] | ((uint)d[i + 1] << 8) | ((uint)d[i + 2] << 16) | ((uint)d[i + 3] << 24);
    }

    /// <summary>Canonical little-endian byte accumulator for state hashing (spec §8).
    /// Field order is the versioned contract; change it → bump ReplayFile.SchemaVersion.</summary>
    public sealed class HashWriter
    {
        private byte[] _buf = new byte[4096];
        private int _len;

        public void I32(int v) { Ensure(4); _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); _buf[_len++] = (byte)(v >> 16); _buf[_len++] = (byte)(v >> 24); }
        public void U64(ulong v) { for (int b = 0; b < 8; b++) { Ensure(1); _buf[_len++] = (byte)(v >> (8 * b)); } }
        public void B(bool v) { Ensure(1); _buf[_len++] = v ? (byte)1 : (byte)0; }
        public void S(string s) { I32(s.Length); foreach (char c in s) I32(c); }

        public ulong Finish(ulong seed = 0) => XxHash64.Hash(new System.ReadOnlySpan<byte>(_buf, 0, _len), seed);

        private void Ensure(int n) { if (_len + n > _buf.Length) System.Array.Resize(ref _buf, _buf.Length * 2); }
    }
}
