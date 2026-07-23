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
| `docs/design/` | The 14-document design corpus. `00-INDEX.md` wins all disputes. |

## Quick start

```bash
dotnet test                                            # includes determinism gates
dotnet run --project src/Conduit.SimHarness -- check   # content validation
dotnet run --project src/Conduit.SimHarness -- run --duels 500 --matchup aggro:wall
```

## Non-negotiables (spec §1)

Fixed 20 Hz tick · integer-only math (milli-energy, basis points, Bresenham accumulators) · engine-owned PCG32 · no ambient environment in the engine · deterministic iteration · simultaneous combat resolution · determinism verified in CI (same-seed, cross-OS, golden replays).

## v0 known deviations / TODOs

- Replay is in-memory + `Verify()`; the `.cvr` binary file format is specified (spec §8) but not yet serialized to disk.
- `ModifyStats`/`Hex` status expiry does not yet revert the stat delta (noted in code; lands with the status rework).
- Torrent stores no killer attribution yet (consumed only by M8 Reactions — safe to defer).
- Spatial model is minimal: zones exist and Defend/blocking works, but auto-acquire ignores zone adjacency.
- Card files are one-per-Aspect arrays (not one-per-card) for authoring convenience; the loader accepts both.
- Deck legality runs in `Proto` mode (copies unrestricted) until the pool exceeds ~10 distinct cards per Aspect; `Strict` mode is implemented and tested against the canonical rules.
