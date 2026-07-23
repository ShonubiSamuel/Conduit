# XR Card Battle Game — Research & Design Report

*Prepared July 2026. Roles covered: game designer, XR researcher, technical architect, product strategist.*

*Updated: added §4A (card recognition & summon trigger, standing/open-field play, environment transformation) and locked networking to Photon Fusion.*

> ℹ️ **STATUS — FOUNDATIONAL BACKGROUND (not fully current on specifics).** This is the original umbrella report. It remains the best high-level overview (hardware, prior art, AI pipeline, product strategy) and its §4A/§5/§8 updates are current. However, the game's **specific systems were designed AFTER this report** and live in dedicated docs that take precedence where they differ: Aspect/power system, balance math, casting ladder, deck/progression, voice architecture (v0.2), card-visibility Option B, and the master roadmap. When this report and a dedicated doc disagree, the dedicated doc wins. See `00-INDEX.md`.

---

## 1. Executive Summary

**The idea is technically feasible today, on one platform: Meta Quest 3/3S.** The single most important enabling development is Meta's **Passthrough Camera API** (public since v76, 2025), which gives apps raw RGB camera access — the missing piece that made physical-card recognition on headsets impossible for years. Combined with Shared Spatial Anchors and Colocation Discovery, everything needed for "two people at a real table, real cards, monsters fighting in the room" now exists in shipping consumer hardware.

**Key strategic findings:**

1. **The market whitespace is real.** Konami built a Quest 3 "Solid Vision Experiment" as a one-off anniversary demo and explicitly did not productize it. No one has shipped a serious MR TCG with physical cards. Prior hybrid attempts (Lightseekers, Malkyrs, Flex NBA) failed on mobile-AR-era hardware and economics, not on the concept.
2. **Your VR question has a better answer than "digitize the collection."** The right reframe: don't build a separate VR mode. Build one MR game where the *environment* is a dial — from "your room" to "fully virtual arena" — while your hands, table, and cards stay real via passthrough portals. Details in §5.
3. **Card recognition should not rely on pure image recognition of artwork.** Design the card frame itself as a machine-readable fiducial (plus NFC for ownership). This is a design decision you can only make cheaply now, before printing cards — it's the biggest advantage of building original IP instead of adapting an existing TCG.
4. **AI content pipelines make a 300–500 creature launch set viable for a small team** — but only with a strict art-direction system layered on top. Raw AI output is recognizable and inconsistent; the pipeline in §9 addresses that.
5. **Don't copy turn structure from paper TCGs.** The recommended combat model is a **WEGO (simultaneous planning → cinematic resolution)** structure with real-time interrupt windows — it preserves the drama of anime duels while solving XR's "waiting is boring in a headset" problem (§6).

**Recommended path:** Unity + Meta XR SDK on Quest 3, OpenXR architecture for later Android XR port, Photon Fusion networking, 8–12 week vertical slice with ~10 creatures proving card-on-table recognition + colocated 2-player battle.

---

## 2. Prior Art & Market Landscape

### What exists

| Product | What it proves | Status |
|---|---|---|
| **Yu-Gi-Oh! Solid Vision Experiment** (Konami, Quest 3) | Demand: iconic monsters in MR was the fan-favorite anniversary demo. Konami chose *not* to develop it into a game | Demo only |
| **Tilt Five** | Dedicated tabletop-AR hardware (projection glasses + retroreflective board). Niche but loyal audience; proves tabletop AR sessions work socially | Shipping, small ecosystem |
| **Demeo / Demeo x D&D: Battlemarked** (Resolution Games) | MR tabletop games can be commercial hits; cross-platform play (Quest, PSVR2, Vision Pro, Galaxy XR, PC) is a major retention driver; hands-on interaction with miniatures/cards/dice is beloved | Shipping, successful |
| **Lightseekers** (2017–2020) | NFC-scannable physical cards + free digital play. Good tech, died commercially — physical/digital hybrid economics on mobile were brutal | Dead |
| **Malkyrs** (Switch NFC cards) | NFC card-on-controller play loop | Dead |
| **Flex NBA** (Sequoia Games) | AR + physical boards + collectibles, licensed IP | Niche |
| **Genesis AR, phone AR card apps** | Phone AR "monster pops out of card" is a novelty, not a game — retention collapses after the first wow | Novelty tier |
| **HoloLens fan demos, academic ARTCG papers** | Repeatedly validated card recognition + tabletop battle UX in labs since ~2016; never had consumer hardware to ship on | Research |

