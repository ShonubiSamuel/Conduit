# M0–M1 Technical Specification — Headless Engine v0.1 (DRAFT)

*The first document an engineer implements from. Covers: the determinism contract, the Intent/Event schema (R2), the engine state machine, combat resolution, the sim harness, CI gates, and repo structure. Companion to `master-roadmap.md` (M0–M1), `balance-math.md` (all constants), `casting-and-cards.md` (ladder + keywords), and `power-systems.md` (Aspects). Where anything here touches a contested point, `00-INDEX.md` §3 wins.*

**Status: DRAFT v0.1 — awaiting approval. Engine codename used throughout: `Conduit` (placeholder, from the universal cost rule; rename freely).**

---

## 0. Scope

### M0 delivers
- Repo scaffolded per §12, CI running.
- ~30 proxy cards encoded as content JSON (`content/cards/`), balance constants as `content/config/balance.v0.json`.
- The sim harness (§10) running scripted-archetype duels against the engine as it grows (the balance-doc sims re-homed onto real code).

### M1 delivers
- The complete headless engine: deterministic, pure C#, netstandard2.1, zero dependencies (R1).
- Full Intent/Event schema (§4–5), per-tick pipeline (§6), keyword set v0 (§7).
- Replay files with bit-identical playback (§8); state hashing; golden-replay + cross-OS determinism CI gates (§11).
- Headless AI-vs-AI runner feeding the M0 harness.

### Explicitly excluded from M1 (schema leaves room; code does not exist)
Reactions, Oaths, fusion Aspects, hidden mechanics, mulligans, networking, voice, rendering, Unity anything. Extension points in §13.

### ⚠️ Discrepancy resolved by INDEX authority
`master-roadmap.md` M1 mentions "WEGO-with-interrupts timing." `00-INDEX.md` §3 canonically specifies **real-time, energy-paced continuous combat (no turns)**. Per the INDEX's own rule, this spec implements the **continuous model**. The interrupt *fantasy* survives as: interruptible Script/Ritual channels (§6.5), the reactive Guard action (§6.7), and Ambush reveals — no phase structure. *(Recommend updating the roadmap's M1 wording when this spec is approved.)*

---

## 1. The determinism contract (non-negotiable, R1)

Same replay file → bit-identical event stream and final state hash, on any OS/CPU, forever. Seven rules make that true:

