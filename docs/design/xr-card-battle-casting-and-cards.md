# Card Anatomy & The Casting Language — v0.1

*The physical card design, the keyword vocabulary, and the three-mode casting system (Word / Sign / Script). Companion to the balance-math doc — every mechanic here is priced in its five currencies (energy, stats, life, time, attention).*

---

## Part 1 — Card Anatomy (the physical object)

Layout of a monster card, front:

```
┌─────────────────────────────┐
│ ◆ COST        NAME      ⬡ │   ◆ energy cost (top-left, glanceable in fan)
│ ┌─────────────────────────┐ │   ⬡ Aspect gem (color + icon, colorblind-safe)
│ │                         │ │
│ │        FULL ART         │ │   ← the collectible layer
│ │                         │ │
│ └─────────────────────────┘ │
│  RUNE RING ═══════════════  │   ← the machine-readable fiducial, styled
│                             │      as summoning runes (encodes card ID
│  CLAN SIGIL   ⚔ ATK  ♥ HP  │      + orientation; readable across table)
│  ─────────────────────────  │
│  KEYWORDS (icons + words)   │   ← e.g. 🛡 Bulwark · ⚡ Swift
│  Effect text (short!)       │
│  ─────────────────────────  │
│  "Incantation line"         │   ← the spoken summon phrase, printed
│  RARITY ✦ · SET/№ · [NFC]  │
└─────────────────────────────┘
```

