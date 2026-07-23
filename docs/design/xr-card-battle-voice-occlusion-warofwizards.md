# Fast Voice Models · War of Wizards Teardown · Card Occlusion — Research v0.1

*Three deep dives. Concrete numbers and shipping implementations throughout; speculation flagged as such.*

---

# PART 1 — Extremely fast LLM/SLM voice understanding

## The reframe: you want understanding, not transcription — and there's a named field for that

The cutting edge isn't "faster Whisper." It's **End-to-End Spoken Language Understanding (E2E-SLU)** — models that map audio *directly* to intent, skipping the text step entirely. This is precisely what you asked for ("understand the player's intent rather than simply transcribe words"), and the research is mature:

- **Why E2E-SLU fits us better than ASR→NLU:** cascaded pipelines suffer because ASR transcription errors poison the downstream intent model, and the text bottleneck *throws away* non-phonemic cues — pauses, intonation, emphasis — that carry meaning. E2E-SLU extracts intent from the raw signal, so it's **jointly optimized, lower-latency, smaller carbon/compute footprint (explicitly "attractive for on-device"), and it *uses* the emotion in "Dragon Flare, DESTROY them!" instead of choking on it.** That last point directly answers your original worry about emotional/fast speech.
- **Two-pass low-latency SLU** (Interspeech 2022 line of work) is the pattern for competitive play: pass 1 makes a fast intent guess from a few hundred ms of acoustics; pass 2 refines with fuller context. You get a sub-200ms provisional intent (enough to start the cast animation) confirmed a beat later — the same "predict then confirm" trick fighting games use for rollback.

## The architecture options, with real numbers (2026)

| Option | What it does | Latency (short clip) | On-device? | Determinism | Role for us |
|---|---|---|---|---|---|
| **Grammar-constrained ASR** (Vosk dynamic grammar) | Best path through per-frame grammar | sub-second, ~100–300ms | ✅ light | ✅ total | Competitive command path (baseline, ships today) |
| **E2E-SLU classifier** ⭐ | Audio → intent label + slots, over a closed intent set | **~50–150ms** (classification, not generation) | ✅ (small models, NPU-friendly) | ✅ (fixed label set) | Competitive command path (better feel, near-term) |
| **Small Language Model** (Gemma 3 1B, Phi-class, Qwen-small) | General reasoning / generation | Gemma 3 1B ≈ **2,585 tok/s prefill** on mobile GPU; generation slower | ⚠️ runs, but **NPU throttles in 2–3 min at max load** | ❌ unless constrained | PvE conversation; design-time authoring |
| **Constrained-decoding SLM** | SLM forced to emit only grammar-valid output (GBNF/function-calling) | higher than SLU; hundreds of ms+ | ⚠️ thermal risk in real-time | ✅ by construction | Future competitive path as NPUs improve |
| **Cloud LLM** (ACE/Inworld-lineage) | Full understanding + dialogue | **0.8–2.5s** typical | ❌ | ❌ | PvE story rival only; never ranked |

Hardware reality: Quest 3 / Galaxy XR / most Android XR run **Snapdragon XR2-class** silicon with a real but thermally-limited NPU — sustained max inference throttles in 2–3 minutes, which **rules out a continuously-running LLM as your command parser** but is completely fine for the SLU/grammar path (tiny, bursty, gated to ~1–3s windows a few times a minute). Vision Pro (M-class) has the most headroom; future XR silicon widens it.

## Recommendation (refines the v0.2 voice architecture, doesn't replace it)

Keep the layered hybrid. Make the **L2 recognizer a two-tier competitive core: grammar-constrained ASR as the guaranteed-deterministic baseline, with an E2E-SLU classifier as the primary intent path** (both consume the same per-frame game-state grammar/intent-set; the SLU gives better emotional-speech robustness and lower latency, the grammar-ASR is the fallback and the tie-breaker for disputes). This is a real upgrade over "keyword spotting," and both run on-device within the thermal budget because they're small, bursty, and gated.