- **D1 — Fixed tick.** The engine advances in discrete ticks at **20 Hz** (50 ms). All time is `int` ticks. Rationale: attack period 3 s = 60 ticks exactly; every duration in the design lands on integer ticks; 20 Hz keeps 10k-duel nightly sims cheap. Wall-clock time exists only *outside* the engine (the host maps real time → tick numbers).
- **D2 — Integer-only state and math.** No `float`/`double` anywhere in engine state or rules logic (cross-platform float determinism is not worth fighting). Energy in **milli-energy** (1 energy = 1000 mE). Multipliers in **basis points** (1.3× = 13000 bp; apply as `value * bp / 10000` with defined truncation-toward-zero). Fractional per-tick rates use **Bresenham accumulators**: `acc += ratePerMin; gain = acc / TICKS_PER_MIN; acc %= TICKS_PER_MIN` (TICKS_PER_MIN = 1200). Pure integer, no drift.
- **D3 — Engine-owned PRNG.** A small in-repo **PCG32** (do not use `System.Random` — its algorithm is not a stable contract). Seeded from the replay header. Every random act (deck shuffle, future randomness) draws from this one stream, in pipeline order.
- **D4 — No ambient environment.** No `DateTime`, no I/O, no threading, no statics carrying state, no Unity types. The engine is a pure function of (config, content, seed, intent stream).
- **D5 — Deterministic iteration.** No enumeration of unordered collections in rules code. Runtime collections are arrays/`List<T>`; whenever order matters, sort by a canonical key (ascending `EntityId`, ties impossible by construction). `Dictionary` allowed only for lookup, never iterated.
- **D6 — Simultaneous resolution.** Within a tick, all combat reads happen against start-of-phase state, all writes apply together (sim finding #4: sequential resolution = 100% first-mover advantage). Same-tick intents from both players are validated independently against start-of-tick state; there is no first-submitter privilege.
- **D7 — Verified, not assumed.** State hash every N ticks (§8), golden replays in CI, and a cross-OS determinism job (§11). Determinism regressions block merge like any test failure.

---

## 2. Units & v0 constants

All constants live in `balance.v0.json` — **the engine hardcodes nothing tunable** (this is what makes M9 live-balancing a data push, not a patch). Values below are the balance-doc's v0.1 numbers converted to engine units. ⚑ = flagged provisional, playtest-calibrated.

| Constant | Value (engine units) | Source |
|---|---|---|
| Tick rate | 20 Hz (TICKS_PER_MIN = 1200) | this spec |
| Life | 150 | balance §1 |
| Energy pool cap | 10000 mE | balance §1 |
| Starting energy | 3000 mE | balance §5 |
| Base regen | 20000 mE/min (accumulator) | balance §1 |
| Escalation ramp | +600 mE/min per elapsed minute, cap 26000 at min 10 | balance §5 |
| Surge (outnumbered) | regen ×15000 bp while fielded count < opponent's ⚑ (doc range 1.4–1.6×) | balance §5 |
| Attack period | 60 ticks (Swift: 40) | balance §2, keywords |
| Cast durations | Sign 15 / Word 25 / Script 50 / Ritual 80 ticks | casting §3 |
| Cast multipliers | 10000 / 13000 / 17000 / 22000 bp | casting §3 |
| School cooldown | 160 ticks (8 s) ⚑ (doc range 6–10 s) | casting §3 |
| Diminishing echoes | same spell within 300 ticks: 7000 bp, then 5000 bp | casting §3 |
| Guard block | 3000 bp base → `min(5500, 3000 + (150 − life) × 25)` bp | balance §5 (danger-scaled) |
| Draw | 1 charge per 600 ticks (30 s), bank cap 2 ⚑ | research §6 pacing |
| Opening hand | 5, no mulligan in v0 ⚑ | research §6 |
| Board cap | 4 fielded monsters per player | voice grammar (0–4 entities) |
| Deck | exactly 30, 2-of (legendary 1-of) — validated at duel creation | deck-progression |
| Monster stat budget | content-side rule: SP = 2C+2, 1 ATK = 2 SP, 1 HP = 1 SP | balance §2 |

---

## 3. Data model

### 3.1 Identity
```csharp
readonly struct PlayerId(byte value);          // 0 or 1
readonly struct EntityId(int value);           // engine-assigned, monotonic, never reused
readonly struct CardDefId(string value);       // content key, e.g. "ember.emberdrake"
```

### 3.2 Content (immutable, loaded at duel creation, hash-stamped)
```csharp
record CardDef(
    CardDefId Id, Aspect Aspect, ClanId Clan, Rarity Rarity,
    CardKind Kind,                 // Monster | Spell
    int CostMilliEnergy,
    int Atk, int Hp,               // monsters; live-balanced values (server supplies current)
    KeywordSpec[] Keywords,        // max 2 (legendary 3), from the 10-keyword set
    EffectOp[] Effects,            // closed op-set, §3.3 — NO scripting language in v0
    CastTierMask AllowedTiers      // spells: which ladder rungs this card accepts
);
```
`Aspect` = Ember|Tide|Bastion|Gale|Verdant|Umbra. The Aspect **tax matrix is a content-authoring rule, not engine logic** — the engine trusts costs as given; the content validator (§12, `Engine.Content`) enforces budget/tax/deck-legality at load time.

### 3.3 EffectOp (the closed effect vocabulary, v0)
```
Damage(target, amount) · Heal(target, amount) · ModifyStats(target, atkΔ, hpΔ, durationTicks)
GrantStatus(target, statusId, magnitude, durationTicks) · SummonToken(tokenDefId, count)
DrainLife(target, amount)        // damage + heal caster's summoner, half
Hex(target, atkΔbp, durationTicks)
```
Every card's behavior is a list of these. If a design needs a new op, that is a spec change, not a content hack — the op-set is the balance surface.

### 3.4 Runtime state (the whole world, snapshot-able)
```csharp
class DuelState {
    int Tick; DuelPhase Phase;                    // Created | Running | Ended
    Pcg32 Rng; PlayerState[] Players;             // [2]
    List<MonsterInstance> Field;                  // both sides, sorted by EntityId
    DuelResult? Result;
}
class PlayerState {
    PlayerId Id; int Life; int EnergyMilli; int RegenAccumulator;
    List<CardInstance> Deck, Hand, Graveyard;     // zones; deck order IS state (shuffled once via Rng)
    int DrawCharges; int DrawAccumulator;
    ChannelState? Channel;                        // active cast, if any (§6.5)
    int[] SchoolCooldownUntil;                    // per school
    List<EchoRecord> RecentCasts;                 // for diminishing echoes
    GuardState? Guard;                            // active ward window (§6.7)
}
class MonsterInstance {
    EntityId Id; PlayerId Owner; CardDefId Def;
    int Atk, Hp, MaxHp; FieldZone Zone;           // LeftFlank | Vanguard | RightFlank | Rear
    Directive Directive;                          // §4 CommandMonster
    int NextAttackTick; bool FaceDown;            // Ambush
    List<StatusInstance> Statuses;                // Ward pools, Regrow, hexes, charges…
}
```
Zones per canon: **deck + hand are physical; field/graveyard are virtual-authoritative.** The engine models all four; physical handling is a producer concern above the engine.

---

## 4. Intent schema (R2 — everything is an Intent)

### 4.1 The contract boundary
An Intent reaches the engine **after** multimodal fusion (voice-architecture L3): deictic references ("that one") are already resolved to concrete `EntityId`s; grammar parses are already typed. The engine never sees audio, gaze rays, card poses, or text. **How** an intent was produced lives only in envelope metadata, which the rules ignore (R2) but replays retain (telemetry gold).

### 4.2 Envelope
```csharp
record IntentEnvelope(
    ulong Seq,                 // per-actor monotonic; dedupe + ack key
    PlayerId Actor,
    int SubmittedTick,         // quantized by the host; engine clamps to >= current tick
    Intent Payload,
    IntentMeta Meta            // modality (Voice|Gesture|Card|Mouse|AI), client ms — IGNORED by rules
);
```

### 4.3 Payload variants (closed union)
```csharp
abstract record Intent;
record SummonMonster(CardInstanceId HandCard, FieldZone Slot)                : Intent;
record CastSpell(CardInstanceId HandCard, CastTier Tier, TargetRef Target)   : Intent;
record CancelChannel()                                                       : Intent;
record CommandMonster(EntityId Monster, Directive D)                         : Intent;
record Guard()                                                               : Intent;  // built-in caster ward, §6.7
record DrawCard()                                                            : Intent;
record Concede()                                                             : Intent;

// Directive := Attack(TargetRef) | Defend(FieldZone) | Return | Sacrifice | UseAbility(AbilityId, TargetRef)
// TargetRef := Monster(EntityId) | Summoner(PlayerId) | Zone(FieldZone) | None
```
This is exactly the voice grammar's `entity × verb × slot` made concrete — the compiled grammar (voice doc §3) and this union must stay isomorphic; a verb exists in one iff it exists in the other.

### 4.4 Validation (phase 2 of the tick pipeline; every rule = a rejection reason)
```
UnknownEntity · NotYourCard · WrongZone · InsufficientEnergy · BoardFull
SchoolOnCooldown · AlreadyChanneling · ConduitBroken        // no fielded monster → Script/Ritual tiers blocked
TierNotAllowed · IllegalTarget · NoDrawCharge · DuelOver
```
`IntentRejected{seq, reason}` is an ordinary event — recognition errors and cheats alike die here as rejected intents, never as corrupted state (R3's engine-side half).

---

## 5. GameEvent schema (R4 — presentation renders these, and only these)

```csharp
abstract record GameEvent(int Tick);
// intake        IntentAccepted(seq) · IntentRejected(seq, reason)
// zones         DrawResolved(player, card) · CardSpent(player, card)          // → physical discard pouch
// summoning     SummonResolved(monster snapshot, zone, faceDown)
// casting       ChannelStarted(player, card, tier, durationTicks)             // the opponent's telegraph
//               ChannelBroken(player, reason: Cancelled|Interrupted)
//               CastResolved(player, card, tier, appliedEffects[])
// combat        AttackVolleyResolved(attacks[])                               // ONE event per tick: all
//                   // simultaneous attacks, each {attacker, target, raw, blocked, overflow, pierced}
//               MonsterDied(monster, killer?) · SummonerDamaged(player, amount, source)
// economy       EnergyChanged(player, delta, newValue) · SurgeStateChanged(player, active)
//               DrawChargeGained(player) · GuardRaised(player, blockBp) · GuardExpired(player)
// statuses      StatusApplied/StatusExpired(entity, statusId, magnitude)
// lifecycle     DuelStarted(seed, configHash, decksHash) · DuelEnded(result, reason)
// integrity     StateHash(tick, hash)
```
Guarantees: events within a tick are emitted in pipeline order (§6.1) and are **sufficient to reconstruct everything a client renders** — a presentation layer that stores no rules state of its own is the R4 test. The desktop skin (M2), XR skin (M5), and spectator app all consume this stream unchanged.

---

## 6. The engine state machine

### 6.1 Per-tick pipeline (fixed order — this IS the determinism spine)
```
Tick N:
 1. INGEST      pull intents queued for tick ≤ N; order by (Actor, Seq); dedupe by Seq
 2. VALIDATE    each intent vs start-of-tick state → Accepted/Rejected events;
                accepted intents take effect below (summon/cast start, directive set…)
 3. TIMERS      regen + ramp + surge accumulators; draw charges; cooldowns;
                echo windows; Regrow ticks; status expiry; channel progress; guard expiry
 4. CAST        channels reaching completion apply their EffectOps (tier multiplier, echoes)
 5. COMBAT      collect ALL attacks with NextAttackTick == N (both players) →
                resolve as one simultaneous volley vs pre-volley state (§6.6)
 6. DEATHS      Hp ≤ 0 processed together: on-death hooks (Torrent…), zone moves, cleanup
 7. WINCHECK    life ≤ 0 → DuelEnded (both ≤ 0 same tick = Draw — priced-in per sim finding #4)
 8. EMIT        drain event buffer; every HASH_INTERVAL ticks append StateHash
```

### 6.2 Duel lifecycle
`Created` (decks validated, shuffled via Rng, hands drawn) → `Running` (pipeline loops) → `Ended` (only `Concede` accepted → rejected with `DuelOver`; terminal).

### 6.3 Monster lifecycle
```
[HandCard] --SummonMonster--> [Fielded] --Hp≤0/Sacrifice--> [Dying] --> [Graveyard]
                                 |  Ambush: enters FaceDown; reveals on first attack (2× strike)
                                 |  Directive drives behavior; default = Attack(None) → auto-acquire
                                 |  Return directive: back to hand ⚑ (v0: costs the card's summon
                                 |    energy refund of 0 — it just returns; revisit)
```
Auto-acquire order (deterministic): opposing monsters in the attacker's own zone first, then Vanguard, then adjacent, then Summoner if `Zone` path is empty — full table in code, sorted by `EntityId` for ties.

### 6.4 Summoning
Instant on validation (energy debited, board slot filled, `SummonResolved` next in-tick). Summon theatrics (ritual chants, entrance animations) are **presentation time, not engine time** — the engine models only the ladder's *cast* durations for spells. Legendaries with mechanical summon delay are a §13 extension.

### 6.5 Casting (the ladder as a channel FSM)
```
CastSpell(tier) accepted →
  Sign/Word:   channel runs 15/25 ticks — NOT interruptible (canon), completes in phase 4
  Script/Ritual: channel runs 50/80 ticks — INTERRUPTIBLE:
      · CancelChannel intent → ChannelBroken(Cancelled), energy refunded MINUS 25% fee ⚑
      · any Summoner damage > 0 while channeling → ChannelBroken(Interrupted), same refund ⚑
  Completion: EffectOps applied at tier multiplier × echo multiplier; school cooldown starts;
              EchoRecord appended.
```
One channel per player at a time (`AlreadyChanneling`). `ChannelStarted` is public — the glowing telegraph is literally this event.

### 6.6 Combat resolution (per volley — sim findings #1/#4/#5 encoded)
```
for the set S of attacks firing this tick (both players' monsters):
  1. snapshot all Hp/Atk/Ward values (reads bind to snapshot)
  2. assign blockers: for each attack targeting zone Z or the Summoner, defenders in Z with
     Defend directive intercept, one attacker each (Bulwark: two), assignment order by EntityId
  3. compute damage: raw = Atk × (Ambush reveal? 20000bp : 10000bp)
     · vs blocker: Ward pools absorb first; excess-beyond-kill carries to Summoner iff Pierce
     · unblocked attacker → Summoner: reduced by active Guard blockBp (§6.7)
     · Drain: attacker's owner heals half of damage dealt (truncated)
  4. apply all writes together; queue deaths for phase 6
```
Per-defender blocking with excess-to-face (finding #5) is structural here, not tunable.

### 6.7 Guard (the stationary battlemage's built-in ward — danger-scaled defense)
`Guard` intent: no card, costs 500 mE ⚑, channel 5 ticks, active window 20 ticks, shares the defense-school cooldown. While active, Summoner damage is reduced by the life-scaled blockBp (§2). This is the engine half of "telegraphed attack → planted defensive gesture"; producers map the physical gesture to this intent.

### 6.8 Stabilizers (all in phase 3, all data-driven)
Surge multiplier on regen while outnumbered · escalation ramp on regen rate · danger-scaled Guard (above) · per-defender overflow (in combat, structural). These four are the pacing dials — the sim harness sweeps them, humans never hand-tune constants without a sweep (sim finding #2).

---

## 7. Keyword implementation map (v0 — all ten)

| Keyword | Mechanism | Where |
|---|---|---|
| Bulwark | blocks 2 attackers per volley | §6.6 step 2 |
| Swift | attack period 40 ticks | §6.3 timer |
| Pierce | excess-beyond-kill damage carries to Summoner | §6.6 step 3 |
| Ward X | absorb-pool status, breaks at 0 | StatusInstance |
| Regrow X | +X Hp per 100 ticks (cap MaxHp) | phase 3 |
| Ambush | FaceDown entry; 2× first strike on reveal | §6.3 / §6.6 |
| Drain | owner heals half damage dealt | §6.6 step 3 |
| Torrent | on death, applies Aspect charge status to killer (stored; consumed by M8 Reactions) | phase 6 hook |
| Swarm X | SummonToken ops on entry | §6.4 |
| Soul-link | stat bonus while named partner fielded (checked phase 3; hidden links = content flag) | phase 3 |

---

## 8. Replay format & state hashing

```
.cvr file (Conduit Verified Replay), little-endian binary:
 HEADER  magic "CVR1" · schemaVersion · engineVersion · seed (u64)
         configHash · cardSetHash · per-player: deck list (CardDefIds, 30)
 BODY    intent stream: [tick:varint Δ][actor:u8][seq:varint][payload: tagged union]
 FOOTER  finalTick · finalStateHash (u64) · eventCount
```
- **State hash:** xxHash64 over a canonical field-order serialization of `DuelState` (spec'd field list in code, versioned). Emitted every 100 ticks (5 s) and at duel end.
- **Bit-identical guarantee:** replaying BODY through the same engineVersion + content hashes MUST reproduce every StateHash and the footer. This is a CI-enforced invariant, and it is also the M3 server-validation and reconnect mechanism, built two milestones early for free.
- Meta (modality etc.) rides in an optional TLV side-channel — ignored by playback, mined by telemetry.

---

## 9. Public API (the complete surface — small on purpose)

```csharp
public sealed class DuelEngine {
    static DuelEngine Create(DuelConfig cfg, ContentDb content, DeckList[] decks, ulong seed);
    static DuelEngine FromReplay(ReplayFile replay, ContentDb content);   // validation/playback

    void Submit(IntentEnvelope intent);          // queue; takes effect at next AdvanceTick
    IReadOnlyList<GameEvent> AdvanceTick();      // run pipeline §6.1 once, return that tick's events
    void AdvanceTo(int tick);                    // sim fast-forward (events via sink callback)

    int CurrentTick { get; }
    ulong StateHash();
    DuelView View(PlayerId asSeenBy);            // read-only projection, hidden info culled
                                                 //   (opponent hand/deck contents masked) — this
                                                 //   same projection later feeds each network client
    ReplayFile ExportReplay();
}
```
`DuelView` is what AI policies, the desktop UI, and (later) per-client network payloads all consume — hidden-information discipline starts here, not in M3.

---

## 10. Sim harness & AI policies (M0, permanent infrastructure)

`SimHarness` console app:
```
conduit-sim run --duels 10000 --matchup aggro:wall --seed-base 42 \
                --config content/config/balance.v0.json --out results.json
conduit-sim sweep --param surge_bp=12000..18000:1000 --duels 2000 ...
conduit-sim verify --replay golden/*.cvr        # bit-identical check
```
- **Policy bots** (deterministic, rule-priority scripts — no ML): `AggroBot`, `WallBot`, `TempoBot`, `ControlBot` — the four archetypes from the balance sweeps, now driving the *real* engine. Bots consume `DuelView`, emit `IntentEnvelope`s (Meta.modality = AI).
- **Output telemetry** matches the balance doc's bands: per-matchup winrates, duel-length percentiles (p10/p50/p90), first-summon time, action-availability gaps, energy-waste (capped-pool time).
- Same seed + same build → identical results.json, byte for byte. Sweeps parallelize per-duel (each duel is its own engine instance; determinism is per-instance).

## 11. CI gates (GitHub Actions, `ci.yml`)

| Gate | Job | Blocks merge? |
|---|---|---|
| Build + unit tests | ubuntu | ✅ |
| Determinism: run 50 seeded duels twice, compare hashes | ubuntu | ✅ |
| Cross-OS determinism: same 50 duels on ubuntu/macos/windows, compare | matrix | ✅ |
| Golden replays: `conduit-sim verify golden/` | ubuntu | ✅ |
| Invariant property tests (life ≤ max, energy ≥ 0, zone conservation: 30 cards accounted per player always, no orphan EntityIds) | ubuntu | ✅ |
| Nightly: 10k duels across the archetype matrix → band report vs balance §9 thresholds | scheduled | ⚠️ report + alert (bands are provisional until playtests calibrate) |

## 12. Repo structure

```
conduit/                          # new repo — also the new home for the design corpus
├── docs/design/                  #   ← MOVE the 14-doc corpus here from the session-outputs
│                                 #     folder (it currently lives in an ephemeral-looking
│                                 #     Application Support path; it deserves version control)
├── src/
│   ├── Conduit.Engine/           # netstandard2.1, ZERO package dependencies (R1)
│   │   ├── State/  Intents/  Events/  Pipeline/  Combat/  Statuses/
│   │   ├── Rng/Pcg32.cs  Hash/XxHash64.cs
│   │   └── Replay/
│   ├── Conduit.Content/          # JSON loading, CardDef validation: stat budgets (SP=2C+2),
│   │                             #   tax-matrix pricing checks, deck legality, keyword caps
│   └── Conduit.SimHarness/       # console app: run/sweep/verify + policy bots
├── tests/
│   ├── Conduit.Engine.Tests/     # xUnit: unit + property/invariant + determinism tests
│   └── golden/                   # .cvr corpus (grow it: every fixed bug adds its replay)
├── content/
│   ├── cards/                    # the ~30 M0 proxy cards, one JSON per card
│   └── config/balance.v0.json
├── .github/workflows/ci.yml
└── Conduit.sln                   # .NET 8 SDK toolchain; engine targets netstandard2.1
                                  #   (Unity 6 compatible — M2 consumes it as a local UPM
                                  #   package via a generated .asmdef, no code changes)
```

## 13. Extension points (designed-in, not built)
- **Reactions (M8):** Torrent already stores Aspect charges as statuses; a `ReactionResolver` slots into phase 5 between damage and deaths. Caps (≤25% max life, diminishing) are content constants already representable.
- **Oaths (M8):** an `OathState` list on `PlayerState` + a validation predicate hook (phase 2) + a bonus hook (phase 3). Public-information events already exist (`StatusApplied`).
- **Initiative-priced effects:** anything granting resolution priority must become an explicit, energy-priced phase-5 ordering key (sim finding #4 says price it premium) — the volley resolver takes an ordering comparator for exactly this.
- **Snapshot/rollback:** `DuelState` is designed copyable (no shared mutable refs escaping) so M3's server can keep rewind buffers; not exercised in M1 beyond snapshot tests.
- **New EffectOps / verbs:** additive union members with schema-version bumps; replays are versioned so old files stay valid.

## 14. Decisions this draft makes that need your sign-off
1. **20 Hz tick** (vs 30) — chosen for clean landing of all canon durations + sim cheapness.
2. **Integer/basis-point math + Bresenham accumulators** — the determinism cost is near zero; the payoff is cross-platform bit-exactness.
3. **Channel interruption rule:** any Summoner damage breaks Script/Ritual, 75% energy refund ⚑.
4. **Guard as a built-in cardless action** (500 mE, defense-school cooldown) — makes danger-scaled defense real in M1 sims rather than waiting for content.
5. **Draw as a player intent** (physical act) with banked charges (cap 2), not auto-draw.
6. **No mulligan in v0.**
7. **Auto-acquire targeting** default so undirected monsters still fight (autobattler baseline the sims assumed).
8. **Both-die-same-tick = Draw.**
9. **Corpus moves into the repo** (`docs/design/`) — its current path is a session-artifacts folder.

*Maintenance: on approval, register this doc in `00-INDEX.md` §2 and strike the "M0–M1 technical spec" item from §4; update the roadmap's M1 WEGO phrasing per §0.*
