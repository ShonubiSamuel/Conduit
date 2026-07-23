> ⚠️ **STATUS — PARTIALLY SUPERSEDED (read this first).** The **voice/speech recommendation in §1 of this doc (keyword spotting) is OUTDATED.** Canonical voice design is now `xr-card-battle-voice-architecture.md` (v0.2: four registers + E2E-SLU/grammar hybrid + gaze fusion). The rest of this doc — **controllers vs hand tracking (§3), physical card handling intent stack (§4), spellcasting precedents (§5), the input state machine (§7), and cross-platform matrix (§8) — remains current and canonical.** Read §1 only through the lens of voice-architecture.md.

---

# Interaction System Design — Research-Backed v0.1

*Voice, hands, controllers, and cards: the complete input architecture, grounded in shipping products, published benchmarks, and documented player reception. Every recommendation cites its precedent and its limits.*

---

## 1. Speech Recognition: the architecture decision

### State of the art, mapped to our constraint (a headset, real-time, competitive)

| Approach | Exemplar | Latency | Accuracy | On-device? | Verdict for us |
|---|---|---|---|---|---|
| **Full cloud STT/NLU** | Meta Voice SDK (Wit.ai), Azure, Deepgram, Google | 300ms–1s+ network | High | No | Rejected for core gameplay: network dependency + latency spikes + privacy optics of streaming a living-room mic to cloud |
| **Full on-device STT** | Whisper (tiny) via Unity Sentis/whisper.unity on Quest 3 | Seconds of processing delay; documented frame-rate drops | Good | Yes | Rejected for live play: proven to run, but costs the exact GPU/thermal budget our passthrough vision pipeline needs |
| **On-device offline STT (lighter)** | Vosk — ships in Phasmophobia, runs locally per-player even in crossplay | Sub-second | Moderate ("generally lower than cloud") | Yes | Viable fallback; battle-proven in a hit game |
| **On-device keyword/phrase spotting** ⭐ | Picovoice Porcupine/Rhino: 97.1% wake-word accuracy at 1 false alarm/10hrs in 10dB noise; 97% command accuracy in noisy environments, beating Amazon Lex & Google Dialogflow | Tens of ms | **Highest for small vocabularies** | Yes, low power | **Core choice** |

### The decision, and why it's near-forced
**Competitive play uses on-device keyword/phrase spotting over a small, closed vocabulary (spell names + incantation lines), never open transcription.** The reasoning:

- Recognizing a limited word set is a categorically easier ML problem than open speech — that's why KWS hits 97% in noise while full STT degrades with speed and emotion (your exact worry: players *will* shout "EMBERDRAKE, IGNITE!" with emotion; open STT chokes on that, but a phrase-spotter trained on that phrase thrives).
- Our vocabulary is naturally closed: ~30 cards in a deck = at most ~40 valid utterances *in any game state* — and the game state shrinks it further (only cards actually in your hand are valid summons → the recognizer can be *biased toward 5 candidates at any moment*). This is a luxury general assistants never have.
- Design the incantation lines to be **phonetically distant from each other** (a solvable constraint since we're inventing the words — In Verbis Virtus even built a fictional language, Maha'ki, partly for this).

**Your segment-then-transcribe instinct is correct and is the standard trick:** don't run continuous recognition — gate the mic (see §2), capture the 1–3s utterance as a complete segment, recognize the segment against the candidate set. Segmented recognition with known vocabulary + known start point is dramatically more reliable than open streaming, and it eliminates false positives from table talk by construction.

**Fallbacks, in order:** phrase-spotter (core) → Vosk on-device (longer phrases, PvE dialogue) → cloud (nothing latency-critical; maybe post-match voice notes). And per the combat design: **voice is never the only path** — every voice action has a Sign/touch equivalent, so recognition failure degrades to a silent input, never to a lost turn.

## 2. Voice Activation: the mic gate

Research tradeoffs (wake word vs push-to-talk): wake words are hands-free but impose a mandatory phrase per interaction + a known false-accept rate on phonetically similar speech; PTT has near-zero false accepts but occupies a hand/button. Precedent worth copying: **Skyrim's Kinect shouts used hold-button-while-speaking** (PTT), and **In Verbis Virtus raised the character's hand (held mouse) to open the mic** — both gate by *deliberate physical act*.

**Our answer — the pose IS the push-to-talk.** We have something Skyrim didn't: the anime already gives us a deliberate, unambiguous physical act. The mic opens only when:

