> ⚠️ **STATUS — SUPERSEDED (read this first).** This is an early (v0.1) voice writeup. It is **replaced by `xr-card-battle-voice-architecture.md` (v0.2)**, which keeps this doc's core insight ("we never need open transcription") but corrects the architecture: voice has **four registers** (Command / Invocation / Ritual / Conversation), the competitive path is **E2E-SLU + grammar-constrained recognition fused with gaze** (not keyword spotting), and compositional commands ("Dragon Flare, defend the left flank") are handled. Use this doc only for historical context; treat voice-architecture.md as canonical.

---

# Voice System Architecture — v0.1

*How the game hears "Fireball!", full incantations, and nothing else. Research-backed design for maximum accuracy, minimal hallucination, on-device, real-time where it matters.*

---

## 0. The master insight: we never need open transcription

General speech-to-text is bad in real time because it solves an unbounded problem: *"what arbitrary sentence might this person be saying?"* Our game never has that problem:

- The command vocabulary is **closed** (~30–60 action words/phrases we author).
- During an incantation, the system already **knows the expected phrase** — the vision system saw which card was raised, and *we printed the incantation on that card*. Speech handling becomes **verification** ("did they say approximately this?"), not transcription.

Verification against a known phrase is orders of magnitude more reliable than open ASR. This single reframe eliminates the hallucination/error problem you've experienced with transcription AIs. We also *author* the vocabulary — so every incantation and command is designed to be phonetically distinct from every other (no "Ignite"/"Invite" confusions; that's a content-design rule, enforced at card creation).

## 1. Two tiers of listening

### Tier 1 — Instant commands (real-time, <100ms)
For reflex words during combat: **"Shield!" "Strike!" "Hold!"** plus Sign-accompanying words.
- **Tech: on-device keyword spotting / speech-to-intent — NOT transcription.** Picovoice's engines are the benchmark here: >99% accuracy clean, 97% in 9dB noise, entirely on-device, tiny compute, Unity SDK available. ([docs](https://picovoice.ai/docs/faq/rhino/), [Unity quickstart](https://picovoice.ai/docs/quick-start/porcupine-unity/))
- It never produces text — it produces *intents*, matched only against our closed vocabulary. Nothing to hallucinate.
- This tier can run continuously during a duel at negligible cost (wake-word-class models are designed for always-on listening).

