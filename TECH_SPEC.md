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
| Zone B (split arena, gates, collectors, walls, scoring) | **Dev 2** |
| `core/` seam (`contracts.ts`, `EventBus.ts`, `Layout.ts`, `Materials.ts`, `MaterialPainter.ts`, `Theme.ts`) | **Shared** — edits require both devs to agree |

## Runtime model

A single Phaser scene. Each zone is a self-contained **system module** behind a uniform
interface and communicates only via a typed **event bus** — never by calling into another
zone's code. The scene file just instantiates the systems and wires the bus, so it stays
thin and rarely changes.

```
src/
  main.ts                    # Phaser game config bootstrap                       [shared, stable]
  GameScene.ts               # single scene: instantiates systems, wires the bus  [shared, thin/stable]
  core/
    contracts.ts             # THE seam: event names, payloads, GameSystem iface  [shared — changes need both devs]
    EventBus.ts               # typed event emitter                               [shared, stable]
    Layout.ts                 # screen dims + zone rects                          [shared, stable]
    Progression.ts            # reads progression.json, resolves current stage    [shared — Zone A owns the values, HUD reads them]
    progression.json          # authored per-level-milestone tuning data          [shared]
    Materials.ts               # 20-material tier ladder: colours + physics feel   [shared — single source both zones draw from]
    MaterialPainter.ts         # procedural canvas textures for Materials.ts       [shared]
    Theme.ts                   # environment palette (no hex literals in zone code)[shared]
    HUD.ts                     # renders score + buffer/queue row                 [Dev 1]
    Sfx.ts                     # procedural Web Audio engine, shared singleton    [shared, stable — either zone calls it at its own hooks]
    DebugMode.ts               # ?debug=2 / D-key overlay + DROP button           [dev tooling]
    comboPitch.ts               # pure pitch-climb math for merge/multiply chains  [shared, unit-tested]
  zoneA/
    ZoneASystem.ts             # wires the sub-modules below into one GameSystem
    tuning.ts                  # authored constants (radii, friction, growth…)
    ballMath.ts                 # pure math (radius/friction/growth/death-line), unit-tested
    BallFactory.ts               # builds Matter balls from tier/value
    BallQueue.ts                 # next-ball preview + draw-window sampling
    MergeLogic.ts                 # equal-value merge detection, unit-tested
    AimController.ts             # drag-to-aim input + queue-row HUD
    Board.ts                     # per-frame physics bookkeeping, blacklist drain, death-line state
    DeathLine.ts                # red overflow-warning line
    ArenaView.ts                 # movable boundary walls/funnel + milestone zoom camera
  zoneC/
    ZoneCSystem.ts              # trap-door: marker sweep, tap-to-freeze, suck→pop transition
  zoneB/
    ZoneBSystem.ts               # wires the sub-modules below, picks a random layout per run
    zoneLayout.ts                 # LAYOUT_1 / LAYOUT_2 authored layouts + picker
    GateSystem.ts                 # static/translating/rotating gates, split-on-hit
    CollectorSystem.ts             # sensor areas, scoring, SCORE_CHANGED/SCORE_BAR_CHANGED
    WallSystem.ts                  # static physical barriers
    ZoneBBall.ts                    # small (10px) ball wrapper, material-painted
    BallBuffer.ts                    # finite ball supply + refill-on-milestone, unit-tested
    Funnel.ts                        # superseded by CollectorSystem — dead code, kept for reference
  dev/
    stubZoneB.ts, stubZoneAC.ts    # isolation stubs for ?zone=ac / ?zone=b
    harness.ts, DebugHarness.ts     # debug button/keypress + event log
```

Every system implements `GameSystem { create(scene); update(time, delta); destroy?() }` so
`GameScene` wires them all identically.

## Interface contract (the canonical cross-team API)

This is the **only** coupling between Dev 1 and Dev 2. It lives in `core/contracts.ts`;
edits to it require both devs to agree. `TIER_COUNT` (10) is the size of the base
radius/colour table, **not** a gameplay ceiling — merges are uncapped; `tierToValue(tier) =
3^(tier-1)` since a merge of two equal balls yields `1.5×(V+V) = 3V`.

**Zone C → Zone B**
- `BALL_DROPPED { value, tier, x }` — trap-door fired; Zone B spawns one ball with this
  value/tier at horizontal entry `x`. `x` is the column the player froze the trap-door's
  sweeping marker on (one of nine positions along the boundary) — not a fixed point.

**Zone B → Zone C** (drives the trap-door cooldown; Zone C owns its own lock state)
- `ZONE_B_BUSY {}` — at least one ball in flight; Zone C locks the trap-door. Zone C also
  emits this itself the instant a tap is accepted, ahead of the transit animation finishing,
  so Zone A's stalemate check can't misfire mid-transit.
- `ZONE_B_EMPTY {}` — no balls in flight; Zone C may re-arm.

**Zone B → HUD**
- `SCORE_CHANGED { total }` — cumulative running total.
- `SCORE_BAR_CHANGED { filled, target }` — score-bar fill progress.
- `SCORE_BAR_FILLED {}` — bar hit target and reset; Zone A refills its ball buffer.

**Zone A → HUD**
- `BALL_BUFFER_CHANGED { count }` — remaining buffer slots.
- `PROGRESSION_CHANGED { level, minTier, maxTier, bufferCapacity, scoreBarTarget }` —
  internal level advanced; carries the new stage parameters (both Zone A and Zone B read
  from `progression.json` via `Progression.ts`, but Zone A is the one that drives the level
  counter and broadcasts changes).

**Zone A → Zone C**
- `ARENA_ZOOM { active }` — a milestone arena zoom-out is animating; Zone C locks the
  trap-door while `active`, since the boundary geometry is mid-tween.

**Shared layout** (`core/Layout.ts`): portrait `390×844` and the three zone rects. It
changes rarely. There is no single fixed Zone B entry point anymore — see `BALL_DROPPED.x`
above.

## Working independently

A `?zone=` URL flag (handled in `GameScene` / `main.ts`) swaps real systems for stubs so
each dev runs a playable slice without touching the other's files:

- **`?zone=ac`** — real Zone A + C plus a **stub Zone B** (`dev/stubZoneB.ts`): on
  `BALL_DROPPED` it emits `ZONE_B_BUSY`, then after a delay `ZONE_B_EMPTY` and fake score
  events. Dev 1 builds merge + trap-door with no dependence on real B physics.
- **`?zone=b`** — real Zone B plus a debug **harness** (`dev/harness.ts`) with a
  button/keypress that fires `BALL_DROPPED { value, tier, x }` and logs the responses.
  Dev 2 builds the arena with no dependence on A or C.
- **`?zone=full`** (default) — all real systems wired together.

A separate **debug overlay** (`src/core/DebugMode.ts` + `src/dev/DebugHarness.ts`), toggled
by `?debug=2` or the **D** key, adds a DROP button (also SPACE) that fires `BALL_DROPPED`
straight onto the bus plus a live event log — useful in `?zone=full` too, not just isolated
mode.

## Resolved design notes

- `BALL_DROPPED.x` semantics were the one open question at the start of the project: it is
  now the player-chosen trap-door column (one of nine discrete positions), not a fixed
  tunnel-exit point. Zone B already spawned at `ball.x`, so this needed no Zone B code
  change — only accurate comments on the seam.
- Matter.js is the physics engine for both zones (frozen decision).
