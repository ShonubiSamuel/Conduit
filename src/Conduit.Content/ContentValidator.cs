using System;
using System.Collections.Generic;
using System.Linq;
using Conduit.Engine.Core;

namespace Conduit.Content
{
    public enum DeckRules
    {
        /// <summary>Canonical launch rules: 30 cards, ≤2 copies (legendary 1), ≥20 primary Aspect,
        /// ≤10 from ONE allied Aspect, opposed Aspects never share a deck.</summary>
        Strict,
        /// <summary>M0 proto rules: 30 cards, any copies — the card pool is too small for Strict
        /// until the set grows past ~10 distinct cards per Aspect.</summary>
        Proto,
    }

    public sealed record ValidationIssue(bool IsError, string Message)
    {
        public override string ToString() => (IsError ? "ERROR: " : "warn:  ") + Message;
    }

    public static class ContentValidator
    {
        // The wheel: Ember→Gale→Umbra→Tide→Verdant→Bastion→(Ember). Opposites across it.
        private static readonly (Aspect, Aspect)[] Opposed =
            { (Aspect.Ember, Aspect.Tide), (Aspect.Gale, Aspect.Bastion), (Aspect.Umbra, Aspect.Verdant) };

        /// <summary>Stat-budget check (balance §2): SP = 2C+2, ATK = 2 SP, HP = 1 SP, tolerance ±1
        /// (keywords are priced loosely in v0 — a keyword ≈ 1 SP).</summary>
        public static IEnumerable<ValidationIssue> CheckBudgets(ContentDb db, int toleranceSp = 1)
        {
            foreach (var d in db.All.Where(c => c.Kind == CardKind.Monster && c.Rarity != Rarity.Token))
            {
                int costEnergy = d.CostMilliEnergy / 1000;
                int budget = 2 * costEnergy + 2;
                int spent = d.Atk * 2 + d.Hp + d.Keywords.Count;
                int diff = spent - budget;
                if (Math.Abs(diff) > toleranceSp)
                    yield return new ValidationIssue(false,
                        $"{d.Id}: SP {spent} vs budget {budget} (Δ{diff:+#;-#;0}) — off-curve, confirm intentional");
                if (d.Keywords.Count > (d.Rarity == Rarity.Legendary ? 3 : 2))
                    yield return new ValidationIssue(true, $"{d.Id}: too many keywords for {d.Rarity}");
            }
        }

        public static IEnumerable<ValidationIssue> CheckDeck(ContentDb db, DeckList deck, DeckRules rules, int deckSize = 30)
        {
            if (deck.Cards.Count != deckSize)
                yield return new ValidationIssue(true, $"deck has {deck.Cards.Count} cards, must be exactly {deckSize}");

            var defs = new List<CardDef>();
            foreach (var id in deck.Cards)
            {
                if (!db.TryGet(id, out var def)) { yield return new ValidationIssue(true, $"unknown card {id}"); continue; }
                if (def.Rarity == Rarity.Token) yield return new ValidationIssue(true, $"{id}: tokens are not deck-legal");
                defs.Add(def);
            }
            if (rules == DeckRules.Proto) yield break;

            foreach (var g in defs.GroupBy(d => d.Id))
            {
                int max = g.First().Rarity == Rarity.Legendary ? 1 : 2;
                if (g.Count() > max)
                    yield return new ValidationIssue(true, $"{g.Key}: {g.Count()} copies (max {max})");
            }

            var byAspect = defs.GroupBy(d => d.Aspect).OrderByDescending(g => g.Count()).ToList();
            if (byAspect.Count > 0)
            {
                var primary = byAspect[0];
                if (primary.Count() < 20)
                    yield return new ValidationIssue(true, $"primary Aspect {primary.Key} has {primary.Count()} cards (min 20)");
                if (byAspect.Count > 2)
                    yield return new ValidationIssue(true, "more than two Aspects in deck");
                if (byAspect.Count == 2 && byAspect[1].Count() > 10)
                    yield return new ValidationIssue(true, $"splash Aspect {byAspect[1].Key} has {byAspect[1].Count()} cards (max 10)");
                foreach (var (a, b) in Opposed)
                    if (defs.Any(d => d.Aspect == a) && defs.Any(d => d.Aspect == b))
                        yield return new ValidationIssue(true, $"opposed Aspects {a}/{b} may never share a deck");
            }
        }
    }
}
