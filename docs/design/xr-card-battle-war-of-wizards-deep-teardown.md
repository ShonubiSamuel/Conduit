# War of Wizards — Deep Teardown & Head-to-Head With Our Design

*Developer: Arcane Miracle Entertainment. Released Sept 21, 2022 (PCVR/Steam), later Quest. A competitive VR spell-drawing MOBA. Meta Store ~4.7/5 across ~5,900 reviews (very popular on Quest); Steam "Mixed," 69% of ~140. Sourced from two full review walkthroughs + Steam criticism + store docs. This is the closest existing product to ours, so every mechanic is dissected and mapped to a decision.*

---

## 1. Exactly what the player does (the mechanics, precisely)

### Controls — brutally minimal (3 inputs)
This is the headline: **the entire game runs on three controller inputs.**

- **Trigger (hold):** hold and *move the whole controller through the air* to draw a rune/magic circle. The drawn path stays visible as a glowing trail. "It doesn't have to look pretty — as long as the pattern is right, the system recognizes it."
- **Grip (press):** after drawing, press to *cast/activate* the drawn spell.
- **A button:** fires a small always-available bolt (chip damage, finishing a fleeing low-HP enemy, and — see below — shooting spell-pickups out of the sky).

That's it. A reviewer's line worth remembering: *"you could hand this to a kid at a family gathering and they'd still look like a decent archmage."* Radical accessibility is the game's foundation.

### The spell-draw loop
- Your **tower displays your currently-equipped spells** and their rune shapes — so you draw *while looking at the reference*. There's also an in-game **skill book** so you never memorize shapes. (Onboarding solved.)
- Each cast is **graded on accuracy + size**: you get a letter rank (up to S). **Below C rank the spell fails to fire at all** — a hard skill floor. Bigger + more accurate = stronger.
- **Mana** replenishes over time and through combat; casting draws it.

### The MOBA layer
- It's **tower defense / MOBA**, not a duel-in-a-void. You have a **small tower** (auto-attacks minions) and a **main tower/Crystal** (destroy the enemy's to win).
- **Summoning spells** spawn **minions/lane creeps** that march at the enemy base; **Giant/boss summons** are high-HP threats that must be focused down with Fireballs/rapid spells or "your minions get crushed almost instantly."
- **Offensive spells don't damage towers directly** — they exist to clear minions and disrupt advances. Towers fall to minion pressure. (Clean MOBA logic.)
- **Spell pickups fall from the sky** mid-match as orbs; shoot them with A to bank them into your tower's **limited-capacity loadout** — an in-match drafting/resource layer ("one smart pick can turn the match"). 

### Player embodiment & the "ultimate"
- Players can be **directly attacked and killed** (by enemy spells, minions, and opponents); death = **respawn timer** during which you can act only if you have a **revival spell** banked.
- **God's Wrath (ultimate):** you *become a giant wielding a battle axe* and swing at enemies — the player is temporarily **embodied as a monster**. Also ranged summons like an attack dragon, and **meteor AoEs** you must "block or spread out" to survive.

### Modes & meta
- **1v1** (main ranked), **2v2** (voice-coordinated), **Duel Arena** (no towers, first to 3 KOs — pure combat), **Play with Friend**, **Practice vs AI**, **Magic Shop** (buy spells w/ earned tokens), **Avatar & Staff** (pure cosmetic).
- **Rank-based matchmaking**; hot-mic voice throughout (trash talk pre-match is normal).

---

## 2. How recognition & targeting actually work

- **Recognition = controller-trajectory shape-matching.** The system matches the *path the controller travels* against a library of rune templates, scored on shape-accuracy and scale. No hand tracking, no cameras, no cards — the controller's tracked motion IS the input. This is why it's so robust: 6DoF controller pose is rock-solid, unlike hand tracking or vision.
- **Targeting is mostly non-manual / lane-based.** You largely don't pick targets — spells fly forward down the lane, minions auto-path, towers auto-fire, AoEs land where aimed. The A-button bolt is manually aimed. There's little "select that specific unit" — the MOBA lane structure removes the targeting problem rather than solving it.
- **Defense = draw a shield spell OR physically dodge.** The Christmas build shows a drawn **defensive shield blocking a dragon's fire**; meteors are survived by **stepping aside / spreading out**. So defense is a mix of *cast-a-shield* (stationary, gesture) and *physically relocate* (the comfort problem).