### What the graveyard teaches

- **Phone AR is the wrong vessel.** Holding a phone up kills the fantasy — you're watching a screen, not inhabiting a duel. Every phone-AR card product decayed into a collectible viewer.
- **Hybrid physical/digital economics killed Lightseekers, not tech.** Physical card production, distribution, and retail relationships are a real business you must plan for, not an afterthought. Start with small print runs, direct-to-consumer, event-driven drops.
- **Demeo's lesson:** the social table is the product. Everything — netcode, platforms, session flow — should serve "I sat down with a friend and it felt like a real game night."
- **The opportunity:** nobody has combined (a) real cards, (b) headset MR, (c) an actual competitive game. The pieces all exist separately. That's the gap.

---

## 3. Hardware Platform Analysis

| Platform | Card recognition | MR quality | Install base | Verdict |
|---|---|---|---|---|
| **Meta Quest 3 / 3S** | ✅ Passthrough Camera API: 1280×960 per eye @30fps, 40–60ms latency; proven community stacks for QR/ArUco/image tracking (QuestCameraKit, OpenCV for Unity) | Good color passthrough, scene mesh, table detection | Largest MR base by far; 3S at $299 | **Primary target** |
| **Apple Vision Pro** | ❌ blocking: ImageTrackingProvider updates ~1 Hz — unusable for cards in motion. Camera access requires Apple-approved entitlement, oriented to enterprise, and apps using it face store constraints | Best-in-class passthrough & hand/eye tracking | Small, wealthy | Port later; wait for API relaxation |
| **Android XR (Galaxy XR, 2026 devices)** | ARCore-lineage tracking; camera access story still maturing; OpenXR + Unity path means low porting cost | Excellent displays ($1,799 tier); more devices coming 2026 | Small but growing; strategically important (Google+Samsung+Qualcomm) | **Fast-follow port** |
| **Pico 4 Ultra** | MR APIs exist; smaller dev ecosystem, mostly non-US markets | Comparable to Quest 3 | Regional (EU/Asia) | Opportunistic port |
| **Mobile AR (ARKit/ARCore, handheld — NOT Cardboard)** | ✅ Excellent image tracking, NFC | Screen-mediated — less immersive, but a real playable tier | Billions | **Free entry tier**: full games playable via handheld/propped phone + collection scanning + spectating. The discovery funnel: phones bring millions in, headsets convert the fans. Skip Google Cardboard entirely (dead, poor lenses, blocks the needed camera) |
| **Tilt Five** | Board-based tracking | Tabletop-only projection | Tiny | Skip |
| **Smart glasses (Meta Ray-Ban Display etc.)** | No — insufficient compute/display for this in 2026 | — | — | Watch for 2028+ |

**Recommendation:** Quest 3/3S primary; build on OpenXR/Unity so Android XR is a port, not a rewrite; mobile as companion, never as the game. Vision Pro when Apple opens moving-image tracking or camera access to games — its eye tracking would be the best "attunement" input of any device (§7).

---

## 4. Making Physical Cards Come Alive (MR Mode)

### Recognition technology comparison

| Method | Reliability | Latency | Cost/card | Notes |
|---|---|---|---|---|
| **Pure artwork image recognition** | Medium — struggles with glare, sleeves, occlusion by hands, similar artworks at 1000+ card scale | Medium | $0 | Works on phones; on Quest you'd run your own CNN/feature matcher on the passthrough feed |
| **Fiducial-in-frame (ArUco/custom)** | High — designed to be machine-readable; proven Quest 3 stacks exist today | Low | $0 | **Design the card border/back so the fiducial IS the art style.** A rune-ring around the art, corner glyphs, or a patterned card back can encode 12–20 bits invisibly-in-plain-sight |
| **QR on card** | High | Low | $0 | Ugly if overt; fine as small corner code for scan-in flows |
| **NFC chip** | Perfect ID, zero vision | n/a (tap) | $0.10–0.30 | Can't be read at-a-distance by headsets — phones/readers only. Use for **ownership, authenticity, pack-opening, scan-in** — not live play |
| **Object recognition (3D)** | Wrong tool — cards are flat | — | — | Skip |

**Recommended card architecture (the "three-layer card"):**

