# Deckbuilding Rules & Player Progression — v0.1

*Grounded in comparative TCG research (deck sizes, copy limits, banlist philosophies) and free-to-play progression ethics. Companion to the balance-math and Aspect-system docs.*

---

## Part 1 — Deckbuilding Rules

### What the established games teach

| Game | Deck | Copies | Why |
|---|---|---|---|
| MTG | 60 min | 4-of | Started at 40 with no copy limit — proved *too consistent, too combo-prone*, so both knobs were raised |
| Yu-Gi-Oh | 40–60 | 3-of | Competitive players almost always play exactly 40 — **players always minimize for consistency** |
| Hearthstone | exactly 30 | 2-of (1 legendary) | Designed "sweet spot": consistent draws without running out of steam |

Core tension: small decks = consistency (your strategy shows up every game); big decks = variety (matches feel different). Players *always* build at the minimum, so the minimum IS the deck size — design it deliberately.

### Our constraints are unusual — and they all point the same direction

1. **Real-time duels draw fewer cards than turn-based.** At our pacing (open with 5, draw ~1 per 30s, 12–18 min duel) a player sees ~25–35 cards per duel. A 60-card deck would mean never seeing half your deck — strategy becomes lottery.
2. **Physical handling in a headset.** You hold, fan, and draw real cards while casting with the other hand. Shuffling 60 sleeved cards standing up in passthrough is friction; 30 is manageable. (No paper TCG ever had to price in *ergonomics* — we do.)
3. **Physical economics.** Smaller deck = cheaper complete starter product (~30-card preconstructed deck at an accessible price) = lower entry barrier for a new physical game. MTG-scale 60+15 sideboard economics would strangle a launch IP.

### v0.1 Rules

- **Deck: exactly 30 cards.** (Hearthstone-proven consistency point; matches our draw math and hand ergonomics.)
- **Copies: max 2 of any card; legendaries max 1.** With 30 cards, 2-of ≈ MTG's 4-of-in-60 density — same consistency feel, proven by Hearthstone.
- **Aspect identity (the wheel as law):**
  - Choose one **primary Aspect** — minimum 20 of 30 cards.
  - Up to 10 cards from ONE **allied Aspect** (adjacent on the wheel) — the "splash."
  - **Opposed Aspects can never share a deck** (Ember/Tide, Gale/Bastion, Umbra/Verdant). This is cosmology as rules — the lore and the deckbuilding teach each other.
  - A small **Clanless/neutral pool** (~10% of each set) playable in any deck — the glue cards.
- **Structure quotas: none.** No forced monster/spell ratios — let deckbuilders discover degenerate all-spell decks and let the balance loop respond. (Quotas are patches on pricing failures.)
- **Formats at launch: one** (Standard). Add rotation/eternal formats only when the card pool demands it (~year 2+). Premature format-splitting fragments a small playerbase.

### Banlist philosophy: "nerf-first, ban-almost-never"

The research spectrum: paper games (MTG, Yu-Gi-Oh) ban/restrict because printed cards are immutable; digital games (Hearthstone, Runeterra) nerf instead, keeping cards playable. **We have live balancing (server-side cost/stats on physical cards) — so we get the digital philosophy with physical cards:**

1. **First lever: re-cost.** A problem card gets +1 energy overnight. Card stays playable, collection value intact.
2. **Second lever: stat adjustment.**
3. **Emergency-only: suspension** (temporary, with a stated review date) — for genuinely broken *mechanics* (usually an unforeseen interaction), while a fix is designed. Never permanent bans of purchased physical product if avoidable.
4. **The collector's promise, stated publicly:** *"No card you own will ever become unplayable. The worst that can happen to your card is that it gets fairly priced."* No paper TCG can make this promise. It should be printed on the box.

---

## Part 2 — Player Progression (the Solo Leveling question)

### The cautionary tale that settles the debate

Clash Royale is the definitive experiment in "player level grants power": card/king levels rise with play and payment. Result, per years of community consensus — even skilled free players "hit a wall where an opponent's over-leveled cards create a statistical advantage that's nearly impossible to outplay," and the game is permanently branded pay-to-win despite real skill elements. **Vertical account power in PvP poisons competitive integrity even when it's earnable free** — because two equal-skill players no longer play the same game.

