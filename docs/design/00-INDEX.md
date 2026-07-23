# 00 — INDEX & CANONICAL DECISIONS (read this first)

*Entry point for the XR Card Battle Game design corpus. If you are Claude Code or any engineer studying these documents, **start here.** This file states the reading order, the status of every document (some are superseded), and the single canonical decision on every point that was contested or refined across the design process. **Where any document disagrees with the "Canonical Decisions" section below, this file wins.***

*Working title: none finalized. Genre: original-IP mixed-reality trading-card battle game (Yu-Gi-Oh-inspired feeling, original mechanics/lore). Primary platform: Meta Quest 3/3S.*

---

## 1. Reading order

**Tier 1 — canonical design (read fully, in this order):**
1. `xr-card-battle-master-roadmap.md` — the build plan (M0–M10) and the 5 architecture rules. The spine of the project.
2. `xr-card-battle-power-systems.md` — the Aspect system (6 elements, wheel, Reactions, Oaths). The game's identity.
3. `xr-card-battle-balance-math.md` — cost curves, Aspect tax matrix, stabilizers, simulation findings.
4. `xr-card-battle-casting-and-cards.md` — card anatomy, keyword vocabulary, the Sign/Word/Script/Ritual casting ladder.
5. `xr-card-battle-deck-progression.md` — deckbuilding rules, banlist philosophy, two-lane progression.
6. `xr-card-battle-voice-architecture.md` — **canonical voice design (v0.2).** Four registers + E2E-SLU/grammar/gaze hybrid.
7. `xr-card-battle-interaction-system.md` — controllers/hands/cards/state-machine (voice §1 superseded — see status below).
8. `xr-card-battle-card-visibility-engineering.md` — the rendering/occlusion engineering (now Option B primary — see status).

**Tier 2 — analysis & background (reference as needed):**
9. `xr-card-battle-war-of-wizards-deep-teardown.md` — the closest competitor, dissected; steal/avoid list.
10. `xr-card-battle-voice-occlusion-warofwizards.md` — SLM/E2E-SLU latency, occlusion tech, WoW summary.
11. `xr-card-battle-references.md` — inspiration pack (games, papers, anime, film). Evergreen.
12. `xr-card-battle-research.md` — the original umbrella report. Foundational background; superseded on specifics.

**Tier 3 — superseded (historical only):**
13. `xr-card-battle-voice-system.md` — early voice draft. **Superseded by #6.**

---

## 2. Document status registry

| Doc | Status | Notes |
|---|---|---|
| `00-INDEX.md` (this) | **CANONICAL — authority** | Wins over any doc on contested points |
| `master-roadmap.md` | **CANONICAL** | Build sequence, architecture doctrine |
| `power-systems.md` | **CANONICAL** | Aspect system; decisions locked in-doc |
| `balance-math.md` | **CANONICAL** | Numbers are v0.1 provisional; framework is fixed |
| `casting-and-cards.md` | **CANONICAL** | Note: card *faces* are rendered (Option B), see §3 |
| `deck-progression.md` | **CANONICAL** | |
| `combat-model.md` | **CANONICAL (combat)** | v0.2 commander/ability-AI model. Supersedes the instant ATK-vs-ATK volley resolution in roadmap/balance §5/spec §6. Economy + pacing targets unaffected |
| `combat-spec.md` | **DRAFT — awaiting approval** | Implementation spec for combat-model.md: fixed-point positions, spatial hash, ability data model, creature AI, activation pipeline, recall/death, re-derived budget. Replaces m0-m1 spec §6 |
| `voice-architecture.md` | **CANONICAL (voice)** | Supersedes voice-system.md & interaction §1 |
| `interaction-system.md` | **PARTIAL** | §1 (voice) superseded; §3–§8 current |
| `card-visibility-engineering.md` | **PARTIAL** | Option B now primary; occlusion analysis = fallback + still-needed hand occlusion |
| `war-of-wizards-deep-teardown.md` | **CANONICAL (analysis)** | |
| `voice-occlusion-warofwizards.md` | **CANONICAL (analysis)** | Occlusion section: apply Option B per §3 below |
| `references.md` | **EVERGREEN** | |
| `research.md` | **BACKGROUND** | Original report; dedicated docs win on specifics |
| `voice-system.md` | **SUPERSEDED** | Historical only |
| `m0-m1-technical-spec.md` | **APPROVED — combat §6 superseded** | Implemented as the Conduit repo. Determinism contract, Intent/Event schema, tick pipeline, replay, economy all current. **§6 combat (instant volley) is superseded by `combat-model.md` v0.2** and must be re-specified to the ability-activation model |

---

## 3. CANONICAL DECISIONS (the single source of truth)

These are the settled decisions. Several were finalized in design discussion and are recorded here for the first time — treat this list as authoritative.

### Platform & tech stack
- **Primary platform:** Meta Quest 3 / 3S. **Fast-follow:** Android XR (Galaxy XR). **Later:** Vision Pro (digital-twin path), Pico. **Entry tier:** mobile AR (ARKit/ARCore), handheld — never Google Cardboard.
- **Engine:** Unity 6 + Meta XR SDK, **OpenXR backend from day one** for portability.
- **Networking:** **Photon Fusion (locked)** — matches Meta's colocation samples; abstract behind a thin transport interface so a later swap (e.g., Unity NGO) is a module change.
- **Architecture doctrine (from roadmap):** headless deterministic pure-C# rules engine; **everything is a typed Intent**; server-authoritative; presentation is a dumb subscriber; the virtual-card renderer is core (not XR-specific).

