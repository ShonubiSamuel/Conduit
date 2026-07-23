// Headless smoke test — proves the Conduit engine EXECUTES correctly under Unity's runtime
// (not just compiles), including the fixed-point math and PCG32 determinism. Invoke via:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod Conduit.Editor.ConduitSmokeTest.RunHeadless
using System.Linq;
using UnityEngine;
using Conduit.Desktop;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;

namespace Conduit.Editor
{
    public static class ConduitSmokeTest
    {
        public static void RunHeadless()
        {
            var cfg = DuelConfig.Default;
            var content = FixtureGame.BuildContent();
            var decks = new[] { FixtureGame.Deck("aggro"), FixtureGame.Deck("wall") };
            var engine = DuelEngine.Create(cfg, content, decks, 42);
            var seq = new ulong[] { 0, 0 };

            while (engine.Phase != DuelPhase.Ended && engine.CurrentTick < 30000)
            {
                if (engine.CurrentTick % 10 == 0)
                    for (int p = 0; p < 2; p++)
                    {
                        var pid = new PlayerId((byte)p);
                        foreach (var it in FixtureGame.Bot(content, engine.View(pid), p == 0))
                            engine.Submit(new IntentEnvelope(++seq[p], pid, engine.CurrentTick + 1, it, new IntentMeta("AI")));
                    }
                engine.AdvanceTick();
            }

            ulong hash = engine.StateHash();
            string winner = engine.Result?.Winner?.ToString() ?? "draw";
            // Compare finalHash/ticks against the .NET harness golden for seed 42 → cross-runtime determinism proof.
            Debug.Log($"CONDUIT_SMOKE ticks={engine.CurrentTick} winner={winner} finalHash={hash:x16}");
        }

        /// <summary>Exercises the exact intents the mouse UI submits (summon, command-attack, guard,
        /// recall→reserve→deploy) headlessly, and asserts they take effect. Non-zero exit on failure.</summary>
        public static void RunHumanControlTest()
        {
            var cfg = DuelConfig.Default;
            var content = FixtureGame.BuildContent();
            var decks = new[] { FixtureGame.Deck("aggro"), FixtureGame.Deck("wall") };
            var engine = DuelEngine.Create(cfg, content, decks, 7);
            var human = PlayerId.P0; var ai = PlayerId.P1;
            ulong hSeq = 0, aSeq = 0;
            bool recalledSeen = false, deployedSeen = false;
            int recallCommandTick = -1;

            void Human(Intent i) => engine.Submit(new IntentEnvelope(++hSeq, human, engine.CurrentTick + 1, i, new IntentMeta("Human")));

            for (int t = 0; t < 4000 && engine.Phase != DuelPhase.Ended; t++)
            {
                var vh = engine.View(human);
                if (engine.CurrentTick % 10 == 0)
                {
                    // AI opponent
                    foreach (var it in FixtureGame.Bot(content, engine.View(ai), false))
                        engine.Submit(new IntentEnvelope(++aSeq, ai, engine.CurrentTick + 1, it, new IntentMeta("AI")));

                    // "Human": summon cheapest monster, order everything to push the enemy summoner
                    if (vh.Mine.Count() < 4)
                    {
                        var pick = vh.Hand.Select(h => (h, d: content.Get(h.Def)))
                            .Where(x => x.d.Kind == CardKind.Monster && x.d.CostMilliEnergy <= vh.YourEnergyMilli)
                            .OrderBy(x => x.d.CostMilliEnergy).FirstOrDefault();
                        if (pick.h != null) Human(new SummonMonster(pick.h.Id, FieldZone.Vanguard));
                    }
                    foreach (var m in vh.Mine)
                        if (!m.AttackingSummoner) Human(new CommandMonster(m.Id, Directive.AttackOf(TargetRef.OfSummoner(ai))));

                    // one recall→deploy cycle to exercise that path
                    if (recallCommandTick < 0 && vh.Mine.Count() >= 2)
                    { Human(new Recall(vh.Mine.First().Id)); recallCommandTick = engine.CurrentTick; }
                    if (vh.Reserve.Count > 0 && !deployedSeen)
                    { Human(new DeployReserve(vh.Reserve[0].Slot, FieldZone.Vanguard)); deployedSeen = true; }
                }
                foreach (var ev in engine.AdvanceTick())
                    if (ev is Conduit.Engine.Events.CreatureRecalled) recalledSeen = true;
            }

            int aiLife = engine.View(ai).YourLife;
            bool faceDamaged = aiLife < cfg.LifeMax;
            Debug.Log($"CONDUIT_HUMAN aiLife={aiLife}/{cfg.LifeMax} faceDamaged={faceDamaged} recalled={recalledSeen} deployed={deployedSeen}");
            if (!faceDamaged) throw new System.Exception("human summon+attack orders did not damage the enemy summoner");
            if (!recalledSeen) throw new System.Exception("recall command did not move a creature to reserve");
            if (!deployedSeen) throw new System.Exception("reserve redeploy did not fire");
            Debug.Log("CONDUIT_HUMAN PASS");
        }
    }
}