- **Summons:** a card enters the **reveal pose** (§4) — card raised, face-out, dwell ≥0.5s. Mic opens for a 3s window, biased to that card's incantation + generic commands.
- **Spells (support casts):** the **Script activation gate** from the casting doc (off-hand palm-up) or the Sign that begins a cast opens a 2s window for the spell's Word.
- **Always-off otherwise.** A glowing **mic-rune on your duel-disk** shows open/closed state — visible to *both* players (privacy transparency as diegetic UI; same philosophy as hardware mic LEDs).

Why hybrid-gated beats each pure approach for us: no wake-phrase overhead ("Hey game, summon—" would murder the fantasy); no idle listening (privacy + zero false accepts from table banter — critical since our game *encourages* trash talk); no extra button (hands are busy); and the gate itself is theater (raising the card dramatically is *literally* how you turn the mic on — mechanics and fantasy are the same act).

Cost/limit acknowledged: gated windows mean a player who speaks *before* the gate opens gets missed — mitigated by generous window timing and the rune's clear open state. Playtest the window lengths.

## 3. Controllers vs Hand Tracking

### The platform matrix forces the core answer
Vision Pro has **no controllers**; mobile AR has **no controllers**; Android XR ships them optional. Only Quest has first-class controllers. Therefore: **hand tracking + gaze + voice is the universal base layer; controllers are a Quest-tier enhancement, never required.** (This is also the Demeo lesson — its cross-platform reach is a retention pillar, and it plays hands-only happily.)

### What research says about mixing them
Meta ships two relevant capabilities: OS-level **Auto Switch** (put controllers down → hands take over; pick up → controllers resume) and the **Multimodal API** (v68+): *simultaneous* tracking of hands and controllers, including knowing whether a controller is currently in-hand. The Multimodal API exists precisely because Auto Switch's mode-flip has delay and makes idle controllers vanish — i.e., Meta built the exact primitive our "controller dangling from the wrist strap while holding cards" scenario needs.

**Verdict on the dangling-controller pattern: practical, with one caveat.** Sensors confirm in-hand vs dangling; a dangling Touch controller is light enough to release mid-session (this is common player behavior in mixed passthrough apps). The caveat is *re-acquisition fumble time* (~1–2s to re-grip) — so nothing time-critical may ever require grabbing a controller. That constraint plus the platform matrix yields the role split:

| Interaction | Best input | Why |
|---|---|---|
| Holding/fanning/drawing physical cards | Bare hands (obviously) | The product's soul |
| Presenting a card to summon | Bare hand + pose | The reveal is a hand act |
| Target selection (which monster attacks what) | **Gaze + pinch** (universal) / controller ray as Quest option | Gaze-ray is fastest cross-platform pointer; controller ray is more precise for players who prefer it |
| Signs (quick casts) | Bare hands only | Poses are the mechanic |
| Script sigils (air-drawing) | Bare hand fingertip; controller tip *allowed* on Quest | Controller gives steadier strokes (The Wizards' gesture-miss complaints were controller-era; our strokes are simpler — see §5) |
| Mic gating | Pose-based (§2) | Never a button — buttons aren't universal |
| Menus/deck-building outside duels | Whatever's in hand | Non-competitive, no constraint |

**Should controllers make you faster?** No — *different feel, equal ceiling.* If controllers were mechanically faster, Vision Pro/mobile players sit at a competitive disadvantage and the "hands and cards" identity erodes into controller meta. Controllers may offer *comfort* (steadier sigil strokes, haptic feedback buzz on cast confirm — genuinely valuable), never exclusive speed. This mirrors how cross-platform shooters treat mouse-vs-gamepad: normalize or die.

## 4. Physical Card Handling: intent from motion

### The problem, stated precisely
The camera sees cards constantly — fanned in hand, sorted, fidgeted, glanced at. The system must fire summons *only* on intent. False-positive summons are the single worst failure this game can have (worse than a miss — a miss is retried in 1s; a mis-summon spends energy and breaks trust in the whole platform).

### The intent stack (no single signal decides)
Every fiducial-framed card gives us continuous **pose**: position, orientation (which way the face points), and stability. Intent = conjunction of cheap, robust signals:

1. **Facing:** card face oriented *away from player, toward field/opponent* (normal-vector test). A fanned hand has faces toward *you* — automatically inert. This one geometric test kills ~90% of false positives for free.
2. **Elevation:** card raised above a calibrated "hand plane" (set during the consecration ritual — we already calibrate the player's comfort zone there).
3. **Isolation:** one dominant card — presented card is spatially separated from the fan cluster (fan = multiple fiducials in a tight cluster; the system treats any card *within* the cluster as "held," never "presented").
4. **Dwell:** stable pose for 500–700ms. This is the classic dwell-select pattern from gaze-UI research, applied to an object.
5. **(Optional strictness, ranked mode) Gaze agreement:** player's head/eye gaze on the field or the card — not down at their lap.

**Feedback makes dwell feel instant instead of laggy (the critical trick):** the moment signals 1–3 hold, the card's rune-ring **begins to glow and fill** (a progress affordance, like a hold-to-confirm button); at full fill a chime + haptic-tone plays and the mic gate opens. The player never experiences "delay" — they experience *charging the card*, which is (again) the anime fantasy doing UX work. Cancel = lower or flip the card; the glow drains. 

Your scenario walkthrough: *holding five* → cluster, faces inward: inert. *Fanning/sorting* → cluster churn: inert (motion instability also blocks dwell). *Raises one* → leaves cluster, still face-in: inert until rotated. *Rotates it outward* → facing + elevation + isolation true → glow starts → dwell completes → mic opens → "Emberdrake, ignite!" *Partially hidden* (fingers over corner) → fiducial ring is redundant/error-corrected (encode ID with ECC across the ring segments; ArUco-style markers tolerate partial occlusion) — glow simply pauses if confidence drops, resumes when grip shifts. *Looking elsewhere while presenting* → casual mode: fires anyway (facing+dwell suffice); ranked: gaze check holds it, HUD hints "look to the field to seal the summon."

Precedent honesty: no shipped game does *exactly* this (we're first); the components are each proven — fiducial pose tracking (Quest PCA community stacks, ~70fps on multi-marker), dwell-select (gaze UIs since HoloLens 1), progress-glow affordance (every hold-to-confirm button). The *composition* is our invention and needs playtest tuning of thresholds — budget for it.

## 5. Spellcasting Precedents: what shipped, what worked, what players hated

| Game | Player act | Recognition | Reception & lesson |
|---|---|---|---|
| **The Wizards / Dark Times** (VR) | Controller gesture combos per spell (flick-up = fireball, etc.) | Motion-pattern matching on controller trajectories | Praised as the immersion innovation; **top complaint: missed gestures, especially under combat stress** — tutorial gestures "don't always get detected." Lesson: *gesture vocabulary must be few, gross-motor, and mutually distinct; recognition failure under pressure is rage fuel* |
| **Waltz of the Wizard: Natural Magic** (VR/MR, hand tracking) | Natural hand acts — point, grab, speak; "telepath" gestures | Hand-pose + gesture recognition (Aldin's stack); also voice | The benchmark for *hands-feel-magical*; shipped hand-tracking magic on Quest & PSVR2 successfully. Lesson: *natural single poses > choreographed sequences* |
| **In Verbis Virtus** (PC, mic) | Hold key → speak full incantation in fictional language (Maha'ki: "lumeh tial" = light) | Speech recognition against known phrase set | 78% positive on Steam; hailed as genuinely innovative; **complaints: mic recognition reliability**. Lessons: *fictional-language chants are beloved AND phonetically controllable; PTT gating worked; recognition failures are the entire risk* |
| **Phasmophobia** (PC, mic) | Speak naturally; ghost reacts to keywords ("Is anyone here?"… saying "panic" has consequences) | **Vosk, on-device, per-player** keyword recognition | Massive hit; voice recognition is its signature feature — *because speaking has stakes*, players self-censor and immerse. Lessons: *local keyword recognition is shippable at scale; voice-with-consequences is unforgettable* |
| **Skyrim Kinect** (console, mic) | Hold button, speak shout ("Fus Ro Dah") — 200+ commands | Kinect speech, PTT-gated | Verdict in reviews: "a neat, impressive **gimmick**." Lesson: *voice bolted onto a game designed without it stays a gimmick; breadth (200 commands) ≠ depth* |
| **Mass Effect 3 Kinect** (console, mic) | Speak squad orders instead of pausing for the command wheel | Small-vocabulary command recognition | "Doesn't feel like a bolted-on gimmick. It feels right." Lesson — the golden one: **voice succeeds when it replaces a slower menu, fails when it's decoration.** Our voice summons replace menu-diving for a named card = the ME3 pattern, not the Skyrim pattern |
| **Black & White** (PC, mouse) *[pre-search-era classic, widely documented]* | Draw symbols with mouse to cast miracles | Stroke-shape matching | Iconic; also famous for misrecognized strokes. Same lesson as The Wizards: *few, distinct shapes* |
| **Okami / Arx Fatalis** *[classics]* | Paint brush-shapes on screen / draw runes in air with mouse | Shape matching | Okami's shapes were simple & distinct → beloved; Arx's freehand runes were fiddly → patched with helpers. Lesson: *drawability and mutual distinctness beat elegance* |

**Synthesis into our ladder (from the casting doc):** Signs = Waltz-style single poses (not Wizards-style combos — the miss-under-stress complaint is fatal in PvP); Words = ME3-style small-vocabulary replacements for selection friction, In-Verbis-style fictional phrases for flavor; Scripts = Okami-lesson glyphs (8 launch sigils, gross-motor, maximally distinct strokes — cross, circle, spiral, triangle, zigzag, wave, arrow, hourglass); Ritual = the In Verbis full-chant fantasy, opt-in for the players who love it. Each rung's recognizer is simpler than the games that struggled: static poses (not trajectories), closed phrase sets (not open speech), 8 glyphs (not freehand).

## 6. Incantation Design: keeping spoken magic exciting

What the record shows: full chants are *loved as fantasy* (In Verbis' 78%, its magic language called the best part) and *worn down by repetition + recognition strain* (its complaints; Skyrim's gimmick verdict). The failure mode is mandatory-and-long; the success mode is optional-and-meaningful. Our design already threads this, now sharpened:

- **Recognize the kernel, reward the flourish.** The recognizer requires only the card's name-kernel ("Emberdrake"); speaking the full printed line ("From cinder and wrath — Emberdrake, ignite!") grants the Word-tier bonus. Rushed players say one word; performers get paid for performing. (Also robust: the kernel is the phonetically-engineered token; the flourish needn't be perfectly recognized — detect kernel + prosody duration, not verbatim text.)
- **Repetition is self-limiting by economy:** you summon a given card at most twice a duel (2-copy limit) — chants can't wear out like a spammable ability. Support-spell Words face the school-cooldown + diminishing-echo throttles.
- **Variety by design:** every card's line is unique (the card-anatomy doc prints them); legendaries get 2-line chants for Ritual summons — the trailer moment.
- **The Phasmophobia stake:** chanting isn't just flavor — the opponent *hears* your summon before the monster lands (audio telegraph) — while chantless mastery (progression doc) silences it. Speaking has stakes; silence is earned. That tension keeps voice interesting for hundreds of hours.
- **Always skippable:** Sign-tier silent path, full parity, forever (public spaces, streamers' neighbors, speech accessibility, and the socially-shy — which In Verbis' niche proved is a real segment).

## 7. The Input State Machine (with failure paths)

```
[IDLE: cards in hand, faces inward]
   │ card leaves fan-cluster, rotates face-out, elevated
   ▼
[CANDIDATE]  rune-ring begins glow-fill (500–700ms dwell)
   │ fill completes                        │ card lowered/flipped → drain → IDLE
   ▼                                       │ fiducial confidence drops → glow pauses
[PRESENTED]  chime + mic-rune lights; 3s voice window opens
   │                                       
   ├─ kernel recognized ────────────────► [SUMMON: word-tier bonus if full line]
   ├─ Sign performed instead ───────────► [SUMMON: base tier]   (silent path)
   ├─ window expires, card still held ──► [FALLBACK PROMPT]: rune blinks;
   │                                       tap the card with off-hand = confirm silently
   ├─ recognition low-confidence ───────► [DISAMBIGUATE]: top-2 candidates shown
   │                                       as mini-holograms; gaze+pinch picks (≤1s)
   └─ energy insufficient ──────────────► ring flashes red + cost shown; → IDLE
   ▼
[RESOLVING]  summon animation; card marked spent (server-authoritative)
   │ then: physical card → discard pouch (tracked but not required immediately)
   ▼
[FIELDED] → command loop (gaze+pinch targeting, Signs, Scripts as designed)
```

Failure-handling principles baked in: **(a) every failure state has a ≤1s silent recovery** (tap-to-confirm, disambiguation pick) — voice failure never costs more than a second, so trust survives; **(b) two consecutive voice failures auto-suggest the silent path for that session** (adaptive degradation — never let a player with a headcold fight the recognizer all night); **(c) all confirmations are multi-channel** (visual glow + spatial chime + controller haptic if held) because in a loud room or bright light any single channel can be missed; **(d) the server validates every transition** (card in registered deck? energy sufficient? cooldowns clear?) so recognition errors can *never* create illegal game states — worst case is a rejected action with a clear red flash, not corruption.

Challenged assumption, per your brief: is "present → speak → gesture" even the right order? Alternatives considered: *speak-first* ("Emberdrake!" then show card) fails the mic-gating requirement (needs always-on listening — rejected in §2); *gesture-first* (Sign then card) adds a step to the most common action. The card-first flow wins because the card is the *only* unforgeable intent signal (it's a physical object with exactly one owner) and every subsequent input can be biased by knowing which card is up — recognition accuracy compounds down the chain. This ordering is, as far as the research shows, the correct one — but the vertical slice should A/B the dwell and window timings, not the order.

## 8. Cross-Platform Interaction Matrix

Universal core (every platform, identical mechanics): **physical-card presentation · hand poses (Signs) · air-drawn sigils · gaze+pinch targeting · gated voice · dwell+glow confirmations.** Built on OpenXR + engine-level abstraction; per-platform notes:

| Platform | Deltas from core | Notes |
|---|---|---|
| **Quest 3/3S** (primary) | + optional controllers (haptics, steadier sigils, ray-targeting); Multimodal API for dangling-controller support | Reference implementation |
| **Android XR** | Same as Quest minus Multimodal (use OS auto-switch); verify camera-feed API parity for card tracking | Port target #2 |
| **Vision Pro** | Hands+gaze native (best-in-class); **cards via digital twins until camera/image-tracking APIs allow live card reading** (1Hz image tracking blocks physical cards today — the VP build is the "remote/VR mode" of the same game, full parity through the silent path + digital hand) | Port when APIs allow physical cards |
| **Mobile AR** (companion/entry tier) | Touch replaces pinch; device camera reads the same physical cards excellently (ARKit/ARCore image tracking is mature); voice gate = on-screen hold-button (true PTT); no Signs (no hand tracking) — Sign-tier effects via touch buttons at same power | Full games playable; the funnel |

Design invariant across all: **same rules, same timings, same power ceilings** (per §3's no-speed-advantage rule); only the *motor expression* differs. A Quest player, a Vision Pro player, and a phone player queue in the same ranked pool legally because no input path is mechanically privileged — the balance-doc telemetry watches per-platform winrates to keep that honest (alert band: any platform outside 48–52%).

---

## Sources (key)
- Whisper-on-Quest: [Unity Sentis + Whisper testing](https://www.topview.ai/blog/detail/offline-speech-recognition-on-meta-quest-testing-unity-sentis-whisper-ai) · [local STT on Quest (Hackaday)](https://hackaday.com/2024/08/16/robust-speech-to-text-running-locally-on-quest-vr-headset/) · [whisper-meta-quest](https://github.com/saurabhchalke/whisper-meta-quest)
- Meta Voice SDK/Wit.ai: [overview](https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/) · [Whisperer sample](https://github.com/oculus-samples/voicesdk-samples-whisperer)
- Keyword spotting benchmarks: [Picovoice KWS](https://picovoice.ai/blog/keyword-spotting-voice-recognition/) · [Porcupine accuracy](https://picovoice.ai/products/voice/wake-word/) · [on-device recognition docs](https://picovoice.ai/docs/)
- Phasmophobia voice: [how it works](https://www.exitlag.com/blog/phasmophobia-voice-recognition/) · [voice modes](https://progameguides.com/phasmophobia/phasmophobia-voice-recognition-mode-explained/)
- In Verbis Virtus: [Steam (78% positive)](https://store.steampowered.com/app/242840/In_Verbis_Virtus/) · [review](https://thekoalition.com/reviews/in-verbis-virtus-review-verbal-magic/)
- Skyrim vs ME3 Kinect: [Skyrim gimmick verdict (Kotaku)](https://kotaku.com/i-shouted-fus-ro-dah-at-skyrim-and-i-kinda-liked-it-5903174) · [ME3 "feels right" (Game Informer)](https://www.gameinformer.com/games/mass_effect_3/b/xbox360/archive/2012/01/18/mass-effect-3-preview-not-bad-with-kinect.aspx)
- The Wizards gesture complaints: [Ghost Howls review](https://skarredghost.com/2017/08/03/wizards-review-entertaining-vr-spellcaster-game-early-access/)
- Waltz of the Wizard: [RoadToVR](https://roadtovr.com/hands-waltz-wizard-natural-magic-flexible-clever-sorcery-massive-dungeon/)
- Hands+controllers: [Multimodal API](https://developers.meta.com/horizon/documentation/unity/unity-multimodal/) · [UploadVR on v68 multimodal](https://www.uploadvr.com/quest-2-multimodal-v68-sdk/)
- Wake word vs PTT: [Picovoice wake-word guide](https://picovoice.ai/blog/complete-guide-to-wake-word/)
