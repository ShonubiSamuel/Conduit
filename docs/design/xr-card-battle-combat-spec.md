# Combat Engine Specification — Ability-Driven Continuous Combat (DRAFT)

*The implementation spec for `combat-model.md` v0.2. Replaces §6 (phases 5–6, the instant volley) of `xr-card-battle-m0-m1-technical-spec.md`. Everything else in that spec — the 20 Hz tick, determinism contract, Intent/Event envelope, energy economy, replay, hashing, view projection — is unchanged and this builds on it. **Status: DRAFT — awaiting approval.***

*Decisions locked with the user (July 23 2026): (A) continuous **fixed-point positions** as sim truth + spatial hash for queries; AI reasons over **derived features** (range bands, nearby enemies, flanking, facing, threat density), not raw coordinates. (B) **Hard death at 0 HP**; **Recall** is the save-before-death. (C) spec-first, then implement.*

---

## 1. Determinism additions (extends spec §1)

The combat model adds space and per-creature AI. Both must stay bit-identical across OS/CPU (D1–D7 still hold). New rules:

- **D8 — Fixed-point space, integer only.** Positions are **integer milli-units**: `1000 mu = 1 metre`. Stored as `int` (`FxVec2 { int X, int Y }`). No floats anywhere. The arena is a bounded rectangle (§2). Velocities are **mu per tick** (`int`).
- **D9 — Distances via squared int64.** Range/adjacency tests compare `dx*dx + dy*dy` (computed in `long`) against `range*range` — **never** take a square root for comparisons. A real length is needed only for movement normalisation (§4), via a deterministic integer sqrt (Newton, fixed iteration count — in-repo, like PCG32/xxHash).
- **D10 — Facing as an integer direction.** A creature's facing is a **unit-ish `FxVec2` scaled to 1000** (a "milli-unit direction"), updated by integer math. No `atan2`, no trig tables in the rules core. "Flanking" = sign of the dot product between attacker→target and target's facing (all integer).
- **D11 — Spatial hash is an accelerator, not a source of truth.** Neighbour queries go through a uniform grid (§3), but every query **returns candidates sorted by `EntityId`** and must yield results identical to brute force. The grid is rebuilt deterministically each tick; it never affects outcomes, only speed. (A debug build asserts grid-query == brute-force.)
- **D12 — AI is a pure priority function.** A creature's decision is a deterministic function of (its directive, its ability readiness, the current world state via derived features). No randomness, no wall-clock, no unordered iteration. Ties break by a fixed key (ability index, then `EntityId`).

State hash (spec §8) gains: each creature's position, facing, movement goal, per-ability `cooldownUntil`, and current action (§5). Field order is the versioned contract.

## 2. The arena

- A bounded rectangle, **default 4000 × 3000 mu** (4 m × 3 m — field/tabletop scale; a `DuelConfig` value, ⚑ provisional). Origin at centre.
- **Summoners** are fixed points: P0 at `(0, -1500)`, P1 at `(0, +1500)` (the two ends). Creatures cannot occupy the summoner points; the summoner is a valid *target* at its point.
- **Zones** (`Vanguard/LeftFlank/RightFlank/Rear`) are **derived regions** of the arena, per owner (e.g. P0's Vanguard is the forward-centre band on P0's half). Used by `Hold(zone)` and `Defend(zone)` directives and by summon placement. Zone membership is computed from position, not stored.
- Creatures have a **body radius** (default 150 mu ⚑) for soft separation (§4) and as the AoE hit test's target extent.

## 3. Spatial index

