# Balance Mathematics — The Aspect System v0.1

*The quantitative framework for balancing six asymmetric Aspects in a real-time XR card battler: cost curves, stat budgets, Aspect pricing, matchup targets, reaction/Oath math, exception rules, and what our simulations already proved. All numbers are v0.1 provisional — the framework is the deliverable; playtesting calibrates the constants.*

---

## 1. The currencies (what everything is priced in)

Every mechanic must be expressible in these five currencies, or it can't be balanced:

| Currency | Unit | Notes |
|---|---|---|
| **Energy** | points, regenerating | The mana. Regen ~20/min ramping to ~26/min by minute 10 (ramp = late game escalates). Pool cap 10 (forces spending; no infinite hoarding) |
| **Stat points (SP)** | ATK & HP | What energy buys on a monster |
| **Life** | 150 per player | The win condition |
| **Time** | seconds | Real-time game → cast time, summon delay, cooldowns are *real costs*. A 2s ritual on a big summon is a window of vulnerability = a price |
| **Attention** | player focus | XR-specific: gestures, gaze, card handling compete for two hands and one gaze. A cheap effect that demands constant attention is expensive. Never ignore this axis |

## 2. The cost curve (vanilla test, adapted to real-time)

Baseline from Hearthstone's proven formula (stats = 2×cost+1, the "vanilla test") — but our sims proved a critical real-time modification:

> **FINDING (sim): ATK must be priced ~2× per point vs HP.** In symmetric pricing, aggression always dominates: monsters die faster than energy can replace them, boards empty, and every game becomes a 2-minute face-race (our naive sim: aggro won 100% of 500 trials, median duel 1.1 min). Defense must be cheap and offense expensive or the game degenerates.

**v0.1 budget:** a monster costing **C** energy gets **SP = 2C + 2**, spent at exchange rates **1 ATK = 2 SP, 1 HP = 1 SP** (i.e., baseline monster ≈ ATK 0.6C, HP 2C, attacks every 3s). Examples:

| Cost | Typical (balanced) | Aggro build | Wall build |
|---|---|---|---|
| 2 | 1 ATK / 4 HP | 2 ATK / 2 HP | 1 ATK / 5 HP |
| 4 | 2 ATK / 6 HP | 3 ATK / 4 HP | 1 ATK / 8 HP |
| 6 | 3 ATK / 8 HP | 5 ATK / 5 HP | 2 ATK / 11 HP |
| 8 (legendary tier) | 5 ATK / 10 HP | — hand-tuned individually — | |

Non-stat abilities (effects, keywords) are priced by converting to SP-equivalents (a "deal 3 damage" effect ≈ 3 SP ≈ 1.5 energy, then taxed by Aspect — §3).

## 3. The Aspect pricing matrix (the color pie as mathematics)

Each Aspect buys capabilities at different rates. **Identity = the tax table.** A hard "cannot do" is an infinite price; a specialty is a discount. This is how "Ember can't heal" becomes engineering instead of flavor text:

| Capability | Ember | Tide | Bastion | Gale | Verdant | Umbra |
|---|---|---|---|---|---|---|
| Direct damage | **0.8×** | 1.3× | 1.4× | 1.0× | 1.3× | 1.1× |
| Burst/speed (fast cast) | **0.8×** | 1.2× | **∞** | **0.9×** | 1.3× | 1.1× |
| Healing | **∞** | 1.2× | 1.3× | **∞** | **0.7×** | **∞** |
| Shields/walls | **∞** | 1.1× | **0.7×** | **∞** | 1.2× | 1.4× |
| Counter/redirect | 1.4× | **0.7×** | 1.2× | 1.0× | 1.4× | 1.2× |
| Mobility/reach | 1.1× | 1.0× | **∞** | **0.7×** | 1.3× | 1.2× |
| Swarm (token generation) | 1.2× | 1.3× | 1.4× | 1.2× | **0.8×** | 1.0× |
| Debuff/drain | 1.3× | 1.1× | 1.4× | 1.3× | 1.4× | **0.7×** |
| Graveyard recursion | **∞** | 1.4× | **∞** | **∞** | 1.3× | **0.8×** |

Rules of the matrix: every Aspect gets **2–3 discounts (0.7–0.9×)**, **2–3 hard ∞s**, and pays surcharge elsewhere. Row and column sums are kept roughly equal across Aspects (no Aspect's average tax may drift >5% from the others — that's the "everyone's slice of pie is equal-sized, differently shaped" invariant). **Umbra's special cost:** its 0.7× discounts are partly paid in *Life, not energy* (e.g., big drains cost 5–10 life) — the One-Piece-style universal cost made mechanical.

