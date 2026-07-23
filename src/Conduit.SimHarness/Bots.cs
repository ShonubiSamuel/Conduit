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

    /// <summary>Control archetype: keep the board FULL of blockers (count &gt; size), all Defending;
    /// heal to out-sustain the aggro clock; Guard when the wall thins. This is a real test of
    /// whether control can stabilize against aggro (the aggro-vs-control equilibrium).</summary>
    public sealed class WallBot : IBotPolicy
    {
        public string Name => "wall";
        public IEnumerable<Intent> Decide(DuelView view, ContentDb content)
        {
            if (view.YourDrawCharges > 0 && view.Hand.Count < 7) yield return new DrawCard();

            // Fill the board with the CHEAPEST wall — blocker COUNT stops more attackers than one big wall.
            if (view.Mine.Count() < 4)
            {
                var wall = view.Hand
                    .Select(h => (h, def: content.Get(h.Def)))
                    .Where(x => x.def.Kind == CardKind.Monster && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                    .OrderBy(x => x.def.CostMilliEnergy).ThenByDescending(x => x.def.Hp).ThenBy(x => x.h.Id.Value)
                    .ToList();
                if (wall.Count > 0)
                    yield return new SummonMonster(wall[0].h.Id, FieldZone.Vanguard);
            }

            // Everything defends — a wall deck wins by outlasting, not attacking.
            foreach (var m in view.Mine)
                if (m.Directive != DirectiveKind.Defend)
                    yield return new CommandMonster(m.Id, Directive.Defend(FieldZone.Vanguard));

            // Sustain: cast the cheapest heal (Sign tier) when hurt and off-cooldown.
            if (view.YourLife < view.OppLife || view.YourLife < 120)
            {
                var heal = view.Hand
                    .Select(h => (h, def: content.Get(h.Def)))
                    .Where(x => x.def.Kind == CardKind.Spell && x.def.School == SpellSchool.Defense
                                && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                    .OrderBy(x => x.def.CostMilliEnergy).ThenBy(x => x.h.Id.Value)
                    .FirstOrDefault();
                if (heal.h != null && !view.YouAreChanneling)
                    yield return new CastSpell(heal.h.Id, CastTier.Sign, TargetRef.None);
            }

            if (view.YourLife < 90 && view.CanGuard) yield return new Guard();
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

    /// <summary>Hand-tuned proto archetype decks (DeckRules.Proto — pool is too small for Strict yet).
    /// A recipe is (cardId, weight) pairs cycled to deckSize, so the mix (e.g. heals in the control deck)
    /// is intentional rather than "one Aspect's monsters cycled".</summary>
    public static class DeckBuilder
    {
        private static readonly Dictionary<string, (string id, int weight)[]> Recipes = new()
        {
            // Aggro: cheap, high-ATK, a few burn spells. Ember.
            ["aggro"] = new[]
            {
                ("ember.cinderling", 3), ("ember.flarewhelp", 3), ("ember.ashrunner", 3),
                ("ember.emberdrake", 2), ("ember.pyreclaw", 2), ("ember.flareburst", 2),
            },
            // Control: cheap Bulwark walls + heals to out-sustain. Bastion.
            ["wall"] = new[]
            {
                ("bastion.stonewall", 4), ("bastion.granitewarden", 3), ("bastion.cragback", 2),
                ("bastion.bulwarktitan", 2), ("bastion.aegis", 4),
            },
        };

        public static DeckList Archetype(string bot, ContentDb content, int deckSize = 30)
        {
            if (!Recipes.TryGetValue(bot, out var recipe))
            {
                // fallback: one Aspect's monsters cycled
                Aspect aspect = bot == "wall" ? Aspect.Bastion : Aspect.Ember;
                var pool = content.All.Where(c => c.Aspect == aspect && c.Kind == CardKind.Monster && c.Rarity != Rarity.Token)
                    .OrderBy(c => c.Id.Value, System.StringComparer.Ordinal).ToList();
                var fb = new List<CardDefId>();
                for (int i = 0; fb.Count < deckSize; i++) fb.Add(pool[i % pool.Count].Id);
                return new DeckList(fb);
            }

            // expand weighted recipe, then cycle to fill exactly deckSize (deterministic order)
            var template = new List<CardDefId>();
            foreach (var (id, weight) in recipe)
                for (int i = 0; i < weight; i++) template.Add(new CardDefId(id));
            var cards = new List<CardDefId>();
            for (int i = 0; cards.Count < deckSize; i++) cards.Add(template[i % template.Count]);
            return new DeckList(cards);
        }
    }
}