- Uniform grid, **cell size = the largest ability range in play** (so any ability's targets lie in the query cell + 8 neighbours). Rebuilt each tick from the (already `EntityId`-sorted) `Field` list.
- Query API (all return `EntityId`-sorted): `WithinRadius(center, r, filter)`, `InCone(apex, dir, halfAngleDot, length, filter)`, `Nearest(center, filter)`. Filters: enemy-of/ally-of/summoner/creature, alive-only.
- With the model's *few creatures per side* (typically 2–6), the grid is cheap; it exists mainly to keep 10k-duel sweeps fast and to make "threat density" queries O(local).

## 4. Movement

Each tick, a creature in a moving state steps toward its **goal point** (set by its AI — e.g. "get within Fire Breath range of target"):

```
delta = goal - pos;  distSq = delta·delta (long)
if distSq <= arriveTol²: arrived (stop)
else:
   dist = IntSqrt(distSq)                      // deterministic integer sqrt, movement only
   step = min(moveSpeed, dist)
   pos += delta * step / dist                  // integer scale; truncates toward zero (defined)
   facing = normalizeTo1000(delta)             // integer direction
soft separation: if overlapping another creature (dist < bodyR₁+bodyR₂), nudge apart
   by a deterministic integer push (both sorted by EntityId so it's order-stable)
```

`moveSpeed` is per-creature (mu/tick, from content ⚑). Movement is the *only* place a real length is computed. Summoner is stationary (canonical: feet planted).

## 5. Creature runtime state (extends `MonsterInstance`)

```
FxVec2 Pos, Facing;
int MoveSpeed;
FxVec2 Goal;                 // AI-set destination
ActionState Action;          // Idle | Moving | WindingUp | Recovering
int ActionUntilTick;         // when the current WindingUp/Recovering ends
int ActiveAbilityIndex;      // during WindingUp/Recovering
int[] CooldownUntil;         // per ability
Directive Directive;         // Attack/Defend/Hold/Retreat/Follow (+ target)
int Hp, MaxHp;               // MaxHp now ~ hundreds–thousands (big-HP attrition)
List<StatusInstance> Statuses;
```
HP scale-up: MaxHp is large (Dragon-class ~1000; commons ~200–400) so single hits chip, not one-shot. `balance.v0` gets a global HP scale, and the stat budget is re-derived (§9).

## 6. Ability data model (content)

```
record AbilityDef(
    string Id, int Damage, DamageType Type,
    int RangeMinMu, int RangeMaxMu,          // valid engagement band
    int WindupTicks, int CooldownTicks, int RecoveryTicks,
    AoeShape Shape,                          // Single | Circle(radius) | Cone(halfAngleDot,length) | Line(width,length)
    TargetPref Pref,                         // Any | Swarm | Armored | Summoner | LowestHp | HighestThreat
    int CostMilli = 0);                      // optional mana/stamina
record CardDef( … , IReadOnlyList<AbilityDef> Abilities );   // replaces single Atk
```
`Hp` stays on `CardDef`; `Atk` is removed in favour of `Abilities`. Keywords remap onto abilities/creatures (Swift → shorter cooldowns; Pierce → AoE overflow to summoner; Drain → self-heal on damage; Ambush → first ability from stealth at ×bonus; Bulwark/Ward/Regrow unchanged as creature properties).

## 7. Creature combat AI (the tactics layer)

Re-decides when it enters `Idle` (after Recovery), or when its current plan is invalidated (target died / left the arena / directive changed), or every `AiReevalTicks` (a cap, default 10 ⚑) so it adapts to a moving battlefield.

**Derived features** (computed per decision from world + spatial hash, never stored as truth):
- primary target (from directive; for `Defend`, the biggest threat to the protected entity),
- range band to target (out-of-range / in-band / too-close per each ability),
- nearby enemy count + **threat density** (sum of enemy DPS-equivalent within a radius),
- **flanking** (dot of target→me vs target facing), **facing** to target.

**Decision (deterministic priority):**
```
1. Resolve primary target from Directive (Attack→named/auto; Defend→top threat to ward; Hold→enemy in zone).
2. Among abilities OFF cooldown, score each by expected value:
      score = Damage × EstTargetsHit(shape, target, density)     // AoE shines vs swarms
            × PrefMatch(pref, target)                            // anti-swarm/armor/etc.
            ÷ (WindupTicks + RecoveryTicks + 1)                  // commitment risk
      require target within [RangeMin,RangeMax]; else ability not eligible this tick
3. If a ready+in-range ability exists → commit to the highest score (ties: ability index, then EntityId) → WindingUp.
4. Else → set Goal to the point that enters the best not-yet-in-range ability's band → Moving.
5. Defend with no active threat → Goal = between ward and nearest enemy (interpose). Retreat → Goal toward summoner (→ Recall, §8). Follow → Goal near summoner.
```
This yields the vision's behaviour: far target → charge the long-range nuke; target closes → switch to fast melee; swarm behind → the anti-swarm AoE scores highest unprompted.

## 8. Ability activation pipeline (replaces spec §6 phases 5–6)

Per tick, after timers (phase 3) and before deaths (phase 6), a new **phase 5 — Creature Act**:

```
for each creature c in Field (EntityId order):
  MOVEMENT: if c.Action == Moving → step (§4); if arrived & ability now in range → fall through to decide
  AI:       if c.Action == Idle (or reeval due) → run §7 → sets Moving(goal) or commits an ability
  COMMIT:   entering an ability → Action=WindingUp; ActionUntilTick=t+windup;
            emit AbilityCommitted(c, abilityId, target)  +  AbilityWindupStarted (the telegraph)
  RESOLVE:  if Action==WindingUp and t>=ActionUntilTick →
              gather targets = spatial query for Shape at resolution time (targets may have moved/died)
              apply Damage to each (EntityId order): Ward → Hp; Pierce overflow → summoner (Guard-reduced);
                 Drain → self-heal; on-hit statuses
              emit AbilityResolved(c, abilityId, hits[])
              CooldownUntil[ability]=t+cooldown; Action=Recovering; ActionUntilTick=t+recovery
  RECOVER:  if Action==Recovering and t>=ActionUntilTick → Action=Idle
```
- **Wind-up is the counterplay surface** (combat-model §9 Q4): during WindingUp the opponent can heal/shield/recall the target or **Interrupt** (a mage effect / heavy CC ability) which cancels the wind-up (→ Idle, ability goes on a short cooldown). v0: only an explicit `Interrupt` effect and death cancel a wind-up.
- Summoner-directed attacks (creature's target is the enemy summoner and it's in range) apply face damage through the existing danger-scaled **Guard** path.
- **Mutual damage is emergent:** two creatures each ordered to attack the other independently run this loop and grind each other down — no retaliation rule needed.

## 9. Balance budget, re-derived (replaces `balance-math.md` §2 stat line)

Cost now buys **HP + a kit**. Provisional v0 model (⚑ all to be calibrated by the harness):
```
budgetSP(C)      = 2C + 2                             // unchanged total budget
HP               = hpScale × (share_hp × budgetSP)    // hpScale ≈ 60 (big-HP attrition)
ability DPS-eq   = Damage × TICKS_PER_MIN / (Windup + Recovery + Cooldown)
kit value        = Σ abilities( DPS-eq/κ  + rangePremium + aoePremium + controlPremium )
require:  share_hp × budgetSP  +  kit value  ≈ budgetSP    (± tolerance)
```
The content validator (`ContentValidator.CheckBudgets`) is rewritten to this. Aspect tax matrix still applies (Ember abilities cheap-fast, Bastion cheap-HP/control, etc.).

## 10. Schema & event changes

- **Removed:** `AttackVolleyResolved`, single-`Atk` assumptions.
- **Added events:** `AbilityCommitted(creature, abilityId, target, windupTicks)`, `AbilityResolved(creature, abilityId, hits[])`, `CreatureRecalled(creature)`, `CreatureMovedTo` (throttled/periodic — not every tick; presentation interpolates from goals + a coarse position stream). Positions ride in `DuelView` + the state hash every tick; the *event* stream stays lean.
- **Intents unchanged in shape**; `CommandMonster` directive set expands (Hold/Retreat/Follow already fit `Directive`). `Recall` = the Retreat directive reaching the summoner, or an explicit `Recall(EntityId)` intent (add).
- **Recall mechanic:** Retreat/Recall → creature Goals to the summoner; on arrival, a short recall wind-up (⚑ ticks) then it returns to hand (it can still die during it — the risk). Recalled/reserve creatures **regen HP** in hand at a slow rate ⚑ (the "let it heal, redeploy later" loop). Hard death at 0 HP → graveyard (revive = Umbra).

## 11. Performance & sweep budget

Few creatures/side (2–6) × movement + one AI eval per idle/reeval + local spatial queries. Comfortably within 10k-duel nightly sweeps (current empty-ish sims run 500 duels in ~0.4 s; expect a small constant-factor increase, still seconds for 10k). The spatial grid keeps "nearby/threat-density" O(local) as creature counts grow for big summons.

## 12. Migration plan (once approved)

1. Add fixed-point primitives (`FxVec2`, `IntSqrt`), arena config, spatial grid + tests (grid == brute force).
2. Extend `CardDef` (Abilities, remove Atk) + content (`AbilityDef` JSON) + re-derived validator; rewrite the 35-card set's monsters into kits (commons 1–2 abilities, rares 2–3).
3. Replace combat phase with the §8 pipeline; add movement (phase 5a) + AI (§7).
4. Recall/death state machine; reserve-HP regen.
5. New events; extend state hash (positions/cooldowns/action). Re-green determinism + replay tests.
6. Re-run trace/sweeps; calibrate hpScale, wind-ups, speeds, ranges toward the 12–18 min band and 48–52% fair matchups. **This is where the real pacing calibration finally happens — on a model that can actually produce attrition.**

## 13. Decisions needing sign-off (this addendum)
1. Arena 4×3 m, summoners at the short ends, body radius 150 mu — reasonable?
2. AoE shapes for v0: Single / Circle / Cone (defer Line)?
3. `TargetPref` set (Any/Swarm/Armored/Summoner/LowestHp/HighestThreat) — enough for v0?
4. Reserve (recalled) creatures heal in hand — flat rate, or only via spent energy/a heal spell?
5. hpScale ≈ 60 starting point (Dragon-class ~1000 HP, hits ~50) — right ballpark?
