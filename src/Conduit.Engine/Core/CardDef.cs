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

    /// <summary>Immutable content. Cost/Atk/Hp are the LIVE (server-balanced) values at duel creation.</summary>
    public sealed record CardDef(
        CardDefId Id,
        Aspect Aspect,
        string Clan,
        Rarity Rarity,
        CardKind Kind,
        int CostMilliEnergy,
        int Atk,
        int Hp,
        IReadOnlyList<KeywordSpec> Keywords,
        IReadOnlyList<EffectOp> Effects,
        SpellSchool School = SpellSchool.Offense,
        CastTierMask AllowedTiers = CastTierMask.All)
    {
        public bool Has(Keyword k) => Keywords.Any(s => s.K == k);
        public KeywordSpec? Get(Keyword k) => Keywords.FirstOrDefault(s => s.K == k);
        public int KeywordX(Keyword k) => Get(k)?.X ?? 0;
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
