// Headless smoke test — proves the Conduit engine EXECUTES correctly under Unity's runtime
// (not just compiles), including the fixed-point math and PCG32 determinism. Invoke via:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod Conduit.Editor.ConduitSmokeTest.RunHeadless
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
    }
}