Meanwhile the ethics research consensus: cosmetics-only and battle-pass models (League of Legends et al.) sustain massive revenue with intact trust, because purchases never touch power.

### The reconciliation: two lanes

The Solo Leveling fantasy ("I train, my mana grows, I get personally stronger") is real and valuable — it just belongs in a different lane than competition:

**Lane 1 — PvE / Story Mode: FULL Solo Leveling.** In campaign and co-op content, the player character genuinely levels: mana pool grows, new caster abilities unlock, your summoner visibly evolves from Novice to Sovereign. Mushoku-style training loops (deplete your mana to grow it) live here guilt-free. PvE is where the RPG power fantasy runs wild — vs AI, nobody's integrity is harmed.

**Lane 2 — PvP: identical baseline, always.** Every ranked duelist has the same energy pool, regen, life, and caster kit. Full stop. What progression grants in PvP is **expression, options, and prestige — never numbers:**

| Mastery unlock | What it is | Why it's fair |
|---|---|---|
| **Chantless casting** | Cast Word-tier silently (no audio tell) after deep mastery of a school | A *side-grade*: same 1.3× power, trades theater bonus for stealth. Skill-gated, not paid |
| **Sigil styles** | Your Script glyphs render in your personal calligraphy/color | Cosmetic |
| **Summon rituals** | Earned entrance animations for mastered monsters (100 duels with a card = its "awakened" summon) | Cosmetic + emotional bond with specific cards |
| **Titles & rank regalia** | Novice → Adept → Warden → Saint → Sovereign → Origin-touched, visible on your avatar/duel-disk | Prestige |
| **Arena environments** | Unlocked field cosmetics for casual lobbies | Cosmetic |
| **Aspect attunement depth** | Lore content, faction storylines, cosmetic auras as you rep your Aspect | Identity/belonging |

The key insight: **mastery should deepen your *bond* with what you own, not raise its stats.** The 100-duels-awakened-summon mechanic makes your specific, physical Emberdrake card feel like *your* Emberdrake — Solo Leveling's emotional core (growth you earned) without its integrity cost.

### Where money can and cannot touch

- **Can:** physical cards/packs (collection = options, the accepted TCG economy), cosmetics, battle pass (cosmetic + PvE content), story chapters.
- **Cannot, ever:** energy/life/regen, card stat boosts, mastery levels, chantless casting, ranked rewards. Nothing purchasable may move a number in a PvP duel.
- **Collection ≠ vertical power** is enforced by the balance doc's budgets: legendaries are *unique* (one rule-break), not *strictly better* — a common-heavy starter deck must sit within striking distance of a whale's deck on rate, differing in flexibility, not raw stats. Telemetry band: starter-vs-full-collection matchup must stay ≤ 60/40.

### Matchmaking note
Because collection breadth still confers option-advantage, casual queue matches by collection-power bracket + rank; ranked matches by rating only (with the ≤60/40 guardrail making this safe). New-player queue uses preconstructed-deck-only lobbies — pure skill, day one.

---

## Sources
- Deck sizes & copy limits: [MTG deck rules (Draftsim)](https://draftsim.com/mtg-deck-size/) · [Yu-Gi-Oh deckbuilding guide](https://outof.games/realms/yugioh/guides/145-everything-you-need-to-know-about-deckbuilding-rules-in-yu-gi-oh/) · [Hearthstone deck design discussion](https://hearthstone.wiki.gg/wiki/Deck)
- Banlist philosophies: [Yu-Gi-Oh Forbidden/Limited system](https://www.sportskeeda.com/esports/yu-gi-oh-master-duel-banlist-forbidden-limited-semi-limited-cards) · [nerf-vs-ban philosophy discussion](https://fftcgcrystarium.com/musings/stop/)
- Progression ethics: [Clash Royale P2W analysis](https://www.zleague.gg/theportal/clash-royale-paywin/) · [ethical monetization design](https://www.daydreamsoft.com/blog/ethical-monetization-system-design-earning-revenue-without-losing-player-trust) · [hybrid monetization complexities](https://www.globalgamesforum.com/features/navigating-the-complexities-and-controversies-of-hybrid-monetization-in-mobile-gaming)
