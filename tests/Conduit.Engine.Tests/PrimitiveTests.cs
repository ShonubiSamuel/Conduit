using System;
using System.Text;
using Conduit.Engine.Hash;
using Conduit.Engine.Rng;
using Xunit;

namespace Conduit.Engine.Tests
{
    public class PrimitiveTests
    {
        [Fact]
        public void Pcg32_SameSeed_SameSequence()
        {
            var a = new Pcg32(12345);
            var b = new Pcg32(12345);
            for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void Pcg32_DifferentSeeds_Diverge()
        {
            var a = new Pcg32(1);
            var b = new Pcg32(2);
            bool anyDiff = false;
            for (int i = 0; i < 16; i++) if (a.NextUInt() != b.NextUInt()) { anyDiff = true; break; }
            Assert.True(anyDiff);
        }

        [Fact]
        public void Pcg32_BoundedDraw_InRange()
        {
            var r = new Pcg32(99);
            for (int i = 0; i < 10_000; i++)
            {
                int v = r.NextInt(30);
                Assert.InRange(v, 0, 29);
            }
        }

        [Fact]
        public void XxHash64_IsDeterministic_AndSensitive()
        {
            var d1 = Encoding.UTF8.GetBytes("conduit state serialization test vector 0123456789");
            var d2 = Encoding.UTF8.GetBytes("conduit state serialization test vector 0123456780");
            Assert.Equal(XxHash64.Hash(d1), XxHash64.Hash(d1));
            Assert.NotEqual(XxHash64.Hash(d1), XxHash64.Hash(d2));
            Assert.NotEqual(XxHash64.Hash(d1, 0), XxHash64.Hash(d1, 1));
        }

        [Fact]
        public void XxHash64_AllLengthClasses_Stable()
        {
            // exercise the <4, <8, <32, ≥32 byte paths
            var rnd = new Pcg32(7);
            for (int len = 0; len < 100; len++)
            {
                var buf = new byte[len];
                for (int i = 0; i < len; i++) buf[i] = (byte)rnd.NextUInt();
                Assert.Equal(XxHash64.Hash(buf), XxHash64.Hash(buf));
            }
        }

        [Fact]
        public void HashWriter_FieldOrderMatters()
        {
            var w1 = new HashWriter(); w1.I32(1); w1.I32(2);
            var w2 = new HashWriter(); w2.I32(2); w2.I32(1);
            Assert.NotEqual(w1.Finish(), w2.Finish());
        }
    }
}
