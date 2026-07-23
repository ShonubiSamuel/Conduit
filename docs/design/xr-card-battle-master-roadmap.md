# Master Development Roadmap — From Empty Project to Launch

*Technical-director plan. Organizing principle: **every MVP retires the single biggest remaining risk while costing the least to throw away if it fails.** Hardware enters late; fun enters first. Assumes a small team (2–5 people); durations are rough relative sizes, not promises.*

---

## 0. The architecture doctrine (read before the MVPs)

Five rules that make the whole sequence work. Violating any of them mid-project is what turns "port to XR" into "rewrite for XR."

**R1 — The rules engine is a headless, deterministic, pure-C# library.** No Unity types, no rendering, no input, no clock of its own. It consumes a stream of timestamped **Intents** and emits a stream of **GameEvents**. Same input stream → bit-identical output, always (this gives us replays, server validation, and simulation-testing for free). This is the single most important architectural decision in the project.

**R2 — Everything is an Intent.** Mouse click, voice command, raised card, gesture, AI decision, network message — all reduce to the same typed schema (`{actor, verb, target, cardId, timestamp}`) we already designed in the voice-architecture doc. The engine never knows *how* an intent was produced. This is why desktop-first isn't a detour: every input modality we add later is a new *producer* plugged into an unchanged consumer.

**R3 — Server-authoritative from the first networked build.** The engine runs on the server (or host); clients send Intents and render Events. Recognition errors, vision glitches, and cheating all become "rejected intents," never corrupted state.

**R4 — Presentation is a dumb subscriber.** The Unity layer renders GameEvents and forwards Intents. Desktop 2D, MR tabletop, VR arena are three *skins* over one engine — the "three camera distances" design made literal in code.

**R5 — The virtual card renderer is core, not XR-specific.** Option B (card replacement) means every platform renders virtual cards — desktop shows them on screen, XR paints them over real cards. Build the card visual system once, in the desktop phase, at shipping quality.

---

## MVP sequence at a glance

```
M0 Paper & Sim ─► M1 Headless Engine ─► M2 Desktop Playable ─► M3 Desktop Netplay
                                              │                        │
                                              ▼                        ▼
                                    M4 Voice on Desktop      (backend v1 grows here)
                                              │
                                              ▼
                              M5 First XR Build (virtual cards)
                                              │
                                              ▼
                              M6 Physical Cards (the magic moment)
                                              │
                                              ▼
                              M7 Colocated Duel (the vision slice)
                                              │
                                              ▼
                              M8 Spectacle & Content (dial, sigils, creatures)
                                              │
                                              ▼
                              M9 Closed Alpha / Live Service
                                              │
                                              ▼
                              M10 Launch
```

Risk burn-down order: **fun → rules integrity → netcode → voice → XR ergonomics → computer vision → colocation → content scale → live ops.** Each is the cheapest point at which that risk can be killed.

---

## M0 — Paper & Simulation Foundation *(1–2 weeks, ongoing forever)*

**Objective:** validate the *economy and counterplay structure* before writing engine code, and stand up the simulation harness as permanent infrastructure.
**Includes:** ~30 hand-written proxy cards; tabletop playtests of the Aspect wheel, energy costs, and counter relationships (turn-based approximation — accepted limitation: paper cannot test real-time pacing); porting the balance-doc duel simulator into a proper repo with CI.
**Excludes:** any Unity work, any art.
**Why it exists:** card math errors found here cost a pencil eraser; found in M6 they cost a reprint. The sim harness becomes the CI gate every future card change runs through (10k duels/night, telemetry bands from the balance doc).
**Risks retired:** degenerate Aspect matchups; broken cost curve; the knife-edge findings from our sims recurring in the real design.

## M1 — Headless Card Engine *(3–5 weeks)*

**Objective:** the deterministic rules kernel (R1) — the project's crown jewel.
**Includes:** full state machine (energy regen, summon/cast/command/interrupt intents, WEGO-with-interrupts timing, stabilizers: surge, danger-scaled defense, per-defender blocking); Intent/Event schemas (R2); deterministic replay files; headless AI-vs-AI runner wired to the M0 sim harness; unit tests as the rules' source of truth.
**Excludes:** graphics, audio, networking, Unity (pure .NET library + CLI).
**Why it exists:** every later system — netcode, voice, vision, AI — plugs into this. Building it headless forces the discipline that makes all later platforms cheap.
**Risks retired:** rules ambiguity (if it can't be coded deterministically, it isn't a rule); real-time pacing math (the sim now runs the *actual* engine, not a model).
**Dependency:** M0's validated numbers.

