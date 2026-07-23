using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;

namespace Conduit.Engine.Replay
{
    /// <summary>
    /// In-memory replay (spec §8). The engine stays IO-free (R1): serialization to the
    /// .cvr binary format lives in Conduit.Content. Playback: Verify() re-runs the duel
    /// and compares the final state hash — the bit-identical guarantee, enforced.
    /// </summary>
    public sealed record ReplayFile(
        int SchemaVersion,
        ulong Seed,
        IReadOnlyList<DeckList> Decks,
        IReadOnlyList<IntentEnvelope> Intents,
        int FinalTick,
        ulong FinalStateHash)
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>Re-simulate and check the hash. True = bit-identical playback.</summary>
        public bool Verify(DuelConfig cfg, ContentDb content)
        {
            var engine = DuelEngine.Create(cfg, content, new[] { Decks[0], Decks[1] }, Seed);
            int cursor = 0;
            var ordered = new List<IntentEnvelope>(Intents);
            while (engine.CurrentTick < FinalTick && engine.Phase != DuelPhase.Ended)
            {
                int next = engine.CurrentTick + 1;
                while (cursor < ordered.Count && ordered[cursor].SubmittedTick <= next)
                    engine.Submit(ordered[cursor++]);
                engine.AdvanceTick();
            }
            return engine.StateHash() == FinalStateHash;
        }
    }
}