### The game (identity & combat)
- **Aspect system:** 6 **grounded elemental** Aspects — Ember (Fire), Tide (Water), Bastion (Earth), Gale (Wind), Verdant (Life), Umbra (Shadow) — on an ally/enemy wheel. Each is *helpless at something* (identity = weakness). Universal cost: **monsters are your conduit** (no fielded monster → limited higher casting).
- **Reactions (element combos) and Oaths (self-restriction for power) are an ADVANCED optional layer** — a full duel plays without them; core must be fun without them.
- **Combat model (canonical = `combat-model.md` v0.2):** real-time, continuous, **commander-and-intelligent-creatures**. Player issues high-level **directives** (Attack/Defend/Hold/Retreat-Recall/Follow); each creature has its own **ability kit** (damage/range/wind-up/cooldown/AoE/preferred-targets) and a deterministic **combat AI** that picks abilities as the fight evolves. **Big HP, small per-hit damage → attrition** (no instant ATK-vs-ATK, no retaliation rule; mutual damage is emergent from two agents attacking). **Fewer, more valuable, longer-lived creatures**; **Recall/heal/revive** make them persistent assets, not turrets. Player is a **stationary Summoner-Caster**: feet planted, upper-body + gaze only, **no room-scale movement/dodging ever**; support kit (heal, recall, protect, interrupt, buff/debuff, danger-scaled Guard), telegraphed attacks answered by a planted gesture (Until You Fall). This **supersedes** the older "monsters carry damage via monster-vs-monster ATK" phrasing.
- **Casting ladder (canonical):** Sign (pose, fast, weak) → Word (spoken, medium) → Script (air-drawn sigil, slow, strong) → Ritual (Script+Word, slowest, strongest). Commitment = power; big casts telegraph themselves. Incantation lines are **printed on cards**; recognition needs only the name-kernel. Silent path always available.
- **Card handling (canonical):** only **two physical zones** — deck (draw) + hand. A **holographic duel-disk** renders all other zones (graveyard, set traps, life). Spent cards → a **physical discard pouch** that is a *tactile layer over a virtual-authoritative graveyard* (ritual mode = physically retrieve to revive; swift mode = tap hologram).
- **Deck rules:** exactly **30 cards, max 2 copies (legendaries 1)**. Primary Aspect ≥20 cards; one allied-Aspect splash ≤10; **opposed Aspects never share a deck**. Banlist philosophy: **nerf-first, ban-almost-never**, via **server-side live balancing of cost/stats on physical cards** (identity/text stay fixed). Collector's promise: *no owned card ever becomes unplayable.*
- **Progression (two lanes):** **PvE = full Solo-Leveling** power growth (mana pool, abilities). **PvP = identical baseline forever**; mastery grants **expression, not stats** (chantless casting as a side-grade, sigil styles, awakened summons after 100 duels, titles). **Money never buys a PvP number**; ≤60/40 starter-vs-whale telemetry guardrail.

### Voice (canonical = voice-architecture.md)
- Four registers: **Command** (grammar+gaze/E2E-SLU, deterministic), **Invocation** (kernel spotting), **Ritual** (prosody *envelope* — no transcription), **Conversation** (PvE LLM only).
- Competitive path: **E2E-SLU (primary) + grammar-constrained recognition (deterministic fallback/audit)**, both over a **per-frame game-state grammar**, on-device. **Never open transcription; never an LLM on the competitive path.** Compositional commands ("Dragon Flare, defend the left flank") supported; deictic targets ("attack *that one*") resolved by gaze (Put-That-There). Mic is **pose-gated** (reveal pose / cast gate), never always-on.

### Card visibility (canonical = OPTION B)
- **Option B — card replacement — is primary on all platforms:** track the real card's pose (via the fiducial we already use for identity) and render a **tracked virtual card locked onto it** (slightly oversized). Player holds/feels the real card; sees an enhanced virtual one (animated art, live server-balanced stats, face-down for opponents, transforms).
- **Hand occlusion is still required** (fingers must appear in front of the virtual card) — the easy, solved case.
- **Option A (show the real card via depth-occlusion) is the fallback / optional MR-authenticity mode**, not the default. The occlusion engineering in `card-visibility-engineering.md` remains valid for the fallback and for the required hand occlusion.
- Digital-twin cards (fully virtual, from a scanned collection) are the path for **remote play, Vision Pro, and travel** — the same renderer as Option B, just not anchored to a physical card.

### Card recognition
- **Fiducial-framed cards** (a machine-readable "summoning-rune" border encoding ID + orientation, ECC for partial occlusion), read via the **Passthrough Camera API** (Quest 3/3S) on its own thread (15–30 Hz, pose-interpolated). NFC for ownership/tournament check-in. Identity and occlusion are separate jobs sharing one pose estimate. Server-authoritative collection (vision is an input, not a source of truth).

### Lessons scheduled as gates (from War of Wizards & the graveyard)
- **Steal:** trajectory-drawn casting (Script tier) + on-screen rune guide + accuracy/size-scaled power **with a fail-floor**; mid-match "sky-draft" pickup layer; a rare **player-embodiment capstone** (earned, never bought).
- **Avoid:** stepping-as-survival (comfort killer); purchasable power (P2W); uncounterable burst (every effect must have a counter); unmoderated hot mics → **ship mute/report/clean-lobby day one**; platform favoritism.

---

## 4. Known open items (not yet designed)
World/lore & faction cultures; launch Clan list (the second creature axis); onboarding/tutorial flow; full PvE campaign; art-direction bible; monetization specifics; naming/branding. The M0–M1 technical spec is now DRAFTED (`xr-card-battle-m0-m1-technical-spec.md`, awaiting approval). See roadmap for when each is scheduled.

---

*Maintenance note: when a decision changes, update §3 here first, then the relevant doc. This file is the contract; the docs are the elaboration.*