Design rules:
- **The incantation line is printed on the card.** ("*From cinder and wrath — Emberdrake, ignite!*") This is how players *learn their chants* — the card teaches its own theater. Speaking it at summon grants the Word bonus (Part 3). This also makes every card quotable — community catchphrase fuel.
- **Six anatomy fields are identity (printed, immutable):** name, art, Aspect, Clan, keywords, incantation. **Two are live (server-side):** cost and ATK/HP — printed values are launch values; the app renders current values on/above the card. This is how we balance-patch physical cards.
- **Glanceability order** (what you must read in a fan of 5 at arm's length): cost > Aspect gem > art silhouette > name. Stats and text are for the inspect-on-gaze zoom (look at a card 0.5s → hologram enlarges it).
- Spell/Support cards use the same frame minus ATK/HP, with a **cast-mode icon strip** showing which casting modes it accepts (Word/Sign/Script — Part 3).

## Part 2 — Keyword Vocabulary (launch set: 10)

Keywords are the shared grammar that makes cards readable at a glance and gives the community its vocabulary. Each is priced via the balance doc's SP-equivalents and taxed by Aspect (∞ where identity forbids):

| Keyword | Effect | Cheap for | Forbidden (∞) |
|---|---|---|---|
| **Bulwark** | Blocks 2 attackers instead of 1 | Bastion | Gale |
| **Swift** | Attacks every 2s instead of 3s | Gale, Ember | Bastion |
| **Pierce** | Excess damage carries to the summoner | Ember | Verdant |
| **Ward X** | Absorbs first X damage, then breaks | Bastion, Tide | Ember |
| **Regrow X** | Heals X per 5s | Verdant | Ember, Umbra |
| **Ambush** | Enters face-down; strikes 2× on reveal | Umbra, Gale | Bastion |
| **Drain** | Damage dealt heals your summoner half | Umbra | Gale |
| **Torrent** | On death, applies its Aspect charge to killer (reaction fuel) | Tide | — |
| **Swarm X** | Summons X 1/1 tokens on entry | Verdant | Bastion |
| **Soul-link** | Bonus when its named partner card is fielded (some links are *hidden* — discoverables) | any | — |

Rule: a card body may carry max 2 keywords (legendaries 3). Keywords appear as icon + word on cards and as *visual tells* on the hologram (a Bulwark monster visibly plants; an Ambush monster shimmers face-down) — the battlefield must be readable without reading text.

## Part 3 — The Casting Language: Word, Sign, Script ⭐

The player's magic (support/boost, per our monster-forward rule) is cast through three input modes forming a **commitment ladder** — more time and skill in, more power out. This is the anti-spam system *and* the skill-expression system *and* the anime theater, all in one mechanism:

| Mode | Input | Time | Power | Interruptible? |
|---|---|---|---|---|
| **1. Sign** (Naruto-style) | A recognized hand pose or 2-pose sequence | ~0.5–1s | 1.0× base | No — too fast |
| **2. Word** (spoken) | The spell's name/phrase: *"Ember Ward!"* | ~1–1.5s | 1.3× | No |
| **3. Script** (drawn sigil) | Trace the spell's glyph in the air (a cross, a spiral, a triangle...) | ~2–3s | 1.7× | **Yes** — visible glowing stroke |
| **Ritual** (Script + Word) | Draw the sigil, then speak the phrase to "seal" it | ~4s | **2.2×** + visual spectacle | **Yes** — the biggest tell in the game |

How it plays:
- Every castable effect (mostly **attachments**: wards, ATK boosts, heals, hexes) can be cast at *any* rung — same energy cost, different output. Rushed defense under pressure: throw the Sign, take the 1.0×. Safe window: invest 4 seconds in the full Ritual for 2.2×.
- **Script casts glow as you draw them** — your opponent *sees* the sigil forming and can react: rush the attack, trigger their own counter-Sign, or force you to abandon the stroke. Big casts telegraph themselves; this is the counterplay drama (and it mirrors Mushoku Tensei: long chants = vulnerability).
- **Activation gate (your "activate the writing first" idea):** Script mode must be *opened* — off-hand held palm-up, or a spoken "activation" word — so the system never misreads idle hand motion as casting. This is also the tech solution to false positives.
- **Chantless mastery (progression):** high-rank players unlock the ability to cast one school's Word-tier silently (the Mushoku Tensei chantless fantasy) — same 1.3× power, no audio tell. Prestige skill, tournament-relevant, and the public-play accessibility path.
- **Casting targets monsters, mostly.** The ladder casts attachments: *Ember Ward* (shield on your Drake), *War-howl* (ATK boost), *Rootbind* (enemy hex). Direct player-cast damage exists only as the rare finisher (per the combat model). The monsters stay the stars; you are the amplifier.

### Anti-spam economics (why it can't degenerate, without ever feeling turn-based)
Four throttles stack — all continuous, none turn-like:
1. **Energy:** every cast draws from the same regenerating pool as summons — boosting more means summoning less (a real strategic tradeoff, not a limiter).
2. **Time-in-motion:** the ladder's cast times are the rate limit — a 2.2× Ritual occupies your hands and attention for ~4s; you physically *cannot* spam it.
3. **School cooldowns:** after casting a school (defense/offense/hex), that school cools ~6–10s. You can always do *something*, never the *same* thing twice back-to-back.
4. **Diminishing echoes:** repeating the identical spell within 15s yields 0.7× → 0.5×. Variety is mathematically optimal.

Net feel: there is always an action available (~every 5–10s: a summon, a Sign, a command, a card draw), but optimal play is a *rhythm* of varied actions, not a mash. Real-time, never turn-based, never spam.

### Tabletop compatibility ✔
Every rung is seated-and-desk-scale by design: Signs are hand poses (fine seated), Words are spoken at conversation volume (fine at a table — and *great* social theater there), Scripts are drawn in the ~40cm of air above the table, Rituals combine the two. Nothing needs standing. In tabletop mode the sigil hangs glowing over the board — visible to both players — before sealing. Identical rules, identical inputs, cozier stage. (And the silent path — Signs + chantless mastery — covers public spaces and accessibility.)

### Tech feasibility ✔
- **Sign recognition:** static/sequential hand poses via Quest Interaction SDK custom poses — shipping tech (Waltz of the Wizard does full hand-gesture magic today).
- **Script recognition:** air-drawn stroke matching is a solved problem (classic $1 recognizer-family algorithms on the hand-tracked fingertip path); the activation gate kills false positives; the glowing stroke gives the player live feedback that also doubles as the opponent's telegraph.
- **Word recognition:** on-device keyword spotting (small vocabulary = high accuracy, no cloud, no privacy issue). Incantation phrases are designed to be phonetically distinct from each other.

---

## Part 4 — Open questions for next session
1. **Sigil alphabet:** how many glyphs at launch? (Recommend ~8: one per spell school, shapes chosen for drawability + mutual distinctness: cross, circle, spiral, triangle, zigzag, wave, arrow, hourglass.)
2. Do **Aspects get unique Signs** (Ember's snap vs Tide's flow) for flavor, or shared Signs for learnability? (Recommend: shared at launch, Aspect-flavored VFX.)
3. Should **summons** also accept the ladder (whispered summon vs full incantation = small stat bonus), or keep the ladder support-only? (Recommend: yes for legendaries only — the full-Ritual legendary summon is the trailer moment.)
4. Playtest question: is 2.2× the right Ritual ceiling, or does it warp play toward turtling? (Sim harness can test.)
