using System.IO;
using System.Linq;
using Conduit.Content;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Events;
using Conduit.Engine.Intents;
using Conduit.SimHarness;
using Xunit;

namespace Conduit.Engine.Tests
{
    public class CombatTests
    {
        private static (DuelConfig cfg, ContentDb db) Load()
        {
            var root = TestPaths.RepoRoot();
            return (ContentLoader.LoadConfig(Path.Combine(root, "content", "config", "balance.v0.json")),
                    ContentLoader.LoadCardsDir(Path.Combine(root, "content", "cards")));
        }

        /// <summary>A creature ordered to attack the enemy summoner moves in, commits, and deals face damage.</summary>
        [Fact]
        public void DirectedFaceAttack_DamagesSummoner()
        {
            var (cfg, db) = Load();
            var deck = new DeckList(Enumerable.Repeat(new CardDefId("test.melee"), cfg.DeckSize).ToList());
            var e = DuelEngine.Create(cfg, db, new[] { deck, deck }, 1);

            var card = e.View(PlayerId.P0).Hand[0];
            e.Submit(new IntentEnvelope(1, PlayerId.P0, 1, new SummonMonster(card.Id, FieldZone.Vanguard)));
            e.AdvanceTick();
            var mon = e.View(PlayerId.P0).Mine.First();
            e.Submit(new IntentEnvelope(2, PlayerId.P0, e.CurrentTick + 1,
                new CommandMonster(mon.Id, Directive.AttackOf(TargetRef.OfSummoner(PlayerId.P1)))));

            bool committed = false, faceHit = false;
            for (int i = 0; i < 2000 && e.Phase != DuelPhase.Ended; i++)
                foreach (var ev in e.AdvanceTick())
                {
                    if (ev is AbilityCommitted ac && ac.TargetKind == TargetKind.Summoner) committed = true;
                    if (ev is SummonerDamaged sd && sd.Player == PlayerId.P1) faceHit = true;
                }
            Assert.True(committed, "the creature committed an ability at the summoner (telegraph)");
            Assert.True(faceHit, "the summoner took face damage");
            Assert.True(e.View(PlayerId.P1).OppLife < cfg.LifeMax || e.View(PlayerId.P0).OppLife < cfg.LifeMax);
        }

        /// <summary>Two creatures ordered to attack each other trade damage (emergent mutual damage).</summary>
        [Fact]
        public void OpposedCreatures_TradeDamage()
        {
            var (cfg, db) = Load();
            var deck = new DeckList(Enumerable.Repeat(new CardDefId("test.melee"), cfg.DeckSize).ToList());
            var e = DuelEngine.Create(cfg, db, new[] { deck, deck }, 5);
            var c0 = e.View(PlayerId.P0).Hand[0];
            var c1 = e.View(PlayerId.P1).Hand[0];
            e.Submit(new IntentEnvelope(1, PlayerId.P0, 1, new SummonMonster(c0.Id, FieldZone.Vanguard)));
            e.Submit(new IntentEnvelope(1, PlayerId.P1, 1, new SummonMonster(c1.Id, FieldZone.Vanguard)));
            e.AdvanceTick();
            var m0 = e.View(PlayerId.P0).Mine.First().Id;
            var m1 = e.View(PlayerId.P1).Mine.First().Id;
            e.Submit(new IntentEnvelope(2, PlayerId.P0, e.CurrentTick + 1, new CommandMonster(m0, Directive.AttackOf(TargetRef.OfMonster(m1)))));
            e.Submit(new IntentEnvelope(2, PlayerId.P1, e.CurrentTick + 1, new CommandMonster(m1, Directive.AttackOf(TargetRef.OfMonster(m0)))));

            int deaths = 0;
            for (int i = 0; i < 3000 && e.Phase != DuelPhase.Ended; i++)
                foreach (var ev in e.AdvanceTick())
                    if (ev is MonsterDied) deaths++;
            Assert.True(deaths >= 1, "at least one creature died from the exchange");
        }

        /// <summary>Recall pulls a creature off the field into reserve, where it can be redeployed.</summary>
        [Fact]
        public void Recall_MovesCreatureToReserve_ThenRedeploy()
        {
            var (cfg, db) = Load();
            var deck = new DeckList(Enumerable.Repeat(new CardDefId("test.melee"), cfg.DeckSize).ToList());
            var e = DuelEngine.Create(cfg, db, new[] { deck, deck }, 3);
            var card = e.View(PlayerId.P0).Hand[0];
            e.Submit(new IntentEnvelope(1, PlayerId.P0, 1, new SummonMonster(card.Id, FieldZone.Vanguard)));
            e.AdvanceTick();
            var mon = e.View(PlayerId.P0).Mine.First().Id;
            e.Submit(new IntentEnvelope(2, PlayerId.P0, e.CurrentTick + 1, new Recall(mon)));

            bool recalled = false;
            for (int i = 0; i < 2000 && !recalled; i++)
                foreach (var ev in e.AdvanceTick())
                    if (ev is CreatureRecalled) recalled = true;
            Assert.True(recalled, "creature reached the summoner and entered reserve");
            Assert.Single(e.View(PlayerId.P0).Reserve);
            Assert.Empty(e.View(PlayerId.P0).Mine);

            // redeploy from reserve
            e.Submit(new IntentEnvelope(3, PlayerId.P0, e.CurrentTick + 1, new DeployReserve(0, FieldZone.Vanguard)));
            e.AdvanceTick();
            Assert.Single(e.View(PlayerId.P0).Mine);
            Assert.Empty(e.View(PlayerId.P0).Reserve);
        }
    }
}
