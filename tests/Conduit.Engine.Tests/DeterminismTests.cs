using System.IO;
using Conduit.Content;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.SimHarness;
using Xunit;

namespace Conduit.Engine.Tests
{
    /// <summary>D7: determinism is verified, not assumed.</summary>
    public class DeterminismTests
    {
        private static (DuelConfig cfg, ContentDb db) Load()
        {
            var root = TestPaths.RepoRoot();
            return (ContentLoader.LoadConfig(Path.Combine(root, "content", "config", "balance.v0.json")),
                    ContentLoader.LoadCardsDir(Path.Combine(root, "content", "cards")));
        }

        [Theory]
        [InlineData(1UL)]
        [InlineData(42UL)]
        [InlineData(987654321UL)]
        public void SameSeed_SameDuel_BitIdentical(ulong seed)
        {
            var (cfg, db) = Load();
            var r1 = Runner.RunDuel(cfg, db, new AggroBot(), new WallBot(), seed);
            var r2 = Runner.RunDuel(cfg, db, new AggroBot(), new WallBot(), seed);
            Assert.Equal(r1.FinalHash, r2.FinalHash);
            Assert.Equal(r1.Ticks, r2.Ticks);
            Assert.Equal(r1.Winner, r2.Winner);
        }

        [Fact]
        public void DifferentSeeds_DifferentShuffles()
        {
            var (cfg, db) = Load();
            var r1 = Runner.RunDuel(cfg, db, new AggroBot(), new AggroBot(), 7);
            var r2 = Runner.RunDuel(cfg, db, new AggroBot(), new AggroBot(), 8);
            Assert.NotEqual(r1.FinalHash, r2.FinalHash);
        }

        [Fact]
        public void ManySeeds_AllStable()
        {
            var (cfg, db) = Load();
            for (ulong s = 100; s < 120; s++)
            {
                var r1 = Runner.RunDuel(cfg, db, new AggroBot(), new WallBot(), s);
                var r2 = Runner.RunDuel(cfg, db, new AggroBot(), new WallBot(), s);
                Assert.Equal(r1.FinalHash, r2.FinalHash);
            }
        }

        [Fact]
        public void Replay_Verifies_BitIdentical()
        {
            var (cfg, db) = Load();
            var decks = new[] { DeckBuilder.Archetype("aggro", db, cfg.DeckSize), DeckBuilder.Archetype("wall", db, cfg.DeckSize) };
            var engine = DuelEngine.Create(cfg, db, decks, 42);
            // scripted short duel: let it run 2000 ticks with no intents (regen/draw-charge timers only)
            engine.AdvanceTo(2000);
            var replay = engine.ExportReplay(decks);
            Assert.True(replay.Verify(cfg, db));
        }
    }

    public static class TestPaths
    {
        public static string RepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conduit.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
        }
    }
}