1. **Human layer** — full art, name, stats. The collectible object. Foils, rarities, alt-arts as usual.
2. **Vision layer** — the card frame doubles as a fiducial: a high-contrast glyph ring encoding card ID + orientation, styled as part of the IP's visual language ("summoning runes"). Headset reads it at 30fps from across the table, robust to partial hand occlusion. Rotation of the card = readable state (tapped/attack/defense analogue).
3. **Radio layer** — NTAG NFC inside premium cards: tap-to-authenticate, tap-to-add-to-collection, tamper-evident for tournament play. Commons can omit it (cost), with QR fallback for scan-in.

This tri-layer design is only possible because you're creating original IP — you get to bake machine-readability into the art direction from day one. It also gives your cards a distinctive look that becomes brand identity (the rune ring *means* "this card is alive").

### Spatial grounding

- **Table detection:** Quest scene understanding gives table surfaces; snap the battlefield to the detected table plane, with a calibration ritual ("place your deck on the table to consecrate the arena") that doubles as anchor placement and feels diegetic.
- **Spatial anchors:** world-lock the arena; **Shared Spatial Anchors + Colocation Discovery** (Bluetooth proximity session discovery) give both players an identical shared frame in the same room with ~zero setup friction.
- **Persistent play spaces:** anchors persist between sessions — your kitchen table can *remember* being an arena (scorch marks fade over days; a nice retention hook).

---

## 4A. How Recognition & Summoning Actually Work (and Standing/Open-Field Play)

This section answers the two most important "but how does it *actually* work" questions: how the system knows which card you're holding and when to summon it, and how the anime-style standing open-field duel works rather than only tabletop.

### Two separate problems: identification vs. trigger

The system must answer two independent questions, and conflating them is the usual mistake:

- **Identification — "which card is this?"** The passthrough cameras stream video (~30fps on Quest 3). The app runs computer vision on each frame, detecting card-shaped patterns and decoding the card's identity and its exact 3D position + tilt. This is *why* the card frame is designed as a machine-readable fiducial (a high-contrast pattern styled as decorative summoning runes around the art): it decodes to an ID in milliseconds, works from across a field, and survives glare, sleeves, and a hand covering half the card. Plain-artwork recognition also works but degrades badly under occlusion/glare and slows down at 1,000+ card scale, so the fiducial frame is primary and artwork-matching is redundancy. At all times the headset knows, e.g., *"Dragon Snake is in the player's right hand, held chest-high, facing outward."*

- **Trigger — "when do I summon?"** Merely *holding* a card must never summon it (players hold cards constantly while thinking). The summon fires on a deliberate action. Supported triggers, combinable:
  - **Placement:** slot the card into your play zone (table zone, or a floating "duel tray"/duel-disk in standing mode) → summon fires. Unambiguous and silent.
  - **The anime move (recommended hero flow):** raise the card facing outward in a reveal pose **and** say *"I summon Dragon Snake!"* The system requires two simultaneous signals — vision confirms *that specific card* in the reveal pose, on-device speech confirms the summon phrase, both true within ~1–2s. Requiring both prevents accidental summons and false voice matches.
  - **Gesture-only fallback:** the raise-and-reveal pose held ~1s, no voice — for public/quiet play.

So the camera answers *what*; the deliberate gesture/voice/placement answers *when*. The summon animation plays on the trigger, not on mere detection.

### Standing open-field play is the primary mode, not tabletop

The anime fantasy is two duelists standing in an open field with monsters battling between them and a crowd around — and the architecture supports this directly. Tabletop is simply the "anchor sits on a table" variant; it is not the main mode.

- **Anchor placement:** the shared spatial anchor can be a patch of floor/field between the players rather than a table. Monsters materialize at field scale in the volume between the two duelists.
- **Your play surface when standing:** instead of table zones, each player gets a floating holographic tray or a **duel-disk-style forearm UI** at hip/wrist height. You hold your real hand of cards; recognition is actually *better* here because cards are close to your cameras.
- **Crowd/spectators:** anyone with a phone (ARCore/ARKit) or a headset joins as an anchored spectator seeing the same battle in the same space.

### Environment transformation is a card effect, not a mode switch

Mid-duel, a field spell or trap (*Dark Moon*, *Volcano*, *another planet*) can transform the whole world. This is the **environment dial** (§5B) driven by gameplay: playing the card smoothly fades the passthrough real world into a fully virtual environment **within the same session**, while selective passthrough keeps your hands and real cards visible throughout. MR → VR is something a card *does to the battlefield*, never a menu the player toggles. The same mechanism handles remote play (dial forced toward virtual: real cards/hands visible, opponent as an avatar across the field).

