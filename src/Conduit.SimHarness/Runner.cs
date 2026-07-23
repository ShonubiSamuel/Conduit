using System;
using System.Collections.Generic;
using System.Linq;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Events;
using Conduit.Engine.Intents;

namespace Conduit.SimHarness
{
    public sealed record DuelOutcome(int Winner /* 0,1, -1 draw/timeout */, int Ticks, int FirstSummonTick, ulong FinalHash);

    public static class Runner
    {
        public const int MaxTicks = 30_000;          // 25 min — p90 ceiling; beyond = timeout draw
        public const int BotDecisionPeriod = 10;     // bots think every 0.5 s (human-ish pacing)

        public static DuelOutcome RunDuel(DuelConfig cfg, ContentDb content, IBotPolicy a, IBotPolicy b, ulong seed)
        {
            var decks = new[] { DeckBuilder.Archetype(a.Name, content, cfg.DeckSize), DeckBuilder.Archetype(b.Name, content, cfg.DeckSize) };
            var engine = DuelEngine.Create(cfg, content, decks, seed);
            var bots = new[] { a, b };
            var seq = new ulong[] { 0, 0 };
            int firstSummon = -1;

            while (engine.Phase != DuelPhase.Ended && engine.CurrentTick < MaxTicks)
            {
                if (engine.CurrentTick % BotDecisionPeriod == 0)
                {
                    for (int p = 0; p < 2; p++)
                    {
                        var pid = new PlayerId((byte)p);
                        foreach (var intent in bots[p].Decide(engine.View(pid), content))
                            engine.Submit(new IntentEnvelope(++seq[p], pid, engine.CurrentTick + 1, intent, new IntentMeta("AI")));
                    }
                }
                foreach (var ev in engine.AdvanceTick())
                    if (firstSummon < 0 && ev is SummonResolved sr) firstSummon = sr.Tick;
            }

            int winner = engine.Result?.Winner?.Value ?? -1;
            return new DuelOutcome(winner, engine.CurrentTick, firstSummon, engine.StateHash());
        }

        public sealed record MatchupStats(
            string BotA, string BotB, int Duels,
            double WinrateA, double WinrateB, double Draws,
            double P10Min, double P50Min, double P90Min, double AvgFirstSummonSec);

        public static MatchupStats RunMatchup(DuelConfig cfg, ContentDb content, string botA, string botB, int duels, ulong seedBase)
        {
            var results = new List<DuelOutcome>(duels);
            for (int i = 0; i < duels; i++)
                results.Add(RunDuel(cfg, content, Bots.ByName(botA), Bots.ByName(botB), seedBase + (ulong)i));

            var ticks = results.Select(r => r.Ticks).OrderBy(x => x).ToList();
            double Pct(double p) => ticks[Math.Min(ticks.Count - 1, (int)(p * ticks.Count))] / (double)cfg.TicksPerMinute;
            return new MatchupStats(botA, botB, duels,
                results.Count(r => r.Winner == 0) / (double)duels,
                results.Count(r => r.Winner == 1) / (double)duels,
                results.Count(r => r.Winner == -1) / (double)duels,
                Pct(0.10), Pct(0.50), Pct(0.90),
                results.Where(r => r.FirstSummonTick >= 0).DefaultIfEmpty(new DuelOutcome(0, 0, 0, 0))
                       .Average(r => r.FirstSummonTick) * 60.0 / cfg.TicksPerMinute);
        }
    }
}
