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
    default:
        Console.Error.WriteLine($"unknown command '{cmd}' (have: run, check)");
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
