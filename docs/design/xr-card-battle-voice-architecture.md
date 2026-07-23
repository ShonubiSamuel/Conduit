# Voice Architecture Revisited — Roles, Registers, and a Layered Hybrid v0.2

*Supersedes the "keyword spotting" recommendation in the interaction-system doc §1. That answer was a component posing as an architecture. This document derives voice's actual roles first, then matches technology to each role — and lands on a hybrid whose core is dynamic grammar-constrained recognition fused with gaze, not keyword spotting.*

---

## 1. The mistake, named

Keyword spotting answers "did the player say Emberdrake?" It cannot answer "what does the player want Dragon Flare to *do*?" Your examples are **compositional commands** — entity + verb + argument:

- "Dragon Flare, **destroy** [the enemy player]"
- "Dragon Flare, **attack** [Monster A]"
- "Dragon Flare, **defend** [our left flank]"
- "Dragon Flare, **return** [to my side]"
- "Dragon Flare, **sacrifice** [yourself]"

Same prefix, five intents, open argument slots. A spotter would need every full sentence pre-enrolled — combinatorially absurd. So the architecture question is real. But before choosing tech, the deeper question:

## 2. What IS voice in this game? Four registers, not one role

The precedent research (ME3 vs Skyrim, Phasmophobia, In Verbis Virtus) shows voice succeeds when its *function* is clear. Our game needs voice in four distinct registers, and **each register needs different machinery — that's the insight that dissolves the "which one tech?" framing:**

| Register | What the player is doing | What the system must extract | Perfection needed? |
|---|---|---|---|
| **A. Command** | Issuing tactical orders ("defend the left flank") | Full intent: entity + verb + slot | Yes — deterministic parse |
| **B. Invocation** | Summon kernels + incantation lines | Kernel identity + (optionally) completion | Kernel yes; the rest no |
| **C. Ritual/Performance** | Long chants, prayers, legendary summons | *That* a performance happened: start, sustained cadence, finish | **No transcription at all** |
| **D. Conversation** (PvE) | Talking to NPCs, spirits, story characters | Open meaning | Flexible — LLM territory |

