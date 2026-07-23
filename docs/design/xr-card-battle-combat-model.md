# Combat Model — Commander & Intelligent Creatures (v0.2, CANONICAL)

*Supersedes the earlier combat resolution model (instant ATK-vs-ATK volleys, per-defender soak/overflow) described in `master-roadmap.md`, `balance-math.md` §5, and the M0–M1 spec §6. Those documents' **economy, currencies, stabilizer intent, and pacing targets remain valid**; only the **resolution mechanism** changes — from "stat blocks subtracting HP each tick" to "ability-driven creatures with their own combat AI." Recorded from the design direction of July 23, 2026.*

---

## 1. Philosophy

The player is a **commander, not a puppeteer**. Deliberately *not* War of Wizards / Clash Royale, where creatures are simple turrets (find target → walk → auto-attack → repeat) and depth comes from spamming summons. This game wants:

- **Fewer creatures** on the field, each **much more valuable**, with **long lifespans**.
- Creatures that feel like **intelligent living beings**, not moving stat blocks.
- Depth from **decisions** (heal? recall? protect? pressure? interrupt? revive?), not from summon volume.

Watching two legendary creatures fight should read as *intelligent fantasy combat*, not two scripted NPCs trading HP.

## 2. The command layer — intentions, not micro

The player never issues an ability ("use Fire Breath"). The player issues a **high-level directive** to a creature; the creature's AI decides *how* to fulfil it:

| Directive | Meaning |
|---|---|
| **Attack(target)** | Engage that enemy creature or the enemy summoner |
| **Defend(target)** | Body-guard the mage or an allied creature; intercept threats to it |
| **Hold(zone)** | Control an area; engage what enters it |
| **Retreat / Recall** | Disengage; Recall returns the creature to hand (see §6) |
| **Follow** | Stay with the mage / reposition to the mage |

Directives are the `CommandMonster` intents (voice grammar's `entity × verb × slot`). Everything below the directive is the creature's own tactics.

## 3. Creatures are ability-driven agents

Every creature has a **kit** of abilities, not a single ATK stat. Each **ability** carries:

- **damage** (and type),
- **range** (see §5 for how range is modelled),
- **wind-up** (charge time before it fires — a telegraph),
- **cooldown** (before it can be used again),
- **recovery** (busy time after firing),
- **area of effect** (single / cone / area / multi),
- **preferred targets** (e.g. anti-swarm, anti-armor),
- optional **mana / stamina cost**.

Example Dragon: *Fire Breath* (high dmg, long wind-up, medium range, wide AoE), *Fireball* (medium dmg, long range, fast projectile), *Bite/Claws* (fast, close, reliable), *Tail Whip* (low dmg, very fast, hits multiple nearby, anti-swarm), *Wing Slam* (knockback CC, long cooldown).

## 4. The creature combat AI

Given its directive + the live battlefield, each creature continuously chooses which **ready** ability best fits:

- Target far → charge the long-range high-damage option (Fire Breath).
- Target closes in → switch to close-range DPS (Bite/Claws).
- Swarm behind it → auto Tail Whip (anti-swarm AoE), unprompted.

The AI re-evaluates as the fight evolves (target moves, is healed, shielded, recalled, replaced). This is **deterministic** (rules-engine requirement R1): the AI is a pure priority function over the current state + the creature's ability readiness, not randomness.

## 5. Continuous real-time resolution

- **No turns, no attack declarations, no ATK comparisons, no instant retaliation.** (Explicitly deletes the Yu-Gi-Oh-style instant model and the prior volley resolver.)
- Combat is a stream of **ability activations**: a creature commits to an ability, winds up (telegraph — the opponent can react: heal, shield, recall, interrupt), then it **resolves**, applying damage to whatever is validly in range/area *at resolution time* (the target may have moved or died — that's real).
- **Big HP, small per-hit damage** → creatures **wear each other down** over many hits. Nothing dies from one exchange. (Dragon 1000 HP; a 50-damage hit leaves 950.) This is what produces the long, tactical fights the 12–18 min pacing target wants — the "duration" comes from attrition depth, not inflated life totals.
- **"Mutual damage" is emergent, not a rule:** two creatures each told to attack the other both run their own ability loops and grind each other down. A creature told to **Defend** is *bracing* — it soaks and protects, and only deals damage if its directive/AI has it strike. So the old "block: soak vs retaliate" question dissolves — it's just two independent agents.

## 6. Recall, heal, revive — creatures are persistent assets

Creatures do **not** always fight to the death:

- **Recall:** pull a damaged creature back to hand (disengage before it dies). Summon a fresh body to cover, let the recalled one heal, redeploy later.
- **Heal / shield** on the field extend a creature's engagement.
- **Revive** (Umbra's graveyard identity) brings fallen creatures back at a cost.

This makes the summon a **long-term investment** with an exit option, not a disposable unit — the strategic core of the "fewer, more valuable creatures" goal.

## 7. The player's parallel game

While creatures fight, the mage is *always* deciding: heal, recall, summon a protector, interrupt an enemy cast, buff/debuff, block/ward (danger-scaled Guard), position, manage mana, revive. **Mage provides strategy; creatures provide tactics; together the battle is a collaboration.** This satisfies the anti-spectator rule (the mage always has a meaningful action) without room-scale movement (stationary-battlemage decision stays intact).

## 8. What carries over unchanged from prior docs

- The **five currencies** (energy, SP, life, time, attention), the **Aspect tax matrix**, **stabilizers' intent** (surge/danger-scaled defense/escalation as comeback + pacing forces), **pacing targets** (12–18 min median), and the **two-lane progression / live-balancing** all still hold. They now sit on top of ability-based creatures instead of single-ATK creatures.
- **Monster stat budget** (`balance-math.md` §2) must be **re-derived**: a creature's cost now buys HP + a *kit* (abilities priced by damage-per-second-equivalent, range, AoE, control), not `ATK + HP`. This is new balance-math work.

## 9. Open questions this raises (for the next design pass)

1. **Spatial fidelity in the rules engine** — full integer positions + movement + geometric AoE, or an abstraction (zones + range-bands + "adjacent/behind" relations)? The rules engine wants the *least* fidelity that still makes the tactical decisions real. **Recommendation: range-bands + the existing field zones + simple adjacency, integer positions deferred to presentation** — enough for "far→Fire Breath, swarm-behind→Tail Whip" without a physics sim in the deterministic core.
2. **Death vs downed** — hard death at 0 HP (revive via Umbra), or a "downed/recallable" window before death? Recall implies you want a save-before-death option; is that pure timing, or a grace state?
3. **Ability authoring & balance** — how many abilities per creature by rarity (commons 1–2, legendaries 4–6?), and the DPS-equivalent pricing model that replaces `SP = 2C+2`.
4. **Wind-up as the counterplay surface** — telegraph windows are where heal/shield/recall/interrupt matter; their durations are the new knife-edge to tune.

---

*Maintenance: `00-INDEX.md` §3 combat bullet updated to point here. The M0–M1 spec §6 (tick pipeline phases 5–6) must be revised to an ability-activation model before further engine work on combat; the deterministic tick, Intent/Event schema, economy, and replay machinery are unaffected.*