**Result:** tabletop, standing open-field, and remote are one game with one recognition system and one anchoring system — they differ only in where the anchor sits and how far the environment dial is turned.

---

## 5. The VR Question — Full Design-Space Analysis

Your instinct is correct: "scan your cards, play digital copies" is practical but emotionally hollow. Here is the full option space, evaluated:

### Option A — Digitized collection (your baseline)
Scan physical cards → account-bound digital twins → play with virtual cards in VR.
- **Feasibility:** trivial today. **Complexity:** low. **Reliability:** perfect.
- **Immersion verdict:** you've built another digital card game. Virtual cards have no weight, no shuffle, no riffle, no fan-out muscle memory. This is Master Duel with extra steps.
- **Keep it as fallback** — required anyway for remote play and for players without their deck on hand.

### Option B — Passthrough portals: real hands + real cards inside the virtual world ⭐ **recommended**
The reframe that dissolves the problem: **MR and VR are not two modes. They are one continuous dial.**

On Quest-class hardware, "VR" is just passthrough with 100% virtual environment. Nothing stops you from rendering a fully virtual arena *and* cutting selective passthrough windows where reality should remain: your hands, your card hand, the table surface, your deck. This is shipped technique — Cubism uses selective passthrough to keep real hands visible over virtual objects; Meta ships the Selective Passthrough shader with soft masking.

**Implementation:** track the card zones (hand-held fan, deck, table) → render passthrough in those regions via depth-masked portals → the rest of the view is the virtual volcano arena / sky temple / abyss. Your physical cards float in front of a fantasy world, exactly like the anime shot of a duelist holding real cards while the arena rages behind them.

- **Feasibility today:** yes, on Quest 3. The hard part is a clean mask around hands+cards; hand-tracking mesh + card fiducial pose + a generous stylized "aura" border around the portal (embrace the edge as a glowing effect rather than fighting for pixel-perfect segmentation).
- **Complexity:** medium. **Reliability:** high if you stylize the seams. **Cost:** engineering only.
- **Immersion verdict:** this *is* the anime fantasy — real deck in your hands, impossible world around you. **This should be the flagship "VR mode": you never stop touching your cards.**
- **Future trajectory:** improves automatically as passthrough resolution and on-device segmentation improve (every headset generation).