## M2 — Desktop Playable: "the ugly fun test" *(4–6 weeks)*

**Objective:** answer the only question that matters: **is this fun for a human?**
**Includes:** Unity project consuming the engine as a package; mouse-driven play — click/drag cards from a hand fan, click targets, hold-to-channel casts (mouse-gesture stand-ins for Signs/Scripts); the **card visual system at shipping quality** (R5 — frame, Aspect gems, live stats, animations: this is the actual card renderer XR will use); crude 2.5D battlefield with placeholder creatures (cubes with health bars are fine); play vs the M1 AI.
**Excludes:** multiplayer, voice, XR, real art, sound polish.
**Why it exists:** War of Wizards, Demeo, and every good game were fun ugly first. If energy pacing, interrupt windows, and the Aspect wheel don't create tension with a mouse, no amount of MR will save them; if they do, everything after is amplification.
**Risks retired:** the fun risk (the biggest one in the entire project); UI readability of simultaneous play; interrupt-window timing feel.
**Dependency:** M1. **Gate:** external playtesters ask for "one more game" unprompted — the Demeo test. Do not proceed on internal enthusiasm alone.

## M3 — Desktop Netplay *(3–4 weeks)*

**Objective:** two humans, two machines, one authoritative duel.
**Includes:** Photon Fusion transport behind a thin interface (per the networking decision); host/server-authoritative engine (R3); simultaneous-resolution netcode (the sim-proven fairness requirement); friend-code matches; reconnect/resume from replay stream; **backend v1**: accounts, deck storage, match history (boring managed-cloud stack).
**Excludes:** ranked matchmaking, spectators, colocation.
**Why it exists:** netcode assumptions poison architectures when added late. Doing it on desktop isolates network risk from XR risk — debugging desync with two windows on one desk instead of two headsets on two heads.
**Risks retired:** desync, authority model, intent-latency feel over real networks (do interrupt windows survive 80ms ping?).
**Dependency:** M2's validated game. Backend work starts here and grows continuously through M10.

## M4 — Voice on Desktop *(3–4 weeks, parallelizable with M3)*