## 4. Matchup targets (the wheel, quantified)

Standards from fighting-game balance practice: overall winrate per Aspect within **48–52%** (pick-weighted); worst allowable single matchup **60/40** (a "6-4" in fighting-game terms); target for designed counters (opposite Aspects on the wheel) **55/45**. Yomi's zero-7-3s standard is the aspiration; 6-4 is the hard ceiling.

| Matchup type | Target | Example |
|---|---|---|
| Mirror | 50/50 | Ember v Ember |
| Allied (adjacent on wheel) | 50/50 ±2 | Ember v Gale |
| Neutral (two steps) | 52/48 max | Ember v Umbra |
| **Opposed (across wheel)** | **55/45 by design** | Ember v Tide, Gale v Bastion, Umbra v Verdant |

The 55/45 counter matchups are *intentional* — they create the "bring the right deck" meta and the wheel's lore ("Tide quenches Ember") — but must stay winnable by the disadvantaged side through skill (that's why 55, not 65).

## 5. Duel-length and pacing math

Targets: **median duel 12–18 min** (Demeo-session comfortable, headset-battery safe), p10 ≥ 6 min (no feel-bad instant losses), p90 ≤ 25 min (no exhausting marathons); **first summon < 20s** (energy start = 3, cheapest monster = 2); a meaningful action available **every ~10s** all game.

> **FINDING (sim): a pure attrition system cannot hit these targets by stat-tuning alone.** Across our full parameter sweep (HP multipliers 1.5–2.5, ATK coefficients 0.5–0.7, life 100–150, regen 18–24/min), median duels stayed 2.5–5 min AND archetype winrates flipped 0%↔90% on tiny coefficient changes — a knife-edge. Conclusion: duel length and archetype fairness must come from **negative-feedback stabilizers**, not from finding magic constants.

**The stabilizer set (v0.1)** — each is a comeback force that also happens to be dramatic:

