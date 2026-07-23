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

        /// <summary>Clone the config with one scalar-int property overridden by name (case-insensitive).
        /// init-only setters are callable via reflection at runtime — the compiler restriction is not a runtime one.</summary>
        public static DuelConfig WithParam(DuelConfig baseCfg, string name, int value)
        {
            var prop = typeof(DuelConfig).GetProperty(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop == null) throw new ArgumentException($"unknown config param '{name}'");
            if (prop.PropertyType != typeof(int)) throw new ArgumentException($"param '{name}' is not a scalar int (arrays not sweepable)");
            var clone = baseCfg with { };
            prop.SetValue(clone, value);
            return clone;
        }

        /// <summary>Run one duel and capture a coarse timeline + damage attribution — the diagnostic
        /// that answers "why did the duel end this way" instead of guessing from aggregate stats.</summary>
        public sealed record TraceSnapshot(int Tick, int P0Life, int P1Life, int P0Board, int P1Board, int P0EnergyMilli, int P1EnergyMilli, bool P0Surge, bool P1Surge);
        public sealed record TraceResult(int Winner, int Ticks, IReadOnlyList<TraceSnapshot> Timeline,
            int P0FaceDmgTaken, int P1FaceDmgTaken, int P0MonsterDmgDealt, int P1MonsterDmgDealt, int TotalAttacks);

        public static TraceResult RunTrace(DuelConfig cfg, ContentDb content, IBotPolicy a, IBotPolicy b, ulong seed, int snapshotEvery = 200)
        {
            var decks = new[] { DeckBuilder.Archetype(a.Name, content, cfg.DeckSize), DeckBuilder.Archetype(b.Name, content, cfg.DeckSize) };
            var engine = Engine.DuelEngine.Create(cfg, content, decks, seed);
            var bots = new[] { a, b };
            var seq = new ulong[] { 0, 0 };
            var timeline = new List<TraceSnapshot>();
            int[] faceTaken = { 0, 0 };
            int[] monsterDealt = { 0, 0 };
            int totalAttacks = 0;

            while (engine.Phase != DuelPhase.Ended && engine.CurrentTick < MaxTicks)
            {
                if (engine.CurrentTick % BotDecisionPeriod == 0)
                    for (int p = 0; p < 2; p++)
                    {
                        var pid = new PlayerId((byte)p);
                        foreach (var intent in bots[p].Decide(engine.View(pid), content))
                            engine.Submit(new IntentEnvelope(++seq[p], pid, engine.CurrentTick + 1, intent, new IntentMeta("AI")));
                    }

                foreach (var ev in engine.AdvanceTick())
                {
                    if (ev is Engine.Events.SummonerDamaged sd) faceTaken[sd.Player.Value] += sd.Amount;
                    else if (ev is Engine.Events.AbilityResolved ar)
                    {
                        totalAttacks++;
                        foreach (var h in ar.Hits) monsterDealt[0] += h.Dealt;   // [0] = total creature dmg
                    }
                    else if (ev is Engine.Events.MonsterDied) monsterDealt[1]++;   // [1] = deaths
                }

                if (engine.CurrentTick % snapshotEvery == 0)
                {
                    var v0 = engine.View(PlayerId.P0);
                    var v1 = engine.View(PlayerId.P1);
                    timeline.Add(new TraceSnapshot(engine.CurrentTick, v0.YourLife, v1.YourLife,
                        v0.Mine.Count(), v1.Mine.Count(), v0.YourEnergyMilli, v1.YourEnergyMilli,
                        false, false));
                }
            }
            return new TraceResult(engine.Result?.Winner?.Value ?? -1, engine.CurrentTick, timeline,
                faceTaken[0], faceTaken[1], monsterDealt[0], monsterDealt[1], totalAttacks);
        }

        public sealed record SweepRow(int Value, MatchupStats Stats);

        /// <summary>Vary one config parameter across [start, end] by step; run the matchup at each.</summary>
        public static List<SweepRow> RunSweep(DuelConfig baseCfg, ContentDb content, string param,
            int start, int end, int step, string botA, string botB, int duels, ulong seedBase)
        {
            var rows = new List<SweepRow>();
            for (int v = start; v <= end; v += step)
            {
                var cfg = WithParam(baseCfg, param, v);
                // shared seed base across values → same shuffles, so differences are the parameter, not variance
                rows.Add(new SweepRow(v, RunMatchup(cfg, content, botA, botB, duels, seedBase)));
                if (step == 0) break;
            }
            return rows;
        }
    }
}
