# Conduit

The headless, deterministic rules engine for the (untitled) mixed-reality trading-card battle game — plus its content pipeline, simulation harness, and design corpus. This repo is milestones **M0–M1** of the [master roadmap](docs/design/xr-card-battle-master-roadmap.md).

**Start here:** [docs/design/00-INDEX.md](docs/design/00-INDEX.md) (design authority) and the [M0–M1 technical spec](docs/design/xr-card-battle-m0-m1-technical-spec.md) (what this code implements).

## Layout

| Path | What |
|---|---|
| `src/Conduit.Engine` | The rules kernel. Pure C#, netstandard2.1, **zero dependencies** (architecture rule R1). Consumes typed Intents, emits GameEvents, bit-identical replays. |
| `src/Conduit.Content` | Card/config JSON loading + validation (stat budgets, deck legality). |
| `src/Conduit.SimHarness` | `conduit-sim`: AI-vs-AI duels, matchup sweeps, content checks. Permanent CI infrastructure. |
| `tests/` | Unit, invariant, and determinism tests. `tests/golden/` holds the golden replay corpus (grows with every fixed bug). |
| `content/` | The M0 proto card set (~30 cards) and `balance.v0.json` (every tunable constant — the engine hardcodes none). |
| `unity/ConduitDesktop` | **M2** — the Unity 6 desktop front-end (architecture rule R4: a dumb subscriber over the engine). Consumes `Conduit.Engine.dll` (vendored in `Assets/Plugins`; run `unity/sync-engine.sh` to refresh). Verified: the engine runs bit-identical under Unity's runtime and the .NET harness (same seed → same state hash). |
| `docs/design/` | The design corpus. `00-INDEX.md` wins all disputes. |

## Quick start

```bash
dotnet test                                            # includes determinism gates
dotnet run --project src/Conduit.SimHarness -- check   # content validation
dotnet run --project src/Conduit.SimHarness -- run --duels 500 --matchup aggro:wall
```

## Non-negotiables (spec §1)

Fixed 20 Hz tick · integer-only math (milli-energy, basis points, Bresenham accumulators) · engine-owned PCG32 · no ambient environment in the engine · deterministic iteration · simultaneous combat resolution · determinism verified in CI (same-seed, cross-OS, golden replays).

## Combat model

Combat follows [combat-model.md](docs/design/xr-card-battle-combat-model.md) v0.2 (commander + intelligent creatures), implemented per [combat-spec.md](docs/design/xr-card-battle-combat-spec.md): continuous fixed-point positions (milli-units) as sim truth, a spatial-hash grid for queries, creatures carrying **ability kits** driven by a deterministic **AI priority function** (range bands / threat density / facing), a **commit → wind-up (telegraph) → resolve → recover** pipeline, and **Recall → reserve → redeploy** as the save-before-death loop. Mutual damage is emergent (two agents attacking), not a rule.

## This is engine, not content

The card set in `content/cards/_fixtures.json` is **generic engine test fixtures** (neutral placeholder creatures/numbers) used only to exercise the engine, AI, and sims. Real creatures, abilities, elements, and balance numbers are designed later on top of this engine. `HpScale = 1` in v0 (a documented balance hook); fixtures author HP directly.

## v0 known deviations / TODOs

- Replay is in-memory + `Verify()`; the `.cvr` binary file format is specified (spec §8) but not yet serialized to disk.
- No positional bodyblocking yet: a creature ordered to attack the summoner paths to it directly; how defense *gates* a push (overflow / lane-blocking) is deferred game design. The engine supports directive-driven targeting generically.
- `ModifyStats`/`Hex` attach a status but don't yet modify a creature's ability output (status rework).
- Torrent stores no killer attribution yet (consumed only by M8 Reactions — safe to defer).
- Balance: fixture numbers are placeholders; duels are decisive but untuned. Mirror shows a mild EntityId-tie fairness bias to calibrate later.
- Deck legality runs in `Proto` mode; `Strict` mode is implemented and tested against the canonical rules.
