using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Conduit.Content;
using Conduit.SimHarness;

// conduit-sim — the M0 harness (spec §10).
//   run    --duels N --matchup aggro:wall --seed-base 42 --content <dir> --out results.json
//   check  --content <dir>          content validation (budgets + proto decks)
// Determinism: same seed-base + same build → byte-identical results.json.

string cmd = args.Length > 0 ? args[0] : "run";
string Get(string name, string dflt)
{
    for (int i = 1; i < args.Length - 1; i++) if (args[i] == "--" + name) return args[i + 1];
    return dflt;
}

string contentDir = Get("content", FindContentDir());
var config = ContentLoader.LoadConfig(Path.Combine(contentDir, "config", "balance.v0.json"));
var db = ContentLoader.LoadCardsDir(Path.Combine(contentDir, "cards"));

switch (cmd)
{
    case "check":
    {
        int errors = 0;
        foreach (var issue in ContentValidator.CheckBudgets(db))
        {
            Console.WriteLine(issue);
            if (issue.IsError) errors++;
        }
        foreach (var bot in new[] { "aggro", "wall" })
            foreach (var issue in ContentValidator.CheckDeck(db, DeckBuilder.Archetype(bot, db, config.DeckSize), DeckRules.Proto, config.DeckSize))
            {
                Console.WriteLine($"[{bot} deck] {issue}");
                if (issue.IsError) errors++;
            }
        Console.WriteLine(errors == 0 ? $"OK — {db.All.Count} cards loaded, no errors." : $"{errors} error(s).");
        return errors == 0 ? 0 : 1;
    }
    case "run":
    {
        int duels = int.Parse(Get("duels", "500"));
        var matchup = Get("matchup", "aggro:wall").Split(':');
        ulong seedBase = ulong.Parse(Get("seed-base", "42"));
        string? outPath = Get("out", "");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stats = Runner.RunMatchup(config, db, matchup[0], matchup[1], duels, seedBase);
        sw.Stop();

        Console.WriteLine($"{stats.BotA} vs {stats.BotB} — {stats.Duels} duels ({sw.Elapsed.TotalSeconds:F1}s)");
        Console.WriteLine($"  winrate: {stats.BotA} {stats.WinrateA:P1} | {stats.BotB} {stats.WinrateB:P1} | draws/timeouts {stats.Draws:P1}");
        Console.WriteLine($"  duel length (min): p10 {stats.P10Min:F1} | p50 {stats.P50Min:F1} | p90 {stats.P90Min:F1}   (targets: ≥6 / 12–18 / ≤25)");
        Console.WriteLine($"  avg first summon: {stats.AvgFirstSummonSec:F1}s   (target < 20s)");

        if (!string.IsNullOrEmpty(outPath))
        {
            File.WriteAllText(outPath, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"  wrote {outPath}");
        }
        return 0;
    }
    case "sweep":
    {
        // sweep --param SurgeBp=10000..20000:2000 --duels 2000 --matchup aggro:wall --seed-base 42 [--out sweep.json]
        string spec = Get("param", "SurgeBp=10000..20000:2000");
        int eq = spec.IndexOf('=');
        if (eq < 0) { Console.Error.WriteLine("--param must be NAME=start..end:step"); return 2; }
        string param = spec[..eq];
        var rangePart = spec[(eq + 1)..];
        var dotdot = rangePart.Split("..");
        int start = int.Parse(dotdot[0]);
        var endStep = dotdot[1].Split(':');
        int end = int.Parse(endStep[0]);
        int step = endStep.Length > 1 ? int.Parse(endStep[1]) : 1;

        int duels = int.Parse(Get("duels", "1000"));
        var matchup = Get("matchup", "aggro:wall").Split(':');
        ulong seedBase = ulong.Parse(Get("seed-base", "42"));
        string? outPath = Get("out", "");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Runner.RunSweep(config, db, param, start, end, step, matchup[0], matchup[1], duels, seedBase);
        sw.Stop();

        Console.WriteLine($"sweep {param} = {start}..{end}:{step} — {matchup[0]} vs {matchup[1]}, {duels} duels each ({sw.Elapsed.TotalSeconds:F1}s)");
        Console.WriteLine($"  targets: p50 duel 12–18 min, p10 ≥6, p90 ≤25; winrate within 48–52% for a fair matchup");
        Console.WriteLine($"  {param,-10} {"A_win",7} {"B_win",7} {"draw",6} {"p10",6} {"p50",6} {"p90",6}");
        foreach (var r in rows)
            Console.WriteLine($"  {r.Value,-10} {r.Stats.WinrateA,7:P0} {r.Stats.WinrateB,7:P0} {r.Stats.Draws,6:P0} {r.Stats.P10Min,6:F1} {r.Stats.P50Min,6:F1} {r.Stats.P90Min,6:F1}");

        if (!string.IsNullOrEmpty(outPath))
        {
            File.WriteAllText(outPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"  wrote {outPath}");
        }
        return 0;
    }
    case "trace":
    {
        var matchup = Get("matchup", "aggro:wall").Split(':');
        ulong seed = ulong.Parse(Get("seed", "42"));
        var tr = Runner.RunTrace(config, db, Bots.ByName(matchup[0]), Bots.ByName(matchup[1]), seed);
        Console.WriteLine($"trace {matchup[0]} vs {matchup[1]} seed {seed} → winner P{tr.Winner}, {tr.Ticks} ticks ({tr.Ticks / (double)config.TicksPerMinute:F1} min)");
        Console.WriteLine($"  face damage taken: P0 {tr.P0FaceDmgTaken} | P1 {tr.P1FaceDmgTaken}   (of {config.LifeMax} life)   total attacks resolved: {tr.TotalAttacks}");
        Console.WriteLine($"  {"tick",6} {"P0life",6} {"P1life",6} {"P0brd",5} {"P1brd",5} {"P0e",6} {"P1e",6}");
        foreach (var s in tr.Timeline)
            Console.WriteLine($"  {s.Tick,6} {s.P0Life,6} {s.P1Life,6} {s.P0Board,5} {s.P1Board,5} {s.P0EnergyMilli,6} {s.P1EnergyMilli,6}");
        return 0;
    }
    default:
        Console.Error.WriteLine($"unknown command '{cmd}' (have: run, check, sweep, trace)");
        return 2;
}

static string FindContentDir()
{
    // walk up from cwd looking for content/config/balance.v0.json (repo-relative convenience)
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "content");
        if (File.Exists(Path.Combine(candidate, "config", "balance.v0.json"))) return candidate;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException("could not locate content/ — pass --content <dir>");
}
