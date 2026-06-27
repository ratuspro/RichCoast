# Technical Spec — Architecture & Parallel Workstreams

Companion to [SPEC.md](SPEC.md). The gameplay design lives there; this document defines how
the code is structured so two people can build it in parallel without colliding.

**Dev 1 owns Zone A + Zone C + the shared shell/HUD; Dev 2 owns Zone B (including
scoring).** All coupling between the two halves flows through a single agreed contract
module — nothing else is shared logic.

## Ownership

| Area | Owner |
|------|-------|
| Zone A (merge), Zone C (trap-door), HUD, scene bootstrap | **Dev 1** |
| Zone B (split arena, gates, funnel, scoring) | **Dev 2** |
| `core/` seam (`contracts.ts`, `EventBus.ts`, `Layout.ts`) | **Shared** — edits require both devs to agree |

## Runtime model

A single Phaser scene. Each zone is a self-contained **system module** behind a uniform
interface and communicates only via a typed **event bus** — never by calling into another
zone's code. The scene file just instantiates the systems and wires the bus, so it stays
thin and rarely changes.

```
src/
  main.ts              # Phaser game config bootstrap                       [shared, stable]
  GameScene.ts         # single scene: instantiates systems, wires the bus  [shared, thin/stable]
  core/
    contracts.ts       # THE seam: event names, payloads, GameSystem iface  [shared — changes need both devs]
    EventBus.ts        # typed event emitter                                [shared, stable]
    Layout.ts          # screen dims + zone rects + Zone B entry point      [shared, stable]
    HUD.ts             # renders score from SCORE_CHANGED                   [Dev 1]
  zoneA/  ZoneASystem.ts, BallQueue.ts, MergeLogic.ts                       [Dev 1]
  zoneC/  ZoneCSystem.ts   (trap-door + cooldown lock)                      [Dev 1]
  zoneB/  ZoneBSystem.ts, GateSystem.ts, Funnel.ts                          [Dev 2]
  dev/    stubZoneB.ts, stubZoneAC.ts, harness.ts                           [isolation stubs]
```

Every system implements `GameSystem { create(scene); update(time, delta) }` so
`GameScene` wires them all identically.

## Interface contract (the canonical cross-team API)

This is the **only** coupling between Dev 1 and Dev 2. It lives in `core/contracts.ts`;
edits to it require both devs to agree.

**Zone C → Zone B**
- `BALL_DROPPED { value: number; tier: number; x: number }` — trap-door fired; Zone B
  spawns one ball with this value at horizontal entry `x`.

**Zone B → Zone C** (drives the trap-door cooldown; Zone C owns its own lock state)
- `ZONE_B_BUSY {}` — at least one ball in flight; Zone C locks the trap-door.
- `ZONE_B_EMPTY {}` — no balls in flight; Zone C may re-arm.

**Zone B → HUD**
- `SCORE_CHANGED { total: number }` — current running total.

**Shared layout** (`core/Layout.ts`): portrait `390×844`, the three zone rects, and Zone
B's entry point. Both devs read it; it changes rarely.

## Working independently

A `?zone=` URL flag (handled in `GameScene` / `main.ts`) swaps real systems for stubs so
each dev runs a playable slice without touching the other's files:

- **`?zone=ac`** — real Zone A + C plus a **stub Zone B** (`dev/stubZoneB.ts`): on
  `BALL_DROPPED` it emits `ZONE_B_BUSY`, then after a delay `ZONE_B_EMPTY` and a fake
  `SCORE_CHANGED`. Dev 1 builds merge + trap-door with no dependence on real B physics.
- **`?zone=b`** — real Zone B plus a debug **harness** (`dev/harness.ts`) with a
  button/keypress that fires `BALL_DROPPED { value }` and logs the responses. Dev 2 builds
  the arena with no dependence on A or C.
- **`?zone=full`** (default) — all real systems wired together.

## Open questions

- `BALL_DROPPED.x` semantics — exact tunnel-exit position vs. the nearest-ball x — must be
  fixed in `contracts.ts` before either dev starts, since both halves depend on it.
