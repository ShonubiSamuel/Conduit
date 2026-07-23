using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conduit.Engine.Core;

namespace Conduit.Content
{
    // ── JSON DTOs (content/cards/*.json — a file may hold one card object or an array) ──

    public sealed class KeywordJson { public string k { get; set; } = ""; public int x { get; set; } public string? @ref { get; set; } }
    public sealed class EffectJson
    {
        public string op { get; set; } = "";
        public string target { get; set; } = "FromIntent";
        public int amount { get; set; }
        public int durationTicks { get; set; }
        public string? @ref { get; set; }
    }
    public sealed class CardJson
    {
        public string id { get; set; } = "";
        public string aspect { get; set; } = "";
        public string clan { get; set; } = "";
        public string rarity { get; set; } = "Common";
        public string kind { get; set; } = "Monster";
        public int cost { get; set; }                  // whole energy; ×1000 at load
        public int atk { get; set; }
        public int hp { get; set; }
        public List<KeywordJson> keywords { get; set; } = new();
        public List<EffectJson> effects { get; set; } = new();
        public string school { get; set; } = "Offense";
        public List<string>? tiers { get; set; }       // null = All
    }

    public static class ContentLoader
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static ContentDb LoadCardsDir(string dir)
        {
            var defs = new List<CardDef>();
            foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var el in doc.RootElement.EnumerateArray())
                        defs.Add(ToDef(el.Deserialize<CardJson>(Opts)!));
                else
                    defs.Add(ToDef(doc.RootElement.Deserialize<CardJson>(Opts)!));
            }
            return new ContentDb(defs);
        }

        private static CardDef ToDef(CardJson j)
        {
            var keywords = j.keywords.Select(k => new KeywordSpec(
                (Keyword)Enum.Parse(typeof(Keyword), k.k, ignoreCase: true), k.x, k.@ref)).ToList();
            var effects = j.effects.Select(e => new EffectOp(
                (EffectOpType)Enum.Parse(typeof(EffectOpType), e.op, ignoreCase: true),
                (EffectTargetKind)Enum.Parse(typeof(EffectTargetKind), e.target, ignoreCase: true),
                e.amount, e.durationTicks, e.@ref)).ToList();
            var tiers = CastTierMask.All;
            if (j.tiers != null)
            {
                tiers = CastTierMask.None;
                foreach (var tname in j.tiers)
                    tiers |= (CastTierMask)(1 << (int)(CastTier)Enum.Parse(typeof(CastTier), tname, true));
            }
            return new CardDef(
                new CardDefId(j.id),
                (Aspect)Enum.Parse(typeof(Aspect), j.aspect, true),
                j.clan,
                (Rarity)Enum.Parse(typeof(Rarity), j.rarity, true),
                (CardKind)Enum.Parse(typeof(CardKind), j.kind, true),
                j.cost * 1000, j.atk, j.hp,
                keywords, effects,
                (SpellSchool)Enum.Parse(typeof(SpellSchool), j.school, true),
                tiers);
        }

        // ── balance.v0.json → DuelConfig ──

        public static DuelConfig LoadConfig(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DuelConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? DuelConfig.Default;
        }
    }
}