### Option C — Proxy deck: physical blank cards, virtual faces
Players use a physical deck of *marker-backed proxy cards* (blank faces or generic backs with fiducials). The headset renders each card's face virtually, mapped onto the physical card.
- **Upside:** full tactile experience (shuffle, draw, fan, slam) **plus** dynamic card faces — cards can animate, reveal, transform, display live stats. Solves hidden information elegantly (opponent's headset renders card backs). One $15 proxy deck plays your entire digital collection in VR.
- **Downside:** undermines the collectible physical card as the object of value; rendering text legibly on a moving card at passthrough resolution is at the edge of current-gen comfort.
- **Verdict:** genuinely innovative — offer it as the "Duelist Deck" accessory tier, not the core model. Strongest in 2027+ hardware. Also your best tournament anti-counterfeit story (faces rendered server-side).

### Option D — Tracked physical objects as instruments
Physical deck box, "duel disk"-style wearable, or totem that's tracked and made native in VR (NFC + fiducial). Cards stay abstract, but summoning gestures happen on a real prop.
- **Verdict:** great merch and great theater, but a peripheral, not a solution. Consider a deck-box product later ("place your real deck in the shrine to sync it").

### Option E — Camera-based hand+card reconstruction (your CV idea)
Segment hands and cards from the camera feed and composite them into VR — same goal as Option B but via reconstruction rather than portals.
- **Verdict today:** Option B achieves the identical user-visible result with far less compute and failure surface. Reconstruction (relit, stylized hands/cards matching the virtual world's lighting) is the *future premium version* of B — revisit when on-device segmentation models mature. Design B so it can upgrade into E without changing the game.

### Option F — Remote play (no shared table)
Digital twins are mandatory here (opponent can't see your physical cards anyway) — but keep *your own* cards physical via Option B, transmit only game-state. Your opponent appears as an avatar across a virtual table; their plays materialize as virtual cards. Asymmetric realism is fine: *your* hands are real, and that's what your immersion depends on.

### Recommendation
**Ship B as the answer to "VR mode," A as the remote/fallback layer, C as a premium accessory experiment, E as the roadmap.** The product story becomes one sentence: *"Your real cards work everywhere — at a real table in your room, or inside worlds that don't exist."* No separate VR game, one codebase, one design.

---

## 6. Gameplay Design — Spatial-Native, Not Ported

Guiding principle: **the player is a summoner standing in the arena, not a general looking at a board.** Every mechanic below exists because spatial computing enables it; none of it is possible on paper or flatscreen.

### The core loop (proposed original system — working title: *threshold*)

**Setup:** Both players consecrate the table (anchor ritual). The table is the **Ley Table** — the summoning zone. The *room* is the battlefield: creatures fight in the volume around and above the table, at 0.3–1.5m scale (readable, not overwhelming, works in small rooms; rare "colossal" summons scale to the ceiling as climax moments).

**Turn structure — WEGO with interrupts (the key design decision):**
Fully turn-based makes the non-active player a spectator in a headset — deadly, because standing idle in MR feels *worse* than on a screen. Fully real-time destroys the cerebral deck-strategy layer and punishes slower players physically. The synthesis:

1. **Channel Phase (simultaneous, 45s):** both players privately place cards face-down into rune slots on their table edge — orders, summons, spells. Hidden information preserved; both players are *always doing something*.
2. **Clash Phase (cinematic resolution, ~20s):** cards flip, creatures act simultaneously per an initiative stat, attacks and counters resolve as staged combat around the room. Both players *watch the consequences of their predictions* — this is the anime moment, earned by decisions rather than played as a cutscene.
3. **Interrupt windows:** during the Clash, each player holds 1–2 "trap-style" reflex cards that can be physically **slammed onto the table** in a 3-second window when a trigger condition flashes (telegraphed by the game). Physical slam = the single most emotionally loaded gesture in the source fantasy, now a skill-timed mechanic.

This gives: no dead time, hidden-information mind games, cinematic payoff, and the card-slam. Playtest the phase lengths obsessively.

### Summoning
- **Placement is the mechanic:** *where* you place the card on the Ley Table matters. The table has zones (vanguard/flank/sanctum) projected as glowing geometry; creature position in the room mirrors card position on the table. Card rotation sets stance (aggressive/warding).
- **Summon ritual scaling:** commons appear instantly; rares deserve theater — place card, then trace a sigil above it with a finger (hand tracking) or speak its invocation (voice). Cost: 2 seconds. Payoff: the moment every trailer is made of. Skippable in ranked.

### Attacks, spells, involvement
- **Command by gaze + gesture:** look at your creature (head-gaze on Quest; eye tracking on future ports), pinch-drag toward the target — an intuitive "sic 'em" that spectators can *read*, which matters for the social table.
- **Spells are somatic:** each spell card, once channeled, binds a gesture — a thrown projectile arc, a slashing block, a two-handed barrier raise. The player physically casts. Big spells can require standing.
- **The player's body is a game object:** creatures target *the summoner* in the endgame; you physically flinch, duck behind your Guardian, raise a warded hand to absorb the hit. Life total lives on a wrist gauge you glance at like a watch.
- **Environmental play:** scene mesh makes the real room tactical — creatures perch on your bookshelf for high ground, flyers circle the light fixture, a burrower travels *under the table* and erupts. Small rooms play differently than big ones; that's a feature (home-field advantage, literally).

### Involvement guarantees (anti-spectator checklist)
Every 10 seconds the player should be doing one of: reading opponent's table, channeling cards, tracing a ritual, holding an interrupt trigger, physically dodging/blocking, or repositioning to see around their own colossal creature (occlusion as drama). If playtests show idle gaps, tighten phase timers before adding mechanics.

---

## 7. Interaction Design Matrix

| Input | Use for | Avoid for | Notes |
|---|---|---|---|
| **Physical card placement** | Summons, channels, interrupts — the sacred core | Menus | Highest-fidelity, highest-joy input you have. Protect it |
| **Hand tracking** | Sigils, spell gestures, card slam detect, UI touches | Precision aiming, sustained holds (fatigue) | Quest hand tracking is good enough in 2026; design gestures with large tolerance |
| **Controllers** | Don't ship them for this game | — | Controllers occupy the hands that must hold cards. Hand-tracking-only is a defining constraint, embrace it |
| **Gaze (head/eye)** | Target selection, card inspect-on-look, attunement | Anything requiring staring (fatigue) | Eye tracking (AVP/Galaxy XR) upgrades this beautifully later; head-gaze works today |
| **Voice** | Invocations (named summons), optional commands, accessibility | Required inputs (public play, noisy rooms, self-consciousness) | Voice must always have a silent equivalent. On-device keyword spotting, not cloud |
| **Touch (table)** | Rune-slot UI on the tabletop — tap the wood, feel real feedback | Floating mid-air buttons | The table is a giant haptic touchscreen for free. Put ALL UI on it |
| **Physical movement** | Dodging, repositioning, leaning to peek | Required locomotion (accessibility) | Everything must be playable seated; movement is bonus expression |

**Accessibility baseline:** seated mode, silent mode, one-handed mode, colorblind-safe rune language, subtitle everything. TCG players skew wide; XR excludes enough people already.

---

## 8. Multiplayer Architecture

| Mode | Tech | Notes |
|---|---|---|
| **Same room (hero mode)** | Colocation Discovery (BT proximity match) + Shared Spatial Anchors + Photon Fusion (Meta ships this exact sample stack) | Both players see identical world-locked battle. Card recognition runs per-headset; authoritative state reconciles via the shared session |
| **Remote MR** | Each player's table is a mirrored anchor; opponent avatar seated across; their cards appear as virtual twins | Asymmetric realism (§5F). Latency tolerance is high thanks to WEGO structure — phases sync, not frames |
| **Full virtual (Option B env)** | Same netcode as remote; environment dial turned to 100% | |
| **Cross-platform** | OpenXR + engine-level abstraction; Demeo proves cross-platform is a top retention lever | Design for it from day one; don't bolt on |
| **Spectators** | Phone companion app = free spectator camera into the arena (mobile AR view of the same anchored scene), plus streamed "director cam" for remote viewing | Spectating is your marketing engine — every clip of the game is an ad |
| **Tournaments** | NFC card authentication + server-authoritative game state + deck registration via companion app scan | The WEGO structure is also anti-cheat-friendly: server validates all transitions; vision only *reports* card identity, server owns legality |

**Anti-cheat reality check:** the camera sees what it sees — a player could show a card the server thinks they don't own. Resolution: collection is server-authoritative (cards must be scanned-in/NFC-registered before play); live vision is an *input device*, not a source of truth. Physical counterfeits then only matter at in-person events → NFC check-in.

**Backend:** standard live-game stack — accounts/collection service (this is a database of owned card IDs; blockchain adds nothing you need), matchmaking, game-state service, telemetry. **Networking is locked to Photon Fusion** (matches Meta's official colocation sample stack → fastest path to a working prototype). Free tier is 100 CCU including commercial use, then ~$95/yr for 200 CCU — effectively free through prototype and alpha. The WEGO model means bandwidth is trivial (card plays and phase transitions, not per-frame transforms), so Fusion is comfortably overpowered for our needs. *Note:* keep game messages abstracted behind a thin transport interface so a later swap to Unity NGO (also more than capable for this low-tickrate game) is a small task, not a rewrite — but Fusion is the decision.

---

## 9. AI Content Pipeline — 500 Creatures Without 500 Artists

The 2026 toolchain genuinely changes the math, but the differentiator is the *system around* the tools:

**The pipeline (per creature, target: <1 day each at scale):**

1. **Design DNA first.** Build a creature taxonomy (factions × archetypes × silhouette language × material palette) *before* generating anything. AI without art direction produces the "AI look" — inconsistent, generic, instantly clocked by players. Your style bible is the moat.
2. **Concept:** image models fine-tuned/LoRA'd on your own commissioned style corpus (hire 2–3 concept artists to create the founding ~50 designs; train on those). Generate variations within DNA constraints; human art director selects and paints over.
3. **3D generation:** current leaders — **Tripo** (fastest, game-ready topology, built-in rigging), **Meshy** (broadest all-in-one: auto-rig + 500+ animation presets), **Rodin/Hyper3D** (highest fidelity, needs external rigging). Realistic expectation per 2026 production reviews: minutes to generate, then **1–4 hours human cleanup for hero assets**; commons may ship with light cleanup only.
4. **Rigging/animation:** auto-rig (Meshy/Tripo/AccuRIG) → shared humanoid/quadruped/flyer animation libraries → per-creature signature moves only (summon entrance, attack, death) hand-touched. Motion-matching + a small mocap day for signature animations pays for itself across hundreds of creatures.
5. **Budget discipline for XR:** every creature at LOD'd 10–30k tris, one texture atlas, mobile-class shaders — Quest is a mobile GPU rendering *two eyes at 90Hz on top of passthrough*. Bake this into the pipeline gate, not a later optimization pass.
6. **Voice/SFX:** AI voice for creature vocalizations and the "arena spirit" announcer is low-risk (non-human voices dodge the uncanny valley and most ethical concerns); commission the core soundtrack.
7. **Tiering:** ~60% commons (pipeline-dominant), 30% rares (heavy human polish), 10% legendaries (fully hand-crafted hero assets — these are your trailers, your box art, your reason to believe).

**Cost sketch:** a 300-card launch set ≈ 2 tech artists + 1 art director + 2 concept artists + tools (~$500–1,500/mo in generation credits) over ~9 months — versus 15–20 artist-years traditionally. The savings are real; spend them on the legendaries and on gameplay polish.

---

## 10. Technical Architecture

**Stack recommendation:**
- **Engine:** Unity 6 + Meta XR All-in-One SDK; OpenXR backend from day one (Android XR portability). Unreal is viable but the Quest MR sample/community ecosystem (QuestCameraKit, OpenCV for Unity, colocation samples) is Unity-centric — that ecosystem is months of saved work.
- **Vision subsystem:** Passthrough Camera API → on-device pipeline: fiducial detection (OpenCV ArUco-style, ~30fps) for live play + lightweight card-art classifier as redundancy → card pose + ID events into game logic. All on-device; no camera frames leave the headset (privacy is both policy requirement and marketing point).
- **Scene:** Meta Scene API for table/room mesh; Shared Spatial Anchors for colocation; anchor persistence for the "remembered arena."
- **Netcode:** Photon Fusion (Meta's own colocation sample stack), server-authoritative game state.
- **Backend:** managed cloud (accounts, collection registry, matchmaking, replays); nothing exotic required — the WEGO model makes state tiny.
- **Companion app (iOS/Android):** collection scan-in (camera+NFC), deck building, spectator AR camera, social/trading.

**Known technical risks, ranked:**

1. **Card readability under occlusion/glare/sleeves** — the fiducial frame design mitigates; mandate matte sleeves with printed fiducial-compatible borders as the sanctioned accessory (turn the constraint into product).
2. **Passthrough camera permission UX** — camera access requires explicit user grant; the consecration ritual doubles as the permission moment with clear framing.
3. **Two-headset households** — colocated play needs two Quests. 3S at $299 helps; design a strong async/remote and a "one headset + one phone-spectator" hot-seat variant for onboarding a headset-less friend.
4. **Quest thermal/perf ceiling** — passthrough + vision + creatures + effects. Budget from day 0: vision on a dedicated thread at 15–30Hz, creatures LOD'd, effects GPU-instanced.
5. **Platform dependency on Meta API stability** — camera API is new; keep the vision layer abstracted so Android XR's equivalent slots in.

---

## 11. Product Strategy & Roadmap

**Positioning:** not "a card game in VR" — *"the first trading card game where your real cards summon real monsters into your real room."* Physical cards are the product's soul, the revenue engine (packs!), and the moat (pure-digital competitors can't retrofit physicality).

**Phased plan:**

- **Phase 0 — Vertical slice (8–12 weeks):** 10 creatures, printed proto-cards with fiducial frames, one table, two Quest 3s, colocated battle with WEGO loop. *Success metric: two strangers finish a duel and immediately ask to play again.* This slice is also your pitch asset for funding/partners.
- **Phase 1 — Closed alpha (6 months):** 60 cards, remote play, companion scan-in, the environment dial (§5B). Small physical print run for testers.
- **Phase 2 — Launch (12–18 months):** 300-card set, ranked, spectator app, first physical product drop (starter decks + boosters, direct-to-consumer).
- **Phase 3:** Android XR port, Vision Pro (API-permitting), proxy-deck accessory (§5C), organized play.

**Honest risk assessment:** the fatal risks are not technical. They are (1) the two-headset requirement throttling the social loop early, (2) physical goods being a second business you must actually run, and (3) TCG design being its own decade-deep discipline — hire or deeply consult a competitive card game designer; the XR magic will not save an unbalanced game. The tech, in 2026, is ready.

---

## Appendix: Key Sources

- Meta Passthrough Camera API: [announcement](https://developers.meta.com/horizon/blog/new-era-mixed-reality-passthrough-camera-api-machine-learning-computer-vision/), [overview/specs](https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/), [public release coverage](https://roadtovr.com/meta-releases-quest-camera-access-for-developers-promising-even-more-immersive-mixed-reality-games/), [UploadVR](https://www.uploadvr.com/quest-passthrough-camera-api-experimental-out-now/)
- Marker/QR tracking on Quest: [QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit), [QuestArUcoMarkerTracking](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking), [QuestImageTracking](https://github.com/Aupuma/QuestImageTracking), [OpenCV for Unity Quest samples](https://github.com/EnoxSoftware/QuestWithOpenCVForUnityExample)
- Colocation & shared anchors: [Shared Spatial Anchors](https://developers.meta.com/horizon/documentation/unity/unity-shared-spatial-anchors/), [Colocation Discovery & Group Sharing](https://developers.meta.com/horizon/blog/colocation-discovery-group-sharing-shared-spatial-anchors-mixed-reality/), [Photon Fusion colocation sample](https://developers.meta.com/horizon/documentation/unity/unity-sample-colocation-fusion/)
- Selective passthrough: [Passthrough API overview](https://developers.meta.com/horizon/documentation/unity/unity-passthrough/), [occlusions](https://developers.meta.com/horizon/documentation/unity/unity-customize-passthrough-passthrough-occlusions/), [Cubism passthrough hands](https://www.uploadvr.com/cubism-passthrough-hand-tracking/)
- Vision Pro: [visionOS 26 enterprise camera access](https://www.uploadvr.com/apple-vision-pro-enterprise-raw-camera-access/), [WWDC26 object tracking](https://developer.apple.com/videos/play/wwdc2026/283/), [image tracking ~1Hz limitation (Apple dev forums)](https://developer.apple.com/forums/thread/757723), [additional dev-forum confirmation](https://developer.apple.com/forums/thread/765132)
- Android XR / Galaxy XR: [Samsung Galaxy XR (Wikipedia)](https://en.wikipedia.org/wiki/Samsung_Galaxy_XR), [mid-2026 report card](https://vr.org/articles/android-xr-mid-year-2026-report-card), [dev landscape](https://framesixty.com/android-xr-development/)
- Prior art: [Konami Solid Vision Experiment](https://www.uploadvr.com/yu-gi-oh-duel-links-vr-solid-vision-experiment/), [Konami VR/AI announcement](https://www.yugioh-card.com/eu/konami-announces-a-new-yu-gi-oh-digital-title-and-unveils-exciting-new-vr-and-ai-projects-for-yu-gi-oh-digital-titles/), [Tilt Five](https://www.uploadvr.com/hands-on-tilt-five-tabletop-ar/), [Malkyrs NFC cards](https://www.nintendolife.com/news/2018/12/malkyrs_the_interactive_card_game_will_bring_trading_cards_to_life_on_switch_using_nfc_tech), [Lightseekers](https://www.lightseekers.cards/digital-trading-card-game.html), [Flex NBA](https://techcrunch.com/2021/05/11/sequoia-games-looks-to-capitalize-nba-top-shot-fever-with-their-own-ar-tabletop-game), [Demeo on Galaxy XR](https://gamespace.com/all-articles/news/demeo-launches-on-samsung-galaxy-xr-bringing-tabletop-adventure-to-mixed-reality/), [Battlemarked design discussion](https://voicesofvr.com/1665-resolution-games-battlemarked-blends-mixed-reality-social-features-with-demeo-and-dd-gameplay/)
- AI 3D pipeline: [Meshy tools comparison](https://www.meshy.ai/blog/best-ai-tools-for-3d-game-assets), [Tripo](https://www.tripo3d.ai/), [2026 generator testing](https://www.indiehackers.com/post/best-ai-3d-model-generator-in-2026-i-tested-9-of-the-best-and-here-is-what-i-found-70ecab1a0a), [Meshy production workflow review](https://medium.com/data-science-in-your-pocket/meshy-ai-3d-generator-review-2026-the-complete-production-workflow-tested-272df212c925)
