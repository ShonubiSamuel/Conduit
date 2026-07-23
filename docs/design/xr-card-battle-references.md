# XR Card Battle Game — Reference & Inspiration Pack

*A curated library of the closest real-world precedents, tech demos, and anime/film touchstones for the game we're designing. Watch/read these to generate ideas — each entry has a one-line note on **why it matters to us**.*

---

## ⭐ Tier 1 — The closest precedents (watch these first)

- **Konami — Yu-Gi-Oh! Duel Links "Solid Vision Experiment" (Quest 3)** — [YouTube](https://www.youtube.com/watch?v=ZTA4ygTRysw) · [UploadVR write-up](https://www.uploadvr.com/yu-gi-oh-duel-links-vr-solid-vision-experiment/)
  *This is literally our core fantasy — real monsters (Blue-Eyes, Dark Magician Girl) summoned into 3D space on a Quest 3. Konami built it and did NOT productize it. Study what lands and what's missing; this is the whitespace we're filling.*
- **HADO — the world's first AR techno-sport** — [Official site](https://hado-official.com/en/) · [How to play](https://hado.nl/en/game-explanation/) · [Forbes feature](https://www.forbes.com/sites/bernardmarr/2019/10/23/hado-the-amazing-ways-augmented-reality-is-changing-sport/)
  *The single best proof of our combat model. Players stand in an arena, throw energy balls and raise shields with arm + wrist-sensor gestures, and dodge by leaning/bending — NOT running. This validates stationary gesture-combat with real players paying to play. Watch tournament footage on YouTube ("HADO tournament").*
- **Demeo (Resolution Games)** — [Official](https://www.resolutiongames.com/demeo) · [TheGamer review](https://www.thegamer.com/demeo-review/)
  *The gold standard for comfortable, seated, social MR tabletop play. Our "why the social table is the product" north star. Note how players lean on the virtual table and relax.*
- **Tilt Five — holographic tabletop AR** — [Hands-on (UploadVR)](https://www.uploadvr.com/hands-on-tilt-five-tabletop-ar/) · [Digital Trends "Holochess"](https://www.digitaltrends.com/gaming/tilt-five-tabletop-video-gaming/)
  *Dedicated tabletop-AR hardware. Great for how holograms sit on a real table and how two people share that space socially.*

---

## Tier 2 — Card recognition & passthrough tech (see the tech working)

- **QuestCameraKit (Roberto Coviello / Meta)** — [GitHub](https://github.com/xrdevrob/QuestCameraKit)
  *Reference projects for the Passthrough Camera API: QR tracking (~70fps), object detection, LLM vision. This is the exact tech stack for reading our cards. Repo has demo clips.*
- **QuestArUcoMarkerTracking (Takashi Yoshinaga)** — [GitHub](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking)
  *Single/multi fiducial-marker tracking on Quest 3 with OpenCV — the basis for our "rune-frame as fiducial" card design.*
- **QuestImageTracking** — [GitHub](https://github.com/Aupuma/QuestImageTracking) · **OpenCV-for-Unity Quest samples** — [GitHub](https://github.com/EnoxSoftware/QuestWithOpenCVForUnityExample)
  *Image/artwork tracking on the passthrough feed — the redundancy layer behind the fiducial.*
- **Meta — "New Era of Mixed Reality with the Passthrough Camera API"** — [Meta blog](https://developers.meta.com/horizon/blog/new-era-mixed-reality-passthrough-camera-api-machine-learning-computer-vision/) · [Now shippable on store (UploadVR)](https://www.uploadvr.com/quest-passthrough-camera-api-now-shippable-on-store/)
  *The announcement + capabilities that make this whole game possible on consumer hardware.*
- **Brown VR wiki — scanning codes with passthrough** — [guide](https://www.vrwiki.cs.brown.edu/vr-development-software/unity/using-passthrough-camera-webcam-texture-in-unity/scanning-codes-with-passthrough)
  *Practical how-to for reading codes off the Quest camera.*

---

## Tier 3 — Stationary VR combat & spellcasting (the player's role)

- **Until You Fall** — [RoadToVR review](https://roadtovr.com/until-you-fall-oculus-quest-review/) · [Combat gameplay video](https://www.roadtovr.com/until-you-fall-gameplay-video-vr-sword-combat/)
  *The blueprint for telegraphed, block-with-a-gesture, stationary combat. A one-second indicator tells you how to defend; you plant your feet and block. Exactly our "you can't run, you repel" defense model.*
- **Waltz of the Wizard: Natural Magic (Aldin)** — [RoadToVR hands-on](https://roadtovr.com/hands-waltz-wizard-natural-magic-flexible-clever-sorcery-massive-dungeon/) · [Quest hand-tracking impressions](https://www.uploadvr.com/waltz-wizard-magic-quest/)
  *Best-in-class "natural" gesture spellcasting — casting from a fixed spot with your hands. Our summoner-caster feel.*
- **The Wizards / The Wizards – Dark Times** — search "The Wizards VR gesture casting gameplay"
  *Gesture-sequence spellcasting (draw a shape → cast). A model for ritual summons and incantations.*
- **Blade & Sorcery** — search "Blade and Sorcery gameplay" *(caution)*
  *Amazing physicality, but it's the OPPOSITE of our comfort goal — full-body, exhausting. Useful as the boundary of what we're deliberately NOT doing.*

---

## Tier 4 — Commander / summoner / RTS-from-a-fixed-vantage

- **Final Assault** — ["Conductor of War" hands-on (UploadVR)](https://www.uploadvr.com/final-assault-hands-on-become-a-conductor-of-war-in-this-vr-rts-meets-moba/)
  *You orchestrate a battlefield from a stationary vantage — the comfortable, satisfying commander seat.*
- **Brass Tactics (Hidden Path)** — [Design deep-dive (Game Developer)](https://www.gamedeveloper.com/design/brass-tactics-evolving-classic-rts-interactions-for-vr---part-2)
  *Grab-point-release unit commands. The gesture grammar for directing your monsters.*
- **Skyworld** — [Best VR strategy roundup](https://www.thegamer.com/best-virtual-reality-strategy-games/)
  *Accessible VR card/strategy hybrid; commander comfort balance.*
- **Summoner/pet class design reading** — [10 games with a summoner class (TheGamer)](https://www.thegamer.com/video-games-with-summoner-pet-familiar-classes/) · [Pantheon "puppet master" dev diary](https://www.pantheonmmo.com/news/dev-diary-summoner-class-refresh/)
  *Why pure-passive summoners feel boring, and how City of Heroes Masterminds keep it active — directly informs our active-support kit.*

---

## Tier 5 — Colocation & shared-space MR multiplayer

- **Meta — Colocation Discovery & Group Sharing** — [Meta blog](https://developers.meta.com/horizon/blog/colocation-discovery-group-sharing-shared-spatial-anchors-mixed-reality/) · [Shared Spatial Anchors docs](https://developers.meta.com/horizon/documentation/unity/unity-shared-spatial-anchors/)
  *How two headsets in a room share one battlefield with near-zero setup. Our same-room mode.*
- **Photon Fusion — MR local multiplayer sample** — [Photon docs](https://doc.photonengine.com/fusion/current/industries-samples/fusion-mixedreality-localmultiplayer)
  *Our chosen netcode, with a ready-made colocated MR sample.*
- **VirtualGo — join a physical session as remote VR** — [UploadVR](https://www.uploadvr.com/virtualgos-mixed-reality-multiplayer-system-lets-people-join-your-session-as-vr/)
  *Elegant model for mixing in-room + remote players — relevant to our spectator and remote modes.*

---

## Tier 6 — Real hands & real cards inside virtual worlds

- **Cubism — passthrough hands** — [UploadVR](https://www.uploadvr.com/cubism-passthrough-hand-tracking/)
  *Selective passthrough keeping real hands visible over virtual objects — the technique for keeping your real cards & hands visible in our VR arenas.*
- **Meta — hand-tracking best practices (gorilla-arm fatigue)** — [Meta design docs](https://developers.meta.com/horizon/design/hands-best-practices/)
  *The ergonomics bible: keep gestures arm's-reach, at/below heart level, brief. Our comfort rulebook.*

---

## Tier 7 — AI creature-content pipeline (see the workflow)

- **Meshy — AI 3D for game assets (all-in-one: auto-rig + 500+ animations)** — [Tools comparison](https://www.meshy.ai/blog/best-ai-tools-for-3d-game-assets) · [Tutorials](https://www.meshy.ai/tutorials) · [Meshy-guide GitHub](https://github.com/meshy-dev/Meshy-guide)
- **Tripo AI — fast game-ready generation** — [Site](https://www.tripo3d.ai/) · [Game-ready models tutorial (YouTube)](https://www.youtube.com/watch?v=YuH455-CyrM) · [Tripo vs Meshy](https://www.tripo3d.ai/compare/tripo-vs-meshy)
  *Watch a full text/image → rigged → engine workflow to feel how fast a creature can go from prompt to fighting in-game.*

---

## Tier 8 — Anime touchstones (the emotional target)

- **Yu-Gi-Oh! Duel Monsters** — the original. The reveal, the trap-card gasp, "I summon…", life points. Our emotional baseline.
- **Yu-Gi-Oh! VRAINS** — duels set *inside VR*. Directly relevant to our VR-arena mode and how digital duels can feel embodied.
- **Yu-Gi-Oh! ARC-V — "Action Duels"** — solid-vision fields duelists physically move through. Great for field-scale spectacle (study it, but note we deliberately avoid the running-around part for comfort).
- **Cardfight!! Vanguard** — clean modern TCG-anime summoning language and stances.
- **Duel Masters** — fast, aggressive card-battle energy; good pacing reference.
- **Bakugan Battle Brawlers** — cards/creatures that "pop" out of a physical object into a battlefield — very close to our card-to-monster moment.
- **Shaman King** — spirit-summoner bond; the human channels the fighter (our summoner-and-monster relationship).
- **Digimon / Pokémon** — the partner-creature bond and "go, fight for me" dynamic; the trainer as active commander.
- **Solo Leveling** — YOUR reference: a summoner who *levels up his own powers* and commands a shadow army. The player-progression fantasy we discussed.
- **Sword Art Online** — full-dive VR immersion and the emotional stakes of being *inside* the game.

## Tier 9 — Film touchstones (staging, casting, spectacle)

- **Doctor Strange** — gesture-based spellcasting and the mandala shield. Our defensive-barrier gesture, basically on screen. Study the hand language.
- **Ready Player One** — social shared virtual arenas and mixed physical/virtual play; the culture around it.
- **Ender's Game** — the holographic strategy command table and the Battle Room. The commander-from-a-vantage fantasy and 3D spatial tactics.
- **Real Steel** — a human controlling/commanding a giant fighter from the sidelines with gestures. The you-and-your-monster bond and coaching feel.
- **Pacific Rim** — kaiju-scale spectacle and human-monster linkage; scale drama for colossal summons.
- **Star Wars — Dejarik (holochess)** — the iconic "creatures battling on a table between two players" image. The tabletop mode's visual DNA.
- **Jumanji / Zathura** — a game whose effects invade the real room. Our "environment transformation" fantasy.

---

## How to use this pack

1. **Start with Tier 1** — Konami's demo (what to beat) and HADO (proof our combat works).
2. **When designing combat/player role**, revisit Until You Fall + Waltz of the Wizard + Doctor Strange scenes together.
3. **When designing summoning/commanding**, watch Brass Tactics/Final Assault + a Yu-Gi-Oh summon montage back-to-back.
4. **Keep a running "steal this" note** — every time a video sparks an idea (like Until You Fall did), jot the timestamp and the mechanic. That list becomes our design-inspiration log.
