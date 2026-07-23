using System.Collections.Generic;
using Conduit.Engine.Core;
using Conduit.Engine.Intents;
using Conduit.Engine.Spatial;
using Conduit.Engine.State;

namespace Conduit.Engine.Ai
{
    /// <summary>
    /// The generic creature combat AI (combat-spec §7). Pure and deterministic (D12): a priority
    /// function over the creature's directive, ability readiness, and DERIVED features (range bands,
    /// nearby enemies / threat density, flanking, facing) — never raw coordinates as game logic,
    /// never randomness. Content-agnostic: it knows nothing about specific creatures or abilities.
    /// </summary>
    public enum AiKind { Idle, MoveTo, Commit }

    public readonly struct AiDecision
    {
        public readonly AiKind Kind;
        public readonly FxVec2 Goal;        // MoveTo
        public readonly int AbilityIndex;   // Commit
        public readonly TargetRef Target;   // Commit (and the intent behind a MoveTo)
        private AiDecision(AiKind k, FxVec2 g, int a, TargetRef t) { Kind = k; Goal = g; AbilityIndex = a; Target = t; }
        public static AiDecision Idle => new AiDecision(AiKind.Idle, FxVec2.Zero, -1, TargetRef.None);
        public static AiDecision Move(FxVec2 goal) => new AiDecision(AiKind.MoveTo, goal, -1, TargetRef.None);
        public static AiDecision Commit(int ability, TargetRef t) => new AiDecision(AiKind.Commit, FxVec2.Zero, ability, t);
    }

    public readonly struct AiWorld
    {
        public readonly DuelConfig Cfg;
        public readonly ContentDb Content;
        public readonly int Tick;
        public readonly IReadOnlyList<MonsterInstance> Field;
        public readonly SpatialGrid Grid;
        public readonly FxVec2 Summoner0, Summoner1;
        public AiWorld(DuelConfig cfg, ContentDb content, int tick, IReadOnlyList<MonsterInstance> field, SpatialGrid grid, FxVec2 s0, FxVec2 s1)
        { Cfg = cfg; Content = content; Tick = tick; Field = field; Grid = grid; Summoner0 = s0; Summoner1 = s1; }
        public FxVec2 SummonerPos(PlayerId p) => p.Value == 0 ? Summoner0 : Summoner1;
    }

    public static class CreatureAi
    {
        public static AiDecision Decide(AiWorld w, MonsterInstance c)
        {
            var def = w.Content.Get(c.Def);
            if (def.Abilities.Count == 0) return AiDecision.Idle;

            // 1) Resolve the primary target POINT + optional target creature from the directive.
            var (targetPoint, targetRef, hasTarget) = ResolveTarget(w, c, def);
            if (!hasTarget) return AiDecision.Idle;

            // 2) Best READY, in-range ability by expected value.
            int bestAbility = -1, bestScore = -1, bestRangeAbility = -1, bestRange = -1;
            for (int i = 0; i < def.Abilities.Count; i++)
            {
                var ab = def.Abilities[i];
                if (ab.RangeMaxMu > bestRange) { bestRange = ab.RangeMaxMu; bestRangeAbility = i; }
                if (w.Tick < CooldownOf(c, i)) continue;
                long distSq = c.Pos.DistSq(targetPoint);
                if (distSq > (long)ab.RangeMaxMu * ab.RangeMaxMu) continue;   // out of range → not eligible now
                if (distSq < (long)ab.RangeMinMu * ab.RangeMinMu) continue;   // too close for this ability
                int score = ScoreAbility(w, c, ab, targetPoint, targetRef);
                if (score > bestScore) { bestScore = score; bestAbility = i; }
            }
            if (bestAbility >= 0) return AiDecision.Commit(bestAbility, targetRef);

            // 3) Nothing ready in range → move to enter the longest-range ability's band.
            var chosen = def.Abilities[bestRangeAbility < 0 ? 0 : bestRangeAbility];
            var goal = GoalIntoBand(c.Pos, targetPoint, chosen.RangeMinMu, chosen.RangeMaxMu);
            return AiDecision.Move(goal);
        }

