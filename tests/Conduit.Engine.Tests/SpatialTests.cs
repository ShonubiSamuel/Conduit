using System.Collections.Generic;
using System.Linq;
using Conduit.Engine.Core;
using Conduit.Engine.Rng;
using Conduit.Engine.Spatial;
using Xunit;

namespace Conduit.Engine.Tests
{
    public class SpatialTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(4, 2)]
        [InlineData(99, 9)]
        [InlineData(100, 10)]
        [InlineData(1_000_000, 1000)]
        [InlineData(1_000_001, 1000)]
        public void ISqrt_IsFloorOfSqrt(long v, int expected) => Assert.Equal(expected, FxMath.ISqrt(v));

        [Fact]
        public void ISqrt_MatchesReference_OverRange()
        {
            var r = new Pcg32(3);
            for (int i = 0; i < 5000; i++)
            {
                long v = r.NextUInt() % 10_000_000;
                int s = FxMath.ISqrt(v);
                Assert.True((long)s * s <= v && (long)(s + 1) * (s + 1) > v, $"ISqrt({v})={s}");
            }
        }

        [Fact]
        public void ScaledTo_HasApproxRequestedLength()
        {
            var v = new FxVec2(300, 400);   // length 500
            var u = v.ScaledTo(1000);
            int len = u.Len();
            Assert.InRange(len, 995, 1005);
        }

        [Fact]
        public void Grid_WithinRadius_MatchesBruteForce()
        {
            // D11: the grid is an accelerator — results must equal a brute-force scan.
            var r = new Pcg32(11);
            var items = new List<(EntityId id, FxVec2 pos)>();
            for (int i = 1; i <= 200; i++)
                items.Add((new EntityId(i), new FxVec2((int)(r.NextUInt() % 8000) - 4000, (int)(r.NextUInt() % 6000) - 3000)));

            var grid = new SpatialGrid(-4000, -4500, 8000, 9000, 600);
            foreach (var (id, pos) in items.OrderBy(x => x.id.Value)) grid.Add(id, pos);

            for (int q = 0; q < 50; q++)
            {
                var center = new FxVec2((int)(r.NextUInt() % 8000) - 4000, (int)(r.NextUInt() % 6000) - 3000);
                int radius = 300 + (int)(r.NextUInt() % 2000);
                var brute = items.Where(x => x.pos.DistSq(center) <= (long)radius * radius)
                                 .Select(x => x.id.Value).OrderBy(v => v).ToList();
                var viaGrid = grid.WithinRadius(center, radius).Select(id => id.Value).ToList();
                Assert.Equal(brute, viaGrid);
            }
        }

        [Fact]
        public void Grid_Nearest_MatchesBruteForce()
        {
            var r = new Pcg32(21);
            var items = new List<(EntityId id, FxVec2 pos)>();
            for (int i = 1; i <= 120; i++)
                items.Add((new EntityId(i), new FxVec2((int)(r.NextUInt() % 8000) - 4000, (int)(r.NextUInt() % 6000) - 3000)));
            var grid = new SpatialGrid(-4000, -4500, 8000, 9000, 700);
            foreach (var (id, pos) in items) grid.Add(id, pos);

            for (int q = 0; q < 40; q++)
            {
                var center = new FxVec2((int)(r.NextUInt() % 8000) - 4000, (int)(r.NextUInt() % 6000) - 3000);
                long bestSq = long.MaxValue; int best = 0;
                foreach (var (id, pos) in items)
                { long d = pos.DistSq(center); if (d < bestSq || (d == bestSq && id.Value < best)) { bestSq = d; best = id.Value; } }
                Assert.Equal(best, grid.Nearest(center).Value);
            }
        }
    }
}