---

## 3. What players LOVED

- **Draw-to-cast is the magic** — universally the most-praised element; it delivers the "I'm actually a wizard" fantasy that button-presses can't. High Meta ratings and ~6k reviews prove real, sustained appeal.
- **Radical control simplicity** — 3 inputs; instantly learnable; "hand it to a kid."
- **No memorization** — tower reference + skill book let you focus on strategy, not recall.
- **Accuracy/size skill expression** — casting *well* under pressure feels earned; the C-rank floor gives depth.
- **MOBA strategy layer** — loadouts, lane pressure, giant threats, sky-pickup drafting → "one more match" addiction; the reviewer literally describes losing track of time.
- **Social electricity** — hot mics + trash talk + 2v2 coordination create presence.
- **Player-embodied ultimate (God's Wrath)** — becoming the giant is a standout "feels unfair in the best way" moment.

## 4. What players HATED (the goldmine)

- **Comfort / space demands — the #1 recurring gripe.** Dodging = **stepping side to side**; meteors force **spreading out**. In a small room "the whole experience just doesn't feel as good," "movement feels restricted and it gets frustrating fast." Comfort is rated only mild-to-poor *specifically* because of physical dodging.
- **Balance failures** — "spell balancing is horrid"; **2,500-damage spells with no counter**; some attacks near-impossible to dodge. Uncounterable burst = rage.
- **Brutal economy / pay-for-advantage** — a spell can cost **~16,600 gold when you earn ~700 to start**; "dozens of hours to unlock a single spell" while others pay for a head start. Widely called a bad, grindy, pay-advantaged economy. **Spells are power, and they're gated behind grind or money — the cardinal P2W sin.**
- **Matchmaking mismatches** — new players thrown against top-tier; 2v2 skill-gap "carry" problem.
- **Toxicity** — hot mics with no buffer; "players who get under your skin."
- **Platform favoritism** — Steam users report deliberately disabled functionality vs Meta; store-war resentment.

---

## 5. Head-to-head: War of Wizards vs OUR design

| Dimension | War of Wizards | Our design | What it means |
|---|---|---|---|
| **Input** | 3 controller buttons; trajectory shape-match | Physical cards + hands + voice + gaze (+ optional controller) | They're **far simpler to build** — you were right. We take on card-vision + occlusion + speech precisely to buy *depth and physicality* they can't have. Their success proves the *fantasy* sells; our bet is that real cards deepen it |
| **Recognition robustness** | Very high (controller pose is rock-solid) | Harder (vision, speech) — but we can lean on the same trick: **controller/hand trajectory for sigils is as reliable for us as for them** | Adopt their most robust mechanic (trajectory draw) for our Script tier; don't reinvent it |
| **Draw-to-cast** | Core, beloved, one-dimensional | One of four rungs (Sign/Word/Script/Ritual) | **Validated** — build it, but as range, not the whole game |
| **No-memorization aid** | Tower reference + skill book | Adopt: sigil prompts/guides | Steal outright |
| **Accuracy/size = power** | Yes, with C-rank fail floor | Our "commitment ladder" (bigger/slower/fuller = stronger) is the same idea | Convergent; add their **fail-floor** so sloppy casts have stakes |
| **Dodging** | **Physical side-to-side stepping → their worst complaint** | **No relocation ever;** telegraphed → **plant feet, block with gesture** (Until You Fall model); attacks mostly hit your monsters; small lean/duck optional | **Their #1 failure is our core thesis, now confirmed by ~6k-review-scale player data.** This is the most important single lesson |
| **Targeting** | Lane-based; barely any manual target select | Grammar + gaze "attack *that one*" (Put-That-There) — real per-unit command | We offer richer command than they do — a differentiator, but adds complexity |
| **Player embodiment** | God's Wrath = become a giant | Consider a **capstone "avatar" moment** (rare, earned) — becoming/merging with your monster | Steal the *feeling*, gate it behind mastery not purchase |
| **Balance** | Uncounterable 2,500-dmg spells | Server-side live balance + sim harness + **"every effect must have a counter" invariant** | Their pain is literally why our balance-math doc exists |
| **Economy** | **Spells = power, gated behind grind/$ → P2W** | **Two-lane rule: money buys cards/cosmetics/PvE, NEVER a PvP number; ≤60/40 collection guardrail** | Their economy is the exact trap our progression doc forbids. Cautionary tale #1 |
| **Matchmaking** | New vs top-tier; 2v2 carry | Rating + collection brackets + newbie preconstructed lobbies | Fix what they broke |
| **Voice** | Hot mics, no moderation | Gated mic for commands + proximity social voice **with mute/report/clean-lobby from day one** | Keep the electricity, add the guardrails |
| **Comfort rating** | Mild-to-poor (dodging) | Designed for seated/small-space | Our comfort is a marketing wedge against the category leader |
| **Platform** | Favoritism, store war | Cross-platform parity as a principle | Don't repeat their community-splitting mistake |

---

## 6. On the dodge question specifically (your core curiosity)

You asked how their dodge works and whether we still need *some* dodging. Now precise: **their dodge is lateral stepping** — you physically move your body side to side out of a projectile/AoE's path, and for big meteors you "spread out." It is **not** jumping or acrobatics; it's room-scale displacement. And critically, **they already offer the alternative we favor**: drawing a **shield spell** to block instead of moving. Their mistake isn't *having* dodging — it's making **stepping the primary survival tool**, which is what tanks their comfort score and breaks small-room play.

Our resolution (unchanged but now evidence-backed):
- **Primary defense = gesture (shield/ward/block), feet planted** — the thing WoW offers as a secondary, we make primary.
- **Allowed micro-dodge = lean, duck, weight-shift in place** — expressive, optional, never room-scale, never required. A telegraphed attack can be *fully* neutralized without a step.
- **Most attacks resolve on your monsters,** so personal dodging is an occasional dramatic beat, not the core loop.
- **The summoning-circle** keeps you anchored (lore + safety), so "spread out across the room" is simply not a mechanic.

In one line: **War of Wizards proves people love drawing spells and hate having to run from them — so we keep the drawing and delete the running.**

---

## 7. The five things to STEAL and five to AVOID

**Steal:** (1) trajectory-drawn casting as our Script tier — it's the most robust input they have; (2) the on-screen rune reference + skill book so nobody memorizes; (3) accuracy/size-scaled power *with a fail-floor*; (4) the mid-match sky-pickup drafting layer as a great tension mechanic worth adapting; (5) a rare **player-embodiment capstone** (their God's Wrath) as an earned mastery moment.

**Avoid:** (1) **making physical stepping the survival mechanic** (their fatal comfort flaw); (2) **spells/power behind grind or purchase** (their P2W economy — our two-lane rule exists to prevent exactly this); (3) **uncounterable burst** (their 2,500-dmg problem); (4) **hot mics with no moderation**; (5) **platform favoritism / community-splitting**.

---

## Sources
- Full mechanics + controls + modes walkthrough: [VRPUPU Quest review (2026)](https://vrpupu.com/en/2026/02/war-of-wizards-review-meta-quest-vr-moba/) (embedded gameplay video: youtu.be/mdYBxIwQzmY)
- Older-build mechanics (crystal/mana/patterns): [VR Today Magazine review](https://vrtodaymagazine.com/war-of-wizards-vr-review/)
- Criticism (balance, economy, matchmaking, platform): [Steam reviews hub](https://steamcommunity.com/app/2009460/reviews/?browsefilter=toprated) · [Steam store page](https://store.steampowered.com/app/2009460/War_of_Wizards/)
- Store/ratings: [Meta Quest store](https://www.meta.com/experiences/war-of-wizards/7865834933488042/) · [Metacritic](https://www.metacritic.com/game/war-of-wizards/)