- **Competitive path:** E2E-SLU (primary) + grammar-ASR (fallback/audit). Target: provisional intent <200ms, confirmed <400ms. Both deterministic over a closed set — mandatory for ranked fairness.
- **PvE path:** local SLM (Gemma/Phi-class) for the talking rival, constrained-decoded; cloud LLM allowed for non-latency-critical story beats. This is where the "opponent hears your trash talk and responds" fantasy lives (Phasmophobia's lesson).
- **Design-time:** an LLM offline authors and vets the grammars/intent sets and incantations — breadth without runtime nondeterminism.
- **Trajectory:** as on-device NPUs grow, the competitive L2 migrates toward constrained-decoding SLMs — natural-language freedom with determinism-by-construction — behind the same stable intent contract, so it's a module swap, not a redesign.

One-line: **classify intent from audio (E2E-SLU), constrain it with game state, confirm it deterministically — never run open transcription or an unconstrained LLM on the competitive path.**

---

# PART 2 — War of Wizards teardown (the most relevant comparable we've found)

*AltLab VR; Quest + Steam; a competitive VR spell-drawing MOBA. Steam 69% positive (140 reviews); Meta store rating notably higher (~92% cited). This game ran the experiment on several of our exact decisions — so it's worth studying closely.*

## How it actually works
- **Casting = drawing.** You physically draw a spell's symbol in the air with the controller; the system shape-matches it and casts instantly. **Power scales with how big and how accurately you draw** — a rushed small glyph is weak, a bold precise one is strong.
- **No-memorization aid:** in-game prompts show the shapes, so newcomers "focus on strategy without needing to memorize spell shapes." (Excellent onboarding decision — steal it.)
- **Targeting & structure:** it's a **MOBA/tower-defense** — draw spells to attack enemy towers, defend yours, and command minions.
- **Combat:** real-time PvP (1v1, 2v2) + PvE; you **physically dodge incoming attacks** by stepping side to side.
- **Direct player threat (your observation, confirmed):** summoned minions *and opponents* can attack you the player directly — combat is not purely minion-vs-minion.
- **Mics hot (your observation, confirmed):** player mics are open; opponents hear each other and communicate/banter physically in real time — social presence, not a voice-command system (voice isn't the casting input; drawing is).

## What players liked
- The **draw-to-cast mechanic** — repeatedly cited as unique, engaging, immersive; the core reason for the high ratings. **This validates our Script/sigil tier directly.**
- **Accuracy/size-based power** — skill expression that feels physical and fair.
- The in-game shape prompts — accessibility without dumbing down.
- Real-time PvP presence with voice — the social electricity of hearing your opponent.

## What players disliked (the goldmine)
- **Comfort rated 1/10 — the #1 complaint.** Physically drawing *while dodging* demands room; "if they don't have enough room… the whole experience doesn't feel as good." Exhausting, small-space-hostile.
- **Balance failures:** "some spells do upwards of 2500 damage with no counters whatsoever"; certain attacks are near-impossible to dodge.
- **2v2 skill-gap carrying:** weak matchmaking lets one player get stranded carrying a mismatched teammate.
- **Toxicity:** hot mics cut both ways — great banter, but "players with bad manners" with no buffer.

## Direct comparison to our design — what it proves

| Dimension | War of Wizards | Our design | Verdict |
|---|---|---|---|
| Cast by drawing | ✅ core, beloved | ✅ Script tier (one of four rungs) | **Validated.** Their success de-risks our sigil system |
| Memorization burden | Solved via prompts | Same (prompt/guide the 8 sigils) | Adopt their prompt approach |
| Power = draw quality | ✅ | ✅ (commitment ladder: bigger/slower = stronger) | Convergent — good sign |
| **Physical dodging** | ✅ **→ comfort 1/10, their worst complaint** | ❌ **we designed this away** (telegraphed, plant-your-feet, gesture-block) | **Their failure is our thesis, confirmed by real player data** |
| Small-space play | ✅ hostile | ✅ safe (stationary battlemage) | **Major differentiator** |
| Balance | ✅ 2500-dmg no-counter | Server-side live balancing + sim harness + no-uncounterable rule | Their pain = why our balance-math doc exists |
| Matchmaking | Weak (2v2 carry) | Rating-based + collection brackets + ≤60/40 guardrail | Learn from their gap |
| Hot mics | ✅ fun + toxic, no controls | Gated mic + we must add moderation/mute/report + optional voice-off | **Add: proximity-voice with mute/report; don't ship hot mics naked** |
| Physical cards + MR | ❌ VR-only, no cards | ✅ physical cards, MR-first | Our whole identity; they don't compete here |
| Direct player attacks | ✅ | ✅ (but defended by gesture, not dodge) | Same drama, comfortable delivery |

## The three lessons, sharp
1. **Draw-to-cast works and players love it — build it, but as *one rung of four*, not the whole game** (their casting is one-dimensional; our Sign/Word/Script/Ritual ladder gives range they lack).
2. **Physical dodging is the comfort killer — their lowest score, from real players, is the exact thing we removed.** This is the strongest external validation we've gotten for the stationary-battlemage decision. Keep attacks personal and dramatic; keep the *response* a planted gesture.
3. **Hot mics create presence AND toxicity — ship social presence with guardrails from day one** (mute, report, proximity falloff, a "clean-lobby" option). Don't discover moderation after launch.

Ideas to **avoid**: uncounterable burst spells (their 2500-dmg problem — every effect in our game needs a counter by the balance invariant); matchmaking that strands players; and making the *entire* input one gesture type.

---

# PART 3 — Physical cards, hands, and occlusion

## The key finding that changes the framing
There are **two different occlusion technologies**, and the naive one (hand-mesh occlusion) is the trap you correctly spotted:

- **Hand-mesh occlusion** — the system knows only the *tracked hand skeleton/mesh*. It draws hands in front of virtual content but **has no idea a card exists** → the card gets painted over. This is the failure mode you feared.
- **Environment depth occlusion (Meta Depth API)** ⭐ — provides a **real-time, per-eye, per-frame, per-pixel depth map of everything the cameras see**, from stereo. It occludes virtual content behind *any* real surface — "hands, people, limbs, pets… fast-moving objects" — **including a card you're holding, because the card is just real geometry in the depth map.** It doesn't need to *know* it's a card.

**So on Quest 3/3S the physical card is preserved essentially for free** — if we composite virtual content using **environment depth**, not hand-mesh occlusion. The card (and your fingers, and the table) stay visually correct against monsters and effects because they're closer in the depth buffer. This is shipping tech (Depth API, Quest 3-only, hard/soft occlusion modes).

### The real constraints (concrete, from Meta's docs)
- **Near-field breakdown < 0.2m:** depth is unreliable closer than ~20cm to the headset. A card held up *right at your face* will occlude poorly. **Design response:** the reveal/summon pose happens at ~30–50cm (natural reading distance — already in our sweet spot), and for cards that come very close, Meta's own guidance is a **fade/blend fallback** instead of hard depth — which we can style as a magical shimmer (turn the limitation into VFX, as we've done elsewhere).
- **Soft vs hard occlusion:** soft edges look right but cost more GPU; hard is cheap but jagged. Budget for soft on the card region, hard elsewhere.
- **Range > 4m degrades** — irrelevant for tabletop/arm's-reach cards.
- **`RemoveHands()` exists** — it strips hands *out* of the depth map; we want the opposite (keep hands+cards), so we simply don't call it.

## Where fiducials fit (they don't do occlusion — they do identity)
Fiducials and occlusion are **orthogonal**: Depth API handles *seeing* the card (occlusion); the fiducial frame handles *knowing which card it is and its pose* (recognition). You need both, for different jobs. Fiducials do not help occlusion and depth does not help identity. (This cleanly separates two concerns we'd previously bundled.)

## Should we reconstruct cards digitally instead? — Platform-dependent

| Platform | Card occlusion approach | Reconstruction needed? |
|---|---|---|
| **Quest 3/3S** | Environment **Depth API** → real card preserved for free | **No.** Keep the real card |
| **Android XR** | Verify per-device depth support; Snapdragon XR2 depth likely parallels Quest; fallback to depth-from-stereo | Probably no; confirm per device |
| **Vision Pro** | No equivalent open per-pixel dynamic depth-occlusion API for held objects today; image tracking is ~1Hz | **Yes — render a digital twin** of the card, pose-anchored, until Apple opens the APIs. This is the "VR/remote mode" build anyway |
| **Mobile AR** | Screen-composited; ARKit/ARCore depth (LiDAR/depth-from-motion) can occlude, but it's a phone screen — card is real in frame already | N/A (you're looking at a screen showing the real card) |

**Recommendation:** on Quest (primary) **preserve the real card via Depth API environment occlusion** — it's the honest, magical, zero-cost-per-card answer and it's why we chose Quest. Build the **digital-twin path as a first-class fallback** for Vision Pro and any platform lacking held-object depth — and note it *also* powers our proxy-deck accessory idea and remote play, so it's not throwaway work.

## Performance implications
Depth API costs GPU (soft occlusion more so) and it stacks on top of our passthrough-camera vision pipeline (card fiducial reading) plus creatures plus effects — on a mobile GPU rendering two eyes at 90Hz. Budgeting stance: run the depth-composite on the card/hand region every frame (it's cheap-ish and near-field where it matters), throttle full-scene soft occlusion, keep the fiducial reader on its own thread at 15–30Hz (identity doesn't need 90Hz — pose interpolates). This all lives inside the thermal envelope we've been budgeting for, but occlusion is now a named line-item in that budget.

---

## Sources
**Voice/SLM:** [E2E-SLU low-latency (arXiv 2011.04884)](https://arxiv.org/pdf/2011.04884) · [Two-pass low-latency SLU (arXiv 2207.06670)](https://arxiv.org/abs/2207.06670) · [edge/mobile LLM leaderboard 2026](https://awesomeagents.ai/leaderboards/edge-mobile-llm-leaderboard/) · [on-device LLM inference guide + thermal throttling](https://docs.octomil.com/blog/on-device-llm-inference-2025-2026/) · [NVIDIA ACE (latency/local inference)](https://developer.nvidia.com/ace-for-games)
**War of Wizards:** [Steam page](https://store.steampowered.com/app/2009460/War_of_Wizards/) · [vrpupu Quest review](https://vrpupu.com/en/2026/02/war-of-wizards-review-meta-quest-vr-moba/) · [VR Today review](https://vrtodaymagazine.com/war-of-wizards-vr-review/) · [AltLab VR](https://www.altlabvr.com/war-of-wizards)
**Occlusion:** [Meta Depth API overview (per-pixel, dynamic, <0.2m limit)](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/) · [Mesh+Depth believable MR](https://developers.meta.com/horizon/blog/mesh-depth-api-meta-quest-3-developers-mixed-reality/) · [Depth API dynamic occlusion (UploadVR)](https://www.uploadvr.com/quest-3-depth-api-mixed-reality-dynamic-occlusion/) · [occlusion shaders how-to](https://blackwhale.dev/meta-quest-depth-api-and-occlusion-shaders-for-environment-occlusion-in-mixed-reality/)
