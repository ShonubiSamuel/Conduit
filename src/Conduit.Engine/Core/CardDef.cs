using System;
using System.Collections.Generic;
using System.Linq;

namespace Conduit.Engine.Core
{
    public sealed record KeywordSpec(Keyword K, int X = 0, string? Ref = null);

    public sealed record EffectOp(
        EffectOpType Type,
        EffectTargetKind Target = EffectTargetKind.FromIntent,
        int Amount = 0,
        int DurationTicks = 0,
        string? Ref = null);

    /// <summary>
    /// A single ability in a creature's kit (combat-spec §6). Generic and data-driven — carries no
    /// creature/element identity, just the timing/geometry/target-preference the engine + AI need.
    /// </summary>
    public sealed record AbilityDef(
        string Id,
        int Damage,
        int RangeMinMu,
        int RangeMaxMu,
        int WindupTicks,
        int CooldownTicks,
        int RecoveryTicks,
        AoeShape Shape = AoeShape.Single,
        int ShapeParam = 0,             // Circle: radius mu; Cone: half-angle as a dot threshold (×1000)
        TargetPref Pref = TargetPref.Any,
        DamageType Type = DamageType.Generic,
        int CostMilli = 0)
    {
        /// <summary>Damage-per-minute equivalent — the balance-hook primitive for kit pricing (spec §9).</summary>
        public int DpsEquivalent(int ticksPerMinute) =>
            Damage * ticksPerMinute / Math.Max(1, WindupTicks + RecoveryTicks + CooldownTicks);
    }

    /// <summary>Immutable content. Cost/Hp are LIVE (server-balanced) values at duel creation. A creature's
    /// offence is its Abilities kit, not a single ATK (combat-model v0.2).</summary>
    public sealed record CardDef(
        CardDefId Id,
        Aspect Aspect,
        string Clan,
        Rarity Rarity,
        CardKind Kind,
        int CostMilliEnergy,
        int Hp,
        IReadOnlyList<AbilityDef> Abilities,
        IReadOnlyList<KeywordSpec> Keywords,
        IReadOnlyList<EffectOp> Effects,
        int MoveSpeedMuPerTick = 60,
        int BodyRadiusMu = 150,
        SpellSchool School = SpellSchool.Offense,
        CastTierMask AllowedTiers = CastTierMask.All)
    {
        public bool Has(Keyword k) => Keywords.Any(s => s.K == k);
        public KeywordSpec? Get(Keyword k) => Keywords.FirstOrDefault(s => s.K == k);
        public int KeywordX(Keyword k) => Get(k)?.X ?? 0;
        public int MaxAbilityRange => Abilities.Count == 0 ? 0 : Abilities.Max(a => a.RangeMaxMu);
    }

    /// <summary>Card lookup for a duel. Engine-side, dependency-free.</summary>
    public sealed class ContentDb
    {
        private readonly Dictionary<CardDefId, CardDef> _defs;
        public ContentDb(IEnumerable<CardDef> defs)
        {
            _defs = new Dictionary<CardDefId, CardDef>();
            foreach (var d in defs) _defs[d.Id] = d;
        }
        public CardDef Get(CardDefId id) => _defs[id];
        public bool TryGet(CardDefId id, out CardDef def) => _defs.TryGetValue(id, out def!);
        public IReadOnlyCollection<CardDef> All => _defs.Values;
    }

    public sealed record DeckList(IReadOnlyList<CardDefId> Cards);
}