The key liberation is Register C, answering your "do we even need every word?" question: **a ritual is a performance envelope, not a dictation.** The game needs the opening kernel ("By the shattered Origin…" → detect the phrase-start), evidence of sustained vocal performance (voice-activity + prosody: cadence, energy, duration — no words needed), and the closing kernel ("…Emberdrake, IGNITE"). Between the checkpoints the player can pray, improvise, whisper, or ham it up in any accent — the system checks *commitment, not text*. This is mechanically honest (the Ritual tier's cost is time-exposed-while-casting, which prosody measures perfectly) and it makes long incantations **robust by design instead of fragile by transcription**. Rhythm games, not dictation software, are the right mental model.

## 3. The architecture bake-off (Register A is the hard one)

Evaluated for: accuracy under emotional/fast speech, latency, on-device viability, compositionality, **determinism** (competitive fairness — same utterance must always parse the same), and future headroom:

| Architecture | Compositional? | Deterministic? | On-device | Latency | Verdict |
|---|---|---|---|---|---|
| **Pure keyword spotting** (Picovoice-class) | ❌ fixed phrases only | ✅ | ✅ tiny | ~10ms | Demoted: kernels & wake-gates only |
| **Small-vocab ASR** (static list) | ❌ | ✅ | ✅ | low | Strictly dominated by grammar-based below |
| **Grammar-constrained ASR** ⭐ (Vosk dynamic-grammar; SRGS lineage — the ME3/Kinect approach) | ✅ within grammar | ✅ | ✅ (Vosk small models support **runtime grammar updates**) | low (sub-second) | **Core of Register A/B** |
| **Full STT → NLU pipeline** (Whisper/cloud + intent model) | ✅ | ⚠️ two stochastic stages compound errors | ❌/heavy | high | Rejected for live play: pays open-dictation fragility for flexibility we don't need — our language is closed |
| **LLM intent parsing** (NVIDIA ACE-lineage; local or cloud) | ✅✅ handles anything | ❌ inherently | ⚠️ improving (local inference is ACE's whole pitch; 0.8–2.5s typical for cloud) | high today | Register D now; Register A's *future* via constrained decoding (§6) |
| **Hybrid (layered)** | ✅ | ✅ where it matters | ✅ | low | **The answer** |

**Why grammar-constrained recognition is the right core:** it's the only approach that is simultaneously compositional AND deterministic AND on-device — and our game is its dream scenario. A grammar recognizer picks the best path through a *finite language you define*. Your own observation completes the argument: the game state defines that finite language **every frame**:

```
command   := entity , verb , [slot]
entity    := {names of YOUR fielded monsters}            ← 0–4 options
verb      := attack | destroy | defend | return | sacrifice
             | use <ability-on-that-entity>              ← ~6–10 options
slot      := {legal targets for that verb, that entity}  ← 0–8 options
             | <deictic marker: "that one/them/there">   ← resolved by gaze (§4)
```

The compiled grammar at any moment is a few hundred paths — trivially small. Every utterance in your list parses deterministically; "Dragon Flare, dance for me" correctly fails (not in the language) and triggers the standard silent-recovery UX. Vosk's small models explicitly support this dynamic runtime re-vocabulary, on-device, today — and the published record shows constrained grammars measurably beat open dictation on exactly our failure cases (noise, accents, technical vocabulary). This is also, historically, how ME3's celebrated Kinect commands worked: grammar-based command-and-control, not free speech — the pattern that "felt right" was *this* pattern.

## 4. The multimodal completion: speech names, gaze points

The 45-year-old foundational result — Bolt's *Put-That-There* (MIT, 1980) — plus its modern XR replications: **speech is the best channel for *naming* (entities, verbs, abstractions); pointing/gaze is the best channel for *spatial reference*; users default to pointing whenever verbal description gets cumbersome.** Recent VR studies confirm speech+pointing fusion beats either alone for 3D target selection.

So we *stop making speech do spatial work*:

- "Dragon Flare, attack **Monster A**" → works fully-spoken (grammar contains fielded enemy names), but nobody wants to memorize enemy names mid-fight, so—
- "Dragon Flare, attack **that one**" + *gaze/point at the target* → the deictic slot binds to the gaze ray at utterance time. Bolt, verbatim.
- "Dragon Flare, defend **the left flank**" → spatial-zone nouns (left/right flank, my side, the front) are in the grammar as *named field zones* (the arena is divided into diegetic zones at consecration — the anime always had named zones; so do we, and now they're both lore AND parse targets).
- Pure-gesture equivalents remain for every command (silent path, unchanged).

This fusion is what makes the command register feel like *commanding a creature* rather than *operating a speech UI* — you say the easy part and look at the hard part, exactly as humans do with dogs, soldiers, and each other.

## 5. The full layered architecture

```
                    ┌────────────────────────────────────────────┐
 mic (gated) ──────►│ L0  VAD + prosody envelope   (always, cheap)│──► Ritual register:
                    └────────────────┬───────────────────────────┘    start/cadence/finish
                                     │ audio segment
                    ┌────────────────▼───────────────────────────┐
 game state ───────►│ L1  Grammar compiler (per-frame):           │
 (cards, field,     │     entities × legal verbs × legal targets  │
  energy, phase)    │     + summon kernels for cards in hand      │
                    └────────────────┬───────────────────────────┘
                                     │ compiled grammar (few hundred paths)
                    ┌────────────────▼───────────────────────────┐
                    │ L2  Constrained recognizer (Vosk-class,     │
                    │     on-device, dynamic grammar)             │
                    └────────────────┬───────────────────────────┘
                                     │ parse: {entity, verb, slot|DEIXIS, conf}
                    ┌────────────────▼───────────────────────────┐
 gaze ray, hand ───►│ L3  Multimodal fusion: bind deictic slots   │
 pose, card pose    │     to gaze/point at utterance time;        │
                    │     cross-check intent vs card/gesture      │
                    └────────────────┬───────────────────────────┘
                                     │ resolved intent
                    ┌────────────────▼───────────────────────────┐
                    │ L4  Server validation (legality, energy,    │
                    │     cooldowns) → execute or reject-with-UX  │
                    └────────────────────────────────────────────┘

 Side channels:
 • Kernels/wake-in-gate: keyword spotter (L2-lite) for the 3s summon windows — its one true home.
 • Register D (PvE only): utterances failing L2 in story mode route to an LLM
   (local-first, ACE-style) for NPC/spirit conversation. Never in ranked.
 • Register C bypasses L2 entirely: envelope + kernel checkpoints only.
```

Properties: every competitive-path component is on-device, deterministic, and sub-second; each layer degrades independently (L2 failure → silent-input recovery; L3 ambiguity → two-candidate disambiguation from the interaction doc's state machine); and the *design contract* between layers is a typed intent schema — which is what makes §6 possible.

## 6. Future-proofing: why this hybrid ages well instead of ossifying

Your instruction was to optimize for long-term experience, not today's easiest build. The layered design does that in a specific way: **L2 is a swappable module behind a stable contract** (audio + context in → typed intent out). Two upgrade paths are already visible in the research:

1. **Constrained LLM decoding.** Local LLMs can be forced to emit only strings valid under a grammar/schema (llama.cpp GBNF-style constrained decoding; function-calling). As on-headset NPUs mature, L2 can become "small local LLM, decoding constrained to the same per-frame grammar" — gaining robustness to disfluency, synonyms, and creative phrasing while **keeping determinism-by-construction** (invalid parses are unrepresentable). That's the end-state: natural-language freedom, competitive-grade rigor.
2. **LLMs as design-time compiler, today.** We already use an LLM offline to *author* the grammars: generating and vetting synonym sets ("guard/protect/shield/hold" → DEFEND), per-locale variants, and phonetic-distinctness checks across the card pool. All the LLM's linguistic breadth, none of its runtime nondeterminism. (This also scales incantation-writing across hundreds of cards.)

And Register D grows into the headline PvE feature over time: story duels against an LLM-driven rival who *hears your table talk* (Phasmophobia's lesson — speech-with-stakes is unforgettable) is a roadmap item the architecture supports without touching the competitive stack.

## 7. Voice's role — the direct answer

Voice is **all five of your options, cleanly partitioned, and never load-bearing alone:**

- **Activation:** the pose-gated mic (unchanged from the interaction doc) — voice never self-activates.
- **Command language:** yes — Register A, grammar+gaze, the ME3 pattern extended with Bolt fusion. This is voice's highest-value gameplay job: replacing multi-step selection friction with one breath.
- **Ritual:** yes — Register C, performance envelopes; commitment measured, words free.
- **Theater:** always free — anything outside a mic gate is never recognized, and *that's a feature*: trash talk, narration, and "haha, you fool" live untouched by machinery.
- **Intent conveyance:** partially, *by design* — speech carries names and verbs; gaze and gesture carry space; the card carries the unforgeable act. No single channel is ever the sole witness to intent, which is why the system can be simultaneously dramatic and reliable.

The one-line philosophy: **transcribe as little as possible, understand as much as needed, and let performance be performance.**

---
## Sources
- Vosk dynamic grammars & runtime vocabulary: [official adaptation docs](https://alphacephei.com/vosk/adaptation) · [LM/grammar docs](https://alphacephei.com/vosk/lm) · [custom-LM WER study](https://arxiv.org/html/2503.21025v1)
- Put-That-There & multimodal fusion: [Billinghurst overview](https://marknb00.medium.com/multimodal-mixed-reality-b4ab8ddc304b) · [XR multimodal scoping review](https://www.mdpi.com/2414-4088/5/12/81) · [LLM-assisted speech+pointing VR selection](https://arxiv.org/pdf/2410.21091) · [Oviatt, Multimodal Interfaces](https://www.cogsci.msu.edu/DSS/2004-2005/Oviatt/Multimodel%20Interfaces.pdf)
- LLM/NPC stacks & latency: [NVIDIA ACE](https://developer.nvidia.com/ace-for-games) · [state of LLMs in games (HF)](https://huggingface.co/learn/agents-course/en/bonus-unit3/state-of-art) · [ACE local-inference design](https://www.solidaitech.com/2026/06/ai-video-games.html)
- Precedents (carried from interaction doc): ME3 grammar-command success, Skyrim breadth-gimmick failure, Phasmophobia local keyword stakes, In Verbis Virtus chant fantasy + recognition strain.
