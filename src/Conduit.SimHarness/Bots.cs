using System.Collections.Generic;
using System.Linq;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.State;

namespace Conduit.SimHarness
{
    /// <summary>Deterministic rule-priority policy (spec §10). Consumes DuelView, emits intents. No ML, no randomness.</summary>
    public interface IBotPolicy
    {
        string Name { get; }
        IEnumerable<Intent> Decide(DuelView view, ContentDb content);
    }

    /// <summary>Summon cheapest-first, everything attacks. The face-race archetype.</summary>
    public sealed class AggroBot : IBotPolicy
    {
        public string Name => "aggro";
        public IEnumerable<Intent> Decide(DuelView view, ContentDb content)
        {
            if (view.YourDrawCharges > 0 && view.Hand.Count < 7) yield return new DrawCard();
            var summonable = view.Hand
                .Select(h => (h, def: content.Get(h.Def)))
                .Where(x => x.def.Kind == CardKind.Monster && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                .OrderBy(x => x.def.CostMilliEnergy).ThenBy(x => x.h.Id.Value)
                .ToList();
            if (summonable.Count > 0 && view.Mine.Count() < 4)
                yield return new SummonMonster(summonable[0].h.Id, FieldZone.Vanguard);
            // monsters default to Attack(auto) — no commands needed
        }
    }

    /// <summary>Summon toughest-first; low-attack monsters Defend; Guard when hurt. The turtle archetype.</summary>
    public sealed class WallBot : IBotPolicy
    {
        public string Name => "wall";
        public IEnumerable<Intent> Decide(DuelView view, ContentDb content)
        {
            if (view.YourDrawCharges > 0 && view.Hand.Count < 7) yield return new DrawCard();
            var summonable = view.Hand
                .Select(h => (h, def: content.Get(h.Def)))
                .Where(x => x.def.Kind == CardKind.Monster && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                .OrderByDescending(x => x.def.Hp).ThenBy(x => x.h.Id.Value)
                .ToList();
            if (summonable.Count > 0 && view.Mine.Count() < 4)
                yield return new SummonMonster(summonable[0].h.Id, FieldZone.Vanguard);

            foreach (var m in view.Mine)
                if (m.Atk <= 1 && m.Directive != DirectiveKind.Defend)
                    yield return new CommandMonster(m.Id, Directive.Defend(FieldZone.Vanguard));

            if (view.YourLife < 100 && view.CanGuard)
                yield return new Guard();
        }
    }

    public static class Bots
    {
        public static IBotPolicy ByName(string name) => name switch
        {
            "aggro" => new AggroBot(),
            "wall" => new WallBot(),
            _ => throw new System.ArgumentException($"unknown bot '{name}' (have: aggro, wall)"),
        };
    }

    /// <summary>Proto archetype decks (DeckRules.Proto — pool is too small for Strict until the set grows).</summary>
    public static class DeckBuilder
    {
        public static DeckList Archetype(string bot, ContentDb content, int deckSize = 30)
        {
            Aspect aspect = bot switch { "aggro" => Aspect.Ember, "wall" => Aspect.Bastion, _ => Aspect.Ember };
            var pool = content.All
                .Where(c => c.Aspect == aspect && c.Kind == CardKind.Monster && c.Rarity != Rarity.Token)
                .OrderBy(c => c.Id.Value, System.StringComparer.Ordinal)
                .ToList();
            var cards = new List<CardDefId>();
            for (int i = 0; cards.Count < deckSize; i++) cards.Add(pool[i % pool.Count].Id);
            return new DeckList(cards);
        }
    }
}