        // ── target resolution per directive (combat-spec §7 step 1 / 5) ──
        private static (FxVec2 point, TargetRef tref, bool ok) ResolveTarget(AiWorld w, MonsterInstance c, CardDef def)
        {
            var d = c.Directive;
            switch (d.Kind)
            {
                case DirectiveKind.Attack:
                    if (d.Target.Kind == TargetKind.Monster)
                    {
                        var m = Find(w.Field, d.Target.Monster);
                        if (m != null) return (m.Pos, TargetRef.OfMonster(m.Id), true);
                    }
                    if (d.Target.Kind == TargetKind.Summoner)
                        return (w.SummonerPos(c.Owner.Opponent), TargetRef.OfSummoner(c.Owner.Opponent), true);
                    // AttackAuto: nearest enemy creature, else the enemy summoner
                    var near = NearestEnemyCreature(w, c);
                    if (near != null) return (near.Pos, TargetRef.OfMonster(near.Id), true);
                    return (w.SummonerPos(c.Owner.Opponent), TargetRef.OfSummoner(c.Owner.Opponent), true);

                case DirectiveKind.Defend:
                {
                    // protect the ward; engage the biggest threat to it (nearest enemy to the ward point)
                    FxVec2 wardPt = d.Target.Kind == TargetKind.Monster
                        ? (Find(w.Field, d.Target.Monster)?.Pos ?? w.SummonerPos(c.Owner))
                        : w.SummonerPos(c.Owner);
                    var threat = NearestEnemyToPoint(w, c, wardPt);
                    if (threat != null) return (threat.Pos, TargetRef.OfMonster(threat.Id), true);
                    // no threat: interpose toward enemy side (idle-guard near the ward)
                    return (wardPt, TargetRef.None, true);
                }

                case DirectiveKind.Hold:
                {
                    var enemy = NearestEnemyCreature(w, c);
                    if (enemy != null) return (enemy.Pos, TargetRef.OfMonster(enemy.Id), true);
                    return (ZoneCenter(w, c.Owner, d.Zone), TargetRef.None, true);
                }

                case DirectiveKind.Follow:
                {
                    var enemy = NearestEnemyCreature(w, c);
                    if (enemy != null && c.Pos.DistSq(enemy.Pos) <= (long)def.MaxAbilityRange * def.MaxAbilityRange)
                        return (enemy.Pos, TargetRef.OfMonster(enemy.Id), true);
                    return (w.SummonerPos(c.Owner), TargetRef.None, true);
                }

                case DirectiveKind.Retreat:
                    return (w.SummonerPos(c.Owner), TargetRef.None, true);

                default:
                    return (FxVec2.Zero, TargetRef.None, false);
            }
        }

        // ── ability scoring (combat-spec §7 step 2) ──
        private static int ScoreAbility(AiWorld w, MonsterInstance c, AbilityDef ab, FxVec2 targetPoint, TargetRef tref)
        {
            int targets = EstTargetsHit(w, c, ab, targetPoint);
            int pref = PrefMultiplier(w, ab, tref);
            long raw = (long)ab.Damage * targets * pref * 1000 / (ab.WindupTicks + ab.RecoveryTicks + 1);
            return raw > int.MaxValue ? int.MaxValue : (int)raw;
        }

        private static int EstTargetsHit(AiWorld w, MonsterInstance c, AbilityDef ab, FxVec2 targetPoint)
        {
            if (ab.Shape == AoeShape.Single) return 1;
            // Circle/Cone: count enemy creatures near the impact point (threat density → AoE value)
            int radius = ab.Shape == AoeShape.Circle ? ab.ShapeParam : ab.RangeMaxMu;
            var ids = w.Grid.WithinRadius(targetPoint, radius, id => IsEnemyCreature(w, c, id));
            return ids.Count < 1 ? 1 : ids.Count;
        }

        private static int PrefMultiplier(AiWorld w, AbilityDef ab, TargetRef tref)
        {
            // generic, content-agnostic preferences; numbers are placeholder engine defaults
            if (ab.Pref == TargetPref.Summoner) return tref.Kind == TargetKind.Summoner ? 3 : 1;
            return 1;
        }

        // ── derived-feature helpers (spatial hash) ──
        private static MonsterInstance? NearestEnemyCreature(AiWorld w, MonsterInstance c)
            => NearestEnemyToPoint(w, c, c.Pos);

        private static MonsterInstance? NearestEnemyToPoint(AiWorld w, MonsterInstance c, FxVec2 pt)
        {
            var id = w.Grid.Nearest(pt, x => IsEnemyCreature(w, c, x));
            return id.IsNone ? null : Find(w.Field, id);
        }

        private static bool IsEnemyCreature(AiWorld w, MonsterInstance c, EntityId id)
        {
            var m = Find(w.Field, id);
            return m != null && m.Owner != c.Owner && m.Hp > 0;
        }

        private static MonsterInstance? Find(IReadOnlyList<MonsterInstance> field, EntityId id)
        {
            for (int i = 0; i < field.Count; i++) if (field[i].Id.Value == id.Value) return field[i];
            return null;
        }

        private static int CooldownOf(MonsterInstance c, int ability) =>
            ability < c.CooldownUntil.Length ? c.CooldownUntil[ability] : 0;

        private static FxVec2 GoalIntoBand(FxVec2 from, FxVec2 target, int rangeMin, int rangeMax)
        {
            // aim at the MIDDLE of the ability's range band so arrival lands comfortably in range
            // (aiming at the band edge lets arrive-tolerance park the creature just outside it).
            var delta = target - from;
            int dist = delta.Len();
            if (dist <= rangeMax && dist >= rangeMin) return from;   // already in band; hold
            int desired = (rangeMin + rangeMax) / 2;
            if (desired <= 0) desired = rangeMax / 2;
            int travel = dist - desired;                             // may be negative → back away (too close)
            var step = delta.ScaledTo(travel);
            return from + step;
        }

        private static FxVec2 ZoneCenter(AiWorld w, PlayerId owner, FieldZone zone)
        {
            // owner's half is toward their summoner; zones are coarse bands (generic layout)
            int sign = owner.Value == 0 ? -1 : 1;
            int fwd = sign * (w.Cfg.SummonerOffsetMu / 2);
            int side = w.Cfg.ArenaWidthMu / 4;
            return zone switch
            {
                FieldZone.LeftFlank => new FxVec2(-side, fwd),
                FieldZone.RightFlank => new FxVec2(side, fwd),
                FieldZone.Rear => new FxVec2(0, sign * w.Cfg.SummonerOffsetMu),
                _ => new FxVec2(0, fwd),   // Vanguard
            };
        }
    }
}
