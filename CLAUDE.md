# CLAUDE.md

A 2D portrait-mode mobile-browser arcade game. Two gameplay zones stacked vertically,
coupled through one contract module so two devs can build in parallel.

## What this is, in one screen

- **Zone A (top)** — fruit-merge puzzle. Drop balls; equal-value balls merge into the next
  power-of-2 tier. Overflowing the top boundary = game over.
- **Zone C (boundary)** — manually-tapped trap-door. Sucks the nearest ball from A into B,
  one at a time. Locked until Zone B is empty.
- **Zone B (bottom)** — no-control physics arena. Gates split a ball into N copies of the
  same value; copies cascade and drain into a funnel. Score = sum of all drained values.
- Endless run, no levels. Merging scores nothing; value is realised only when balls exit B.

## Source of truth — read on demand, not preemptively

Two specs hold the detail. Don't read them wholesale for every task — open the relevant
section when the work touches it:

- **`SPEC.md`** — gameplay design: zone mechanics, scoring formula, visual theme, open
  questions. Read when changing **what the game does** or how a mechanic feels.
- **`TECH_SPEC.md`** — architecture: module layout, ownership, the event-bus contract, and
  the `?zone=` isolation workflow. Read when changing **how the code is structured** or
  touching cross-zone communication.

## Architecture you must respect (from TECH_SPEC.md)

- Single Phaser scene. Each zone is a **`GameSystem { create(scene); update(time, delta) }`**
  module. Zones **never call into each other** — they talk only through a typed event bus.
- **`src/core/contracts.ts` is the seam.** Event names, payloads, and the `GameSystem`
  interface live there. Changing it is a both-devs-must-agree action — treat it as frozen
  unless the task is explicitly about the contract.
- Cross-zone events: `BALL_DROPPED {value,tier,x}` (C→B), `ZONE_B_BUSY`/`ZONE_B_EMPTY`
  (B→C, drives the trap-door lock), `SCORE_CHANGED {total}` (B→HUD), `BUFFER_CHANGED
  {count,nextMilestone}` (B→HUD), `BUFFER_EXHAUSTED` (B→scene, triggers Zone B game-over).

## Ownership boundaries — don't cross them casually

- **Dev 1:** Zone A (merge), Zone C (trap-door), HUD, scene bootstrap (`src/zoneA/`,
  `src/zoneC/`, `src/core/HUD.ts`, `main.ts`, `GameScene.ts`).
- **Dev 2:** Zone B (split arena, gates, funnel, scoring) — `src/zoneB/`.
- **Shared:** `src/core/` (`contracts.ts`, `EventBus.ts`, `Layout.ts`) — changes need both.

When a task lands in one owner's area, stay within those files; reach the other half only
through the contract events above.

## Tech stack & constraints

- Phaser 4 + TypeScript + Vite. Mobile browser, full-screen portrait **390×844**.
- No external asset dependencies — generate textures procedurally.
- `?zone=ac` / `?zone=b` / `?zone=full` (default) swap real systems for stubs so each half
  runs standalone. See TECH_SPEC.md "Working independently" before editing the stubs/harness.

## Running it

- `npm install` once, then `npm run dev` — Vite serves at **http://localhost:5173/**. Append
  `?zone=ac`, `?zone=b`, or `?zone=full` to isolate a half.
- `npm run test` (Vitest, pure logic), `npm run typecheck` (tsc strict), `npm run build`
  (typecheck + production bundle), `npm run preview` (serve the build).
- To verify a render headlessly: screenshot with Chrome —
  `chrome --headless=new --window-size=390,844 --virtual-time-budget=4000 --screenshot=<path> http://localhost:5173/`.
  Point `--screenshot` at a writable dir (a user temp path, not the sandbox scratchpad).

## Status

**All three zones play.** The shared shell is complete
and runnable: the seam (`src/core/contracts.ts`, `EventBus.ts`, `Layout.ts`), the HUD (score
+ buffer count), the thin `GameScene` + `?zone=` routing, the Matter world bounds (including
the solid Zone A floor), and the isolation layer (`src/dev/` stubs + harness) all work. A
debug overlay (`src/core/DebugMode.ts` + `src/dev/DebugHarness.ts`, toggled by `?debug=2` or
the **D** key) adds a DROP button (also SPACE) that fires `BALL_DROPPED` straight onto the
bus plus a live event log. Tooling is Phaser 4 + Vite + TypeScript (strict) + Vitest; pure
logic is unit-tested (`npm run test`) — the seam, Zone A's `ballMath`/`MergeLogic`, and Zone
B's `BallBuffer`.

**Zone A** (`src/zoneA/`) plays: drag along the top to aim, release to drop; balls are
procedurally-textured Matter circles (colour + value) that grow heavier and grippier by
tier, same-tier collisions merge into the next tier with a neighbour-shoving blast, a
next-ball preview shows what's coming, and a ball resting above the death line for ~1s ends
the run with a local overlay (game-over stays inside Zone A — no contract event). Every
dropped ball stamps `body.ballData` so Zone C can find it. The zone splits into `tuning.ts`,
`ballMath.ts`, `BallFactory.ts`, `AimController.ts`, `Board.ts`, plus the existing
`BallQueue`/`MergeLogic`.

**Zone B** (`src/zoneB/`) is fully implemented: balls spawn on `BALL_DROPPED`, three gate
types (static, translating, rotating) split balls into copies via a pending-queue pattern,
collectors (sensor areas, any position) drain balls and score their value × collector
multiplier, walls guide trajectories, and a `BallBuffer` tracks a finite ball supply that
refills when Zone B score crosses escalating milestones — exhausting the buffer while Zone B
is empty triggers a local game-over overlay. The `BUFFER_CHANGED` / `BUFFER_EXHAUSTED` events
are wired to the HUD (buffer count, top-right). The zone splits into `ZoneBSystem` plus
`GateSystem`, `CollectorSystem`, `WallSystem`, `ZoneBBall`, `BallBuffer`, and `zoneLayout.ts`;
the old `Funnel.ts` skeleton is **superseded by `CollectorSystem` and is now dead code**.

**Zone C** (`src/zoneC/ZoneCSystem.ts`) plays: the trap-door lock is driven by Zone B's
busy/empty events; a tap picks the Zone-A ball nearest the door by **edge distance**
(Euclidean centre-to-mouth minus the body's `circleRadius`, so a bigger ball whose edge
reaches nearer wins), removes it from Zone A by destroying its Matter.Image (the Board
self-prunes off the DESTROY event), plays a ~150ms cosmetic "suck" tween of a throwaway
snapshot sprite into the door, then emits `BALL_DROPPED` so Zone B spawns a fresh
fixed-radius (14px) ball of the same tier — the source ball's Zone-A size never carries over.
The tap locks the door immediately to block a double-suck during the tween. Frozen
decisions: Matter.js for both zones; `BALL_DROPPED.x` is always the fixed
`Layout.zoneBEntry.x`.

> **Keep this section current.** As important phases finish, **rewrite** this paragraph to
> describe the project's state *now* — don't append a changelog or history. It should always
> read as a single snapshot of where things stand, so a fresh session knows what exists
> without inferring it from the code.