1. **Surge** — when your board is outnumbered, energy regen +40–60%. (Sim-verified: this single mechanic stretched median duels from ~4 min to ~27 min at full strength — it's the master pacing dial; tune between.)
2. **Danger-scaled defense** — your defensive gestures block 30% baseline, scaling to ~55% at low life (mechanically: low life charges your ward). The player's most desperate moments are their most defensively powerful = anime last-stands, mathematically real.
3. **Unblocked-overflow rule** — each defending monster blocks one attacker; only *excess* attackers hit the summoner. Board advantage = pressure (clean, readable), but a single monster still walls three (defense is never useless).
4. **Escalation ramp** — energy regen +0.6/min over the first 10 min: stalemates break themselves; late game is always bigger than early game.

> **FINDING (sim): resolution order is a giant hidden advantage.** Sequential attack resolution gave "player 1" a 100% mirror-match winrate until we made resolution simultaneous. Implications: (a) netcode must resolve combat simultaneously per tick, never favoring who's "first"; (b) any mechanic that grants first-action (initiative, reaction order) is worth several percent winrate and must be priced as a premium capability.

## 6. Reactions math (advanced layer)

Genshin-style order-dependence, bounded for balance: a triggered reaction deals **bonus = 1.5× the *initiating* effect's SP value** if you applied the catalyst second (the skill position), **1.0×** if first. Caps: a reaction may never exceed **25% of a player's max life** in one burst (no one-combo kills), and re-triggering the same reaction within 10s yields diminishing returns (0.5×, then 0.25×) — prevents loops. Reactions consume the elemental "charge" on the target (one reaction per application — no double-dipping).

## 7. Oath math (advanced layer)

An Oath's power bonus is priced by **expected cost of the restriction**, with the EV deliberately set slightly *negative* at average play and *positive* at expert play — Oaths are skill-expressive gambles, not free stats:

`bonus_SP ≈ 0.8 × E[SP-value forfeited by the restriction] ` (the 0.8 makes random Oath-taking a ~2–3% winrate loss)

Plus a **punishment clause** on breaking it (immediate loss of the bonus + life penalty ≈ half the bonus's SP value). Examples: *"No more than 3 summons this duel"* forfeits ~2 summons ≈ 12 SP → grants ~+10 SP spread over your three; *"I shall not ward myself"* forfeits ~30% face mitigation → grants ~+25% monster ATK. Every Oath is public and visible to the opponent (a glowing brand) — information is part of the price.

## 8. Exceptions, specials, and hiddens (the spice — with guardrails)

The math above makes the game *fair*; controlled violations make it *interesting*. Rules for breaking rules:

- **Pie-breakers:** a card may do something its Aspect is taxed ∞ for — at **2× the normal energy price, minimum rarity Rare, max 5% of any set**, and never at competitive efficiency (MTG's proven pattern: off-color effects exist but are always overpriced). An Ember heal can exist as a legendary curiosity ("The Phoenix's Tear") — it must never be *good enough* to erase Ember's identity. The lore writes itself: these cards are famous *because* they violate cosmology.
- **Legendaries: one rule-break each.** Every legendary gets exactly one unique mechanic that ordinary cards can't have (ignores the board cap, survives one death, casts during the opponent's reaction window). Budgeted at its SP-equivalent + a 20% "uniqueness premium." One break per card keeps them analyzable rather than chaotic.
- **Hidden mechanics (discoverables):** undocumented interactions seeded deliberately — specific creature pairs with synergy dialogue and a stat bonus ("soul-linked"), reactions that behave differently on specific arena environments, an Umbra card whose effect changes at exactly 13 life. Budget: costed as if the hidden effect were visible (no free power — just free *mystery*). These are the community's YouTube-essay fuel; ship 10–20 per set, let players find them.
- **Environmental exceptions:** field transformations (the MR→VR moments) may temporarily rewrite one tax cell (a Volcano arena gives *everyone* Ember's 0.8× burst pricing). Bounded: one cell, temporary, symmetric for both players.
- **The invariant that protects everything:** no exception may (a) give an Aspect sustained access to its ∞ capabilities at fair prices, or (b) push any matchup past 60/40. Exceptions are *seasoning* — the moment a pie-breaker defines the meta, it gets nerfed by the standard levers.

## 9. The balance operations loop (how numbers stay honest)

1. **Telemetry bands:** auto-flag any Aspect outside 48–52% pick-weighted winrate, any matchup past 60/40, any card with >65% inclusion in winning decks of its Aspect (the "auto-include" smell), median duel length outside 10–20 min.
2. **Nerf levers, in order:** energy cost (±1 is a big, clean hammer) → stat line → mechanic text (last resort — changing what a card *does* breaks physical-card truth). Because physical cards can't be reprinted overnight, **costs and stats must live server-side** — the printed card shows the *identity*; the live values come from the server. (This is the digital-hybrid superpower: we can balance-patch physical cards.)
3. **The simulation harness** (built during this session — three iterations of an autobattler duel sim + parameter sweep) becomes CI: every proposed card/constant change runs 10k simulated duels across archetype matrices overnight; regressions on the bands above block the change before humans ever playtest it. Sims catch degeneracy; humans confirm fun.

## 10. Summary of simulation findings (what we proved today)

| # | Finding | Design consequence |
|---|---|---|
| 1 | Symmetric ATK/HP pricing → aggro wins 100%, 1-min duels | ATK priced 2× per point vs HP |
| 2 | Stat-tuning is a knife-edge (0%↔90% archetype flips on small coefficients) | Balance via negative-feedback stabilizers, not magic constants |
| 3 | Surge (outnumbered = faster energy) is the master pacing dial (4 min → 27 min at full strength) | Ship it tunable; calibrate to 12–18 min in playtests |
| 4 | Sequential resolution = 100% first-mover advantage | Simultaneous netcode resolution; initiative effects priced premium |
| 5 | All-or-nothing overflow (face damage only on empty board) creates degenerate races | Per-defender blocking with excess-to-face |

---

*Next steps: (1) extend the sim harness with the Aspect tax matrix to test all 21 matchup pairs against the §4 targets; (2) paper-prototype with these numbers on printed cards — the sims prove non-degeneracy, only humans prove fun.*

## Sources
- [Hearthstone stat budget / vanilla test](https://dotesports.com/hearthstone/news/hearthstone-guide-stat-budget-explanation-29730) · [card value appraisal](https://elie.net/blog/hearthstone/how-to-appraise-hearthstone-card-values)
- [MTG color pie value & enforced weaknesses (Rosewater)](https://magic.wizards.com/en/news/making-magic/value-pie-2003-08-18-0)
- [Sirlin on matchup spreads & Yomi's zero-7-3 standard](https://www.sirlin.net/posts/game-balance-and-fantasy-strike)
- [Evolving the Hearthstone Meta (balance-via-simulation research)](https://arxiv.org/pdf/1907.01623)
