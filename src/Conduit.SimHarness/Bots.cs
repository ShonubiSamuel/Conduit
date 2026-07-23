using System.Collections.Generic;
using System.Linq;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.State;

namespace Conduit.SimHarness
{
    /// <summary>Deterministic rule-priority policy (spec §10). Consumes DuelView, emits directives/intents.
    /// No ML, no randomness. Bots command via high-level directives — the AI handles tactics (combat-model §2).</summary>
    public interface IBotPolicy
    {
        string Name { get; }
        IEnumerable<Intent> Decide(DuelView view, ContentDb content);
    }

    /// <summary>Pressure: summon the cheapest creature, order everything to attack, redeploy reserves.</summary>
    public sealed class AggroBot : IBotPolicy
    {
        public string Name => "aggro";
        public IEnumerable<Intent> Decide(DuelView view, ContentDb content)
        {
            if (view.YourDrawCharges > 0 && view.Hand.Count < 7) yield return new DrawCard();

            if (view.Mine.Count() < 4)
            {
                if (view.Reserve.Count > 0)
                    yield return new DeployReserve(view.Reserve[0].Slot, FieldZone.Vanguard);
                else
                {
                    var pick = view.Hand
                        .Select(h => (h, def: content.Get(h.Def)))
                        .Where(x => x.def.Kind == CardKind.Monster && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                        .OrderBy(x => x.def.CostMilliEnergy).ThenBy(x => x.h.Id.Value)
                        .FirstOrDefault();
                    if (pick.h != null) yield return new SummonMonster(pick.h.Id, FieldZone.Vanguard);
                }
            }

            // push the enemy summoner (a directed face attack; defense must kill the pushers to survive).
            // NOTE: how defense gates a push (overflow/lane-blocking) is deferred game design — the engine
            // just supports directive-driven targeting generically.
            var pushOrder = Directive.AttackOf(TargetRef.OfSummoner(view.You.Opponent));
            foreach (var m in view.Mine)
                if (!m.AttackingSummoner)
                    yield return new CommandMonster(m.Id, pushOrder);
        }
    }

    /// <summary>Control: field durable creatures on Defend to protect the summoner, heal to out-sustain,
    /// Recall a creature about to die, redeploy healed reserves, Guard when the wall thins.</summary>
    public sealed class WallBot : IBotPolicy
    {
        public string Name => "wall";
        public IEnumerable<Intent> Decide(DuelView view, ContentDb content)
        {
            if (view.YourDrawCharges > 0 && view.Hand.Count < 7) yield return new DrawCard();

            if (view.Mine.Count() < 4)
            {
                if (view.Reserve.Count > 0)
                    yield return new DeployReserve(view.Reserve[0].Slot, FieldZone.Vanguard);
                else
                {
                    var pick = view.Hand
                        .Select(h => (h, def: content.Get(h.Def)))
                        .Where(x => x.def.Kind == CardKind.Monster && x.def.CostMilliEnergy <= view.YourEnergyMilli)
                        .OrderByDescending(x => x.def.Hp).ThenBy(x => x.h.Id.Value)
                        .FirstOrDefault();
                    if (pick.h != null) yield return new SummonMonster(pick.h.Id, FieldZone.Vanguard);
                }
            }

            // everything guards the summoner; recall the most-wounded creature before it dies
            foreach (var m in view.Mine)
            {
                if (m.MaxHp > 0 && m.Hp * 100 / m.MaxHp <= 20)
                    yield return new Recall(m.Id);
                else if (m.Directive != DirectiveKind.Defend)
                    yield return new CommandMonster(m.Id, Directive.Defend(TargetRef.OfSummoner(view.You)));
            }

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

    /// <summary>Generic archetype decks built from the ENGINE TEST FIXTURES (not designed content).</summary>
    public static class DeckBuilder
    {
        private static readonly Dictionary<string, (string id, int weight)[]> Recipes = new()
        {
            ["aggro"] = new[] { ("test.skirmisher", 3), ("test.melee", 3), ("test.ranged", 2) },
            ["wall"] = new[] { ("test.bulwark", 4), ("test.brute", 2), ("test.mend", 3) },
        };

        public static DeckList Archetype(string bot, ContentDb content, int deckSize = 30)
        {
            var recipe = Recipes.TryGetValue(bot, out var r) ? r : Recipes["aggro"];
            var template = new List<CardDefId>();
            foreach (var (id, weight) in recipe)
                for (int i = 0; i < weight; i++) template.Add(new CardDefId(id));
            var cards = new List<CardDefId>();
            for (int i = 0; cards.Count < deckSize; i++) cards.Add(template[i % template.Count]);
            return new DeckList(cards);
        }
    }
}
