// Shared M2 scaffolding: builds the generic ENGINE FIXTURES (not designed content), decks, and
// the compact bots. Used by both the runtime renderer (DuelBootstrap) and the editor smoke test.
using System.Collections.Generic;
using System.Linq;
using Conduit.Engine;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.State;

namespace Conduit.Desktop
{
    public static class FixtureGame
    {
        public static ContentDb BuildContent()
        {
            AbilityDef A(string id, int dmg, int rmin, int rmax, int wu, int cd, int rec)
                => new AbilityDef(id, dmg, rmin, rmax, wu, cd, rec);
            var noEff = new List<EffectOp>();
            CardDef Mon(string id, Aspect a, Rarity r, int cost, int hp, int spd, AbilityDef ab, params KeywordSpec[] kw)
                => new CardDef(new CardDefId(id), a, "Test", r, CardKind.Monster, cost * 1000, hp,
                               new List<AbilityDef> { ab }, kw.ToList(), noEff, spd);

            return new ContentDb(new List<CardDef>
            {
                Mon("test.melee",      Aspect.Ember,   Rarity.Common, 2, 50, 60, A("strike", 8, 0, 300, 6, 24, 6)),
                Mon("test.skirmisher", Aspect.Gale,    Rarity.Common, 1, 24, 95, A("slash", 4, 0, 260, 4, 14, 4)),
                Mon("test.ranged",     Aspect.Gale,    Rarity.Common, 3, 32, 55, A("shot", 7, 400, 1400, 12, 36, 8)),
                Mon("test.bulwark",    Aspect.Bastion, Rarity.Common, 2, 90, 35, A("bash", 5, 0, 300, 8, 30, 8), new KeywordSpec(Keyword.Bulwark)),
                Mon("test.brute",      Aspect.Bastion, Rarity.Rare,   4, 70, 45, A("smash", 12, 0, 320, 14, 40, 12)),
                new CardDef(new CardDefId("test.mend"), Aspect.Bastion, "Test", Rarity.Common, CardKind.Spell, 2000, 0,
                    new List<AbilityDef>(), new List<KeywordSpec>(),
                    new List<EffectOp> { new EffectOp(EffectOpType.Heal, EffectTargetKind.AllySummoner, 12) },
                    60, 150, SpellSchool.Defense),
            });
        }

        public static DeckList Deck(string kind)
        {
            var recipe = kind == "aggro"
                ? new[] { ("test.skirmisher", 3), ("test.melee", 3), ("test.ranged", 2) }
                : new[] { ("test.bulwark", 4), ("test.brute", 2), ("test.mend", 3) };
            var template = new List<CardDefId>();
            foreach (var (id, wgt) in recipe) for (int i = 0; i < wgt; i++) template.Add(new CardDefId(id));
            var cards = new List<CardDefId>();
            for (int i = 0; cards.Count < 30; i++) cards.Add(template[i % template.Count]);
            return new DeckList(cards);
        }

        public static IEnumerable<Intent> Bot(ContentDb content, DuelView v, bool aggro)
        {
            if (v.YourDrawCharges > 0 && v.Hand.Count < 7) yield return new DrawCard();
            if (v.Mine.Count() < 4)
            {
                if (v.Reserve.Count > 0) yield return new DeployReserve(v.Reserve[0].Slot, FieldZone.Vanguard);
                else
                {
                    var picks = v.Hand.Select(h => (h, d: content.Get(h.Def)))
                        .Where(x => x.d.Kind == CardKind.Monster && x.d.CostMilliEnergy <= v.YourEnergyMilli);
                    var pick = aggro
                        ? picks.OrderBy(x => x.d.CostMilliEnergy).ThenBy(x => x.h.Id.Value).FirstOrDefault()
                        : picks.OrderByDescending(x => x.d.Hp).ThenBy(x => x.h.Id.Value).FirstOrDefault();
                    if (pick.h != null) yield return new SummonMonster(pick.h.Id, FieldZone.Vanguard);
                }
            }
            if (aggro)
            {
                foreach (var m in v.Mine)
                    if (!m.AttackingSummoner)
                        yield return new CommandMonster(m.Id, Directive.AttackOf(TargetRef.OfSummoner(v.You.Opponent)));
            }
            else
            {
                foreach (var m in v.Mine)
                {
                    if (m.MaxHp > 0 && m.Hp * 100 / m.MaxHp <= 20) yield return new Recall(m.Id);
                    else if (m.Directive != DirectiveKind.Defend)
                        yield return new CommandMonster(m.Id, Directive.Defend(TargetRef.OfSummoner(v.You)));
                }
                if (v.YourLife < 90 && v.CanGuard) yield return new Guard();
            }
        }
    }
}