**Objective:** prove the entire voice architecture with zero XR variables.
**Includes:** the layered stack from the voice doc — push-to-talk key as the mic gate (stand-in for the reveal pose), per-frame grammar compiler from game state, Vosk-class constrained recognizer producing the *same Intents as the mouse* (R2's payoff made visible), kernel + command registers, disambiguation and silent-fallback UX, recognition telemetry (accuracy/latency dashboards). E2E-SLU prototype as a parallel track if capacity allows.
**Excludes:** ritual/prosody register (needs the theater of XR to matter), PvE LLM conversation.
**Why it exists:** your call — voice is XR-independent, and iterating recognition on a desktop with a debugger is 10× faster than in a headset. When XR arrives, voice is a *solved subsystem* that just swaps its gate.
**Risks retired:** recognizer accuracy under emotional speech; grammar-compiler correctness; the fallback UX (does voice failure feel like a shrug or a rage-quit?).
**Dependency:** M2 (needs a game to command). M3 not required (voice works vs AI).

## M5 — First XR Build: virtual cards in MR *(5–7 weeks)*

**Objective:** the game is comfortable and playable on Quest 3 — **without physical cards yet.**
**Includes:** MR passthrough tabletop + standing modes; table/floor anchoring with the consecration ritual; hand tracking + gaze targeting; the stationary battlemage kit (Sign poses, gesture blocks, wrist life-gauge); **digital-twin cards** — the M2 card renderer floating as a pinchable virtual hand/fan (this IS the remote-play and Vision Pro card path, built once); voice with the pose-based mic gate replacing the PTT key; thermal/perf budget instrumentation from day one.
**Excludes:** physical cards, camera access, colocation, VR arenas.
**Why it exists:** separates **XR ergonomics risk** (comfort, gesture fatigue, gaze UX, thermal ceiling) from **computer-vision risk** (card reading). If reach, dwell times, or comfort are wrong, we find out with virtual cards in days, not tangled up with fiducial tuning.
**Risks retired:** comfort (the War-of-Wizards killer), interaction ergonomics, Quest thermal envelope with creatures + anchors + voice running.
**Dependency:** M2 (presentation), M4 (voice). **Note:** this build is *itself* a shippable fallback — it's the remote-play mode of the final game.

## M6 — Physical Cards: the magic moment *(5–8 weeks, highest-risk milestone)*

**Objective:** a real printed card, raised in the reveal pose, summons a monster. The product's soul, isolated and proven.
**Includes:** fiducial frame design + small proto print run (matte + foil variants — test the enemy early); Passthrough Camera API reader (ArUco-lineage, own thread, 15–30Hz, pose interpolation); the five-signal intent stack (facing/elevation/isolation/dwell/gaze) with the charging-glow UX; **Option B rendering** — virtual card painted over the tracked physical card, oversized margin, first-party hand-mesh occlusion putting fingers back on top; deck scan-in flow (companion of the consecration ritual); recognition telemetry (rate, latency, false-positive count per session).
**Excludes:** colocation, opponent's cards (single-player vs AI is fine here).
**Why it exists:** this is the highest *pure-technical* risk in the project and the reason the game exists. It gets its own milestone so nothing else is blamed when tuning gets hard.
**Risks retired:** recognition robustness (glare, sleeves, fans, occlusion by fingers); false-positive summons (the trust-killer); registration quality of card replacement (the "swimming sticker" failure).
**Dependency:** M5 (the XR shell), M2 (card renderer). **Gate:** <1 false summon per session; >95% successful presents within 1.5s; playtesters describe the summon as "magic" unprompted.

## M7 — Colocated Duel: the vision slice *(4–6 weeks)*

**Objective:** two people, one room, real cards, monsters between them — the trailer, the pitch, the whole dream, running.
**Includes:** Colocation Discovery + Shared Spatial Anchors; two-headset session flow (the full consecration ceremony for two); M3 netcode over local sessions; per-headset card reading reconciled by the authoritative engine; spectator phone app v0 (same anchored scene through ARCore/ARKit — the marketing engine begins here).
**Excludes:** ranked, tournaments, remote-vs-colocated mixed sessions (later).
**Why it exists:** this is the milestone that proves the *product*, not a system. It's also the funding/partner artifact — everything before it was engineering; this is the demo that makes people gasp.
**Risks retired:** shared-frame accuracy (do both players see the monster in the same spot?); dual-headset session friction (the 5-minute-setup death); social loop ("do strangers finish a duel and go again?" — the M2 gate, now in headsets).
**Dependency:** M3 + M5 + M6 converge here. This is the "vertical slice" from the original report, now properly de-risked by everything before it.

## M8 — Spectacle & Content Scale *(8–12 weeks, overlaps M9)*

**Objective:** from tech demo to *game* — the fantasy at full volume, and the content pipeline proven at rate.
**Includes:** the environment dial (field-spell MR→VR transformation with card/hand preservation via Option B + hand occlusion); Script/sigil casting (trajectory-drawn, War-of-Wizards-validated, with fail-floor + on-screen guides); ritual summons with the prosody envelope; the AI creature pipeline run in anger (target: 60 creatures — commons via Tripo/Meshy flow, legendaries hand-finished; the art-direction bible enforced); spatial audio + the announcer; Oaths and Reactions (the advanced layer, entering last as designed).
**Excludes:** full 300-card set (that's launch-scale), monetization.
**Why it exists:** content-pipeline throughput is a *production* risk, not a technical one — it needs its own milestone to measure cost-per-creature honestly. Spectacle lands here because it amplifies a proven loop instead of masking an unproven one.
**Risks retired:** cost/time per creature at quality; thermal budget under full VFX; whether Reactions/Oaths deepen or clutter (telemetry decides).
**Dependency:** M7. Content work parallelizes with M9's backend work naturally.

## M9 — Closed Alpha & Live Service *(6–10 weeks, continuous)*

**Objective:** strangers playing nightly on infrastructure that can take a patch without a client update.
**Includes:** ranked matchmaking (rating + collection brackets + newbie preconstructed lobbies — the War-of-Wizards matchmaking lesson); **live balancing** (server-side card values, the collector's promise); telemetry bands wired to alerts (winrates, duel length, platform parity, card inclusion rates); moderation stack (mute/report/clean-lobby — shipped *before* strangers meet, the WoW toxicity lesson); remote play polish; mobile companion v1 (scan-in, deck building, spectating); onboarding/tutorial (teach summon → command → block in 10 minutes); alpha cohort of 50–200 external players under NDA.
**Excludes:** monetization mechanics (alpha is free; economy telemetry only).
**Why it exists:** every live-game lesson says ops maturity gates launch more than features do. The alpha cohort also generates the balance data our bands need before ranked means anything.
**Risks retired:** retention (do people *come back*?), live-patch pipeline, moderation load, backend cost curve.
**Dependency:** M7 minimum; M8 content flows in continuously.

## M10 — Launch *(timeline set by print logistics as much as code)*

**Objective:** first public release — Quest store + physical starter product.
**Includes:** 200–300 card launch set through the pipeline; physical print run (starter decks + boosters, direct-to-consumer first — the Lightseekers economics lesson: small runs, event-driven); store submission (camera-permission review prepared with the diegetic consecration flow); monetization live (two-lane rule enforced: cards/cosmetics/PvE only); launch trailer (shot inside M7's colocated mode — the game *is* the trailer); Android XR port branch begins (OpenXR discipline pays out here).
**Why it exists:** it's the goal. Everything above was sequenced so that by now, launch is *integration and logistics*, not invention.

---

## Dependency & foundation map

### Build as permanent foundations (start early, never throw away)
| System | Built in | Why foundational |
|---|---|---|
| Headless rules engine + Intent/Event schema | M1 | Everything consumes it (R1/R2) |
| Simulation/CI harness | M0–M1 | Gates every card change forever |
| Card visual system | M2 | Ships on every platform (R5/Option B) |
| Transport-abstracted netcode + server authority | M3 | All multiplayer modes reuse it |
| Voice stack (gate-agnostic) | M4 | Gate swaps per platform; core never changes |
| Backend (accounts/collection/telemetry) | M3→M9 | Grows continuously; live balancing depends on it |
| Digital-twin card hand | M5 | Remote play + Vision Pro + fallback, forever |

### Deliberately deferred (waiting is the optimization)
| System | Enters | Why it waits |
|---|---|---|
| Computer vision / fiducial reading | M6 | Needs the XR shell; isolated from ergonomics risk |
| Colocation | M7 | Needs netcode + XR + vision all proven |
| VR arenas / environment dial | M8 | Amplifies a proven loop; pointless before fun is proven |
| Oaths & Reactions | M8 | Advanced layer by design; core must be fun without them |
| AI content pipeline at scale | M8 | Prove cost-per-creature on 60 before committing to 300 |
| PvE LLM rival, prosody rituals | M8–M9 | Highest-tech, lowest-risk-to-defer features |
| Monetization | M10 | Economy telemetry first, pricing last |
| Vision Pro / Pico ports | Post-launch | Blocked on platform APIs, not on us |

### The categorical split you asked for
- **Core architecture (M0–M1):** rules engine, Intent schema, determinism, sim harness.
- **Gameplay systems (M2, M8):** presentation, card visuals, battlefield, spectacle, advanced mechanics.
- **Networking (M3, M7):** transport, authority, colocation, spectators.
- **Voice (M4, M8):** grammar/SLU stack early; ritual/LLM registers late.
- **XR integration (M5):** anchors, hands, gaze, comfort, thermal.
- **Physical cards + CV (M6):** fiducials, intent stack, Option B replacement.
- **Multiplayer at scale (M9):** matchmaking, ranked, moderation.
- **Backend (M3→M9):** accounts → collection → telemetry → live balancing → live ops.
- **Polish/production (M8–M10):** content scale, audio, onboarding, trailer, print logistics.

---

## Why this order is the smart one (the argument, compressed)

1. **The fun risk dies first and cheapest** (M0–M2). It's the only risk that kills the project with certainty, and it needs zero hardware.
2. **Each hardware layer enters alone.** Netcode without XR (M3), voice without XR (M4), XR without vision (M5), vision without multiplayer (M6), everything together only at M7. When something breaks, you know which layer broke — the debugging-cost argument for this whole sequence.
3. **Option B collapsed the platform stack.** Because cards are *rendered* everywhere, desktop card UI, XR replacement cards, digital twins, and remote play are one system built once in M2. The roadmap exploits that aggressively.
4. **Every milestone leaves a shippable artifact.** M2 is a playtest build, M5 is remote-play mode, M6 is a solo MR game, M7 is the pitch demo. If funding, team, or platform realities shift, we hold a working product at every stage — no five-milestone dark tunnel.
5. **The graveyard lessons are scheduled, not remembered.** WoW's comfort failure → M5 gates; its economy → M10's two-lane rule; its toxicity → M9's day-one moderation; Lightseekers' economics → M10's print strategy; the sim knife-edge → M0's permanent harness.

*Suggested next artifact: M0–M1 technical specification — the Intent/Event schema, engine state machine, and repo structure — i.e., the first document an engineer implements from.*