### Tier 2 — Incantations (gated capture → whole-utterance verification)
Exactly your proposal: open the gate, let the player speak the **complete** incantation, close the gate, process the whole audio at once.
1. **Gate opens** (button or gesture — §2). Mic capture begins. A visual "listening" rune confirms it.
2. Player recites: *"From cinder and wrath — Emberdrake, ignite!"*
3. **Gate closes** (release/drop) + voice-activity endpoint detection trims silence.
4. The complete utterance is scored **against the known expected phrase** — phonetic similarity matching / forced alignment, not free decoding. Pass threshold → summon fires with the Word/Ritual bonus. Borderline → fires anyway at base power (never punish the player with a failed summon over ASR doubt; the bonus is what's at stake, not the action).

**Why whole-utterance beats streaming here (your instinct, confirmed by research):** streaming ASR commits to partial guesses and compounds errors; whole-utterance processing uses full context. And the known-phrase matching sidesteps Whisper-class models' documented failure mode — they hallucinate on short, padded, or silent audio (famously inserting YouTube-caption phrases like "Thanks for watching" into silence — [investigated here](https://www.researchgate.net/publication/388232036_Investigation_of_Whisper_ASR_Hallucinations_Induced_by_Non-Speech_Audio)). Gated capture + VAD trimming + verification-not-transcription means that entire failure class can't occur.

**All on-device.** No cloud round-trip: latency (a summon can't wait 800ms on a server), privacy (a live mic in someone's living room must not stream anywhere — also our store-policy stance), and offline/tournament reliability. This rules out Meta's Voice SDK (Wit.ai) for the core loop — it's server-side with latency and no auto voice detection ([overview](https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/)). On-device Whisper-tiny via Unity Sentis runs on Quest 3 but with frame-rate cost ([test report](https://www.topview.ai/blog/detail/offline-speech-recognition-on-meta-quest-testing-unity-sentis-whisper-ai)) — usable as the Tier 2 matcher if we need it, but a lightweight phonetic-matching model is preferred (smaller, faster, can't hallucinate).

## 2. Gating the mic: per-device design

### With controller (Quest) — push-to-talk, and your card-juggling solution works
Wrist strap = the answer, exactly as you described. Controller dangles from the strap when both hands are on cards; grab it to point, draw, or press.
- **Hold trigger/button = mic open; release = utterance complete.** Push-to-talk is the most reliable gate in all of voice UX — zero false activations, crisp endpoints, and it doubles as the "activate the writing" gate for Script mode.
- Controller also gives: precise pointing (target my monster → their monster), laser-clean sigil drawing, haptic feedback on card slams/blocks, and physical buttons for draw/menu. Real advantages.
- **Revised stance from earlier docs:** not "hand-tracking-only" — **hand-tracking-first, controller-optional.** Two fully supported input profiles. Every controller function has a hand-tracking equivalent (required for Vision Pro anyway). Playtest for input-speed parity; neither profile may confer a competitive advantage in ranked.

### Pure hand tracking (Vision Pro / controllerless Quest) — the pose IS the gate
Elegant, and again your instinct: **the card-reveal pose opens the mic.**
- **Summons:** raising the card into the reveal pose = gate opens (the vision system is already watching for exactly this pose — one event, two systems). Speak the incantation. **Lower the card = gate closes**, utterance processes whole.
- **Spells/Script:** the palm-up "activation" gesture from the casting doc = gate opens; closing the fist or completing the sigil = gate closes.
- **Tier 1 reflex words** need no gate (keyword spotting is always-on and hallucination-proof by construction), but respond only during active combat states — context-gating as a second filter.

## 3. When do we actually need real-time streaming? (Almost never — one exception)

Fast responses come from Tier 1 intents (<100ms, no transcription). Incantations are Tier 2 (whole-utterance). The one place near-real-time processing earns its keep: **live chant feedback** — the summoning circle glowing brighter *as you recite*, syllable by syllable. Implementation: per-segment keyword spotting on the incantation's 2–3 authored phrase chunks (each chunk lights a ring segment) — spectacle of streaming, reliability of keyword spotting, still no transcription.

## 4. Failure design (what happens when hearing fails)

- **Never block gameplay on voice.** Voice grants *bonuses* (Word 1.3×, Ritual seal); the underlying action always has a silent path (placement/Sign). Mishears cost you a bonus, never a turn — so even a 5% error rate is emotionally survivable.
- **Borderline scores resolve in the player's favor** in casual; stricter thresholds in ranked (where chantless mastery exists precisely so top players can skip voice variance).
- **Noisy environments:** the 97%-at-9dB figure covers living rooms; tournaments are loud — Quest's beamforming mic array + per-venue threshold calibration + the always-available silent path. Accents: keyword/phonetic matching is more accent-tolerant than open ASR, but test across accents from day one; incantation authoring avoids phoneme clusters known to vary heavily across English accents (and localized incantations per language at launch).
- **Two players in one room (colocated duels):** each headset's mic gates only on *its wearer's* gate-pose/button, and near-field mic beamforming rejects the across-the-table voice; the server also sanity-checks that a summon claim matches a card *that player* revealed.

## 5. Summary architecture

| Layer | Tech | Latency | Failure mode |
|---|---|---|---|
| Reflex commands | On-device speech-to-intent (closed vocab) | <100ms | None visible (intent or nothing) |
| Incantations | Gated whole-utterance capture → phonetic verification vs known phrase | ~200–400ms after gate close | Bonus not granted; action still fires |
| Chant feedback | Per-segment keyword spotting | streaming feel | Ring lags; cosmetic only |
| Mic gating | PTT button (controller) / reveal & palm poses (hands) | — | No open-mic hallucination possible |
| Cloud | **Nothing in the core loop** | — | Offline-proof, privacy-clean |

*The one-line summary: closed vocabulary + gated capture + verify-against-known-phrase + on-device everything = the "best of the best" accuracy you want, achieved by never asking the hard question that makes voice AI bad.*
