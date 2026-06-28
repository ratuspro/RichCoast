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
  (B→C, drives the trap-door lock), `SCORE_CHANGED {total}` (B→HUD).

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

**Zone A is built; Zones B and C are still skeletons.** The shared shell is complete and
runnable: the seam (`src/core/contracts.ts`, `EventBus.ts`, `Layout.ts`), the HUD, the thin
`GameScene` + `?zone=` routing, the Matter world bounds (now including the solid Zone A
floor), and the isolation layer (`src/dev/` stubs + harness) all work. Tooling is Phaser 4 +
Vite + TypeScript (strict) + Vitest; pure logic is unit-tested (`npm run test`) — the seam
plus Zone A's `ballMath` and `MergeLogic`.

**Zone A** (`src/zoneA/`) plays: drag along the top to aim, release to drop; balls are
procedurally-textured Matter circles (colour + value) that grow heavier and grippier by
tier, same-tier collisions merge into the next tier with a neighbour-shoving blast, a
next-ball preview shows what's coming, and a ball resting above the death line for ~1s ends
the run with a local overlay (game-over stays inside Zone A — no contract event). Every
dropped ball stamps `body.ballData` so Zone C can find it. The zone splits into `tuning.ts`,
`ballMath.ts`, `BallFactory.ts`, `AimController.ts`, `Board.ts`, plus the existing
`BallQueue`/`MergeLogic`.

**Zone C** is plumbed but unfinished: the trap-door lock + nearest-ball world-query +
`BALL_DROPPED` emit are live, but the suck animation and *removing the consumed ball from
Zone A* are still `TODO(zoneC)` (so in `?zone=ac` a sucked ball currently stays on the
board). **Zone B** (`src/zoneB/`) is still a skeleton: busy/empty/score bookkeeping is ready
to call, but gate splitting, funnel draining and scoring are unwritten — Dev 2's half. Frozen
decisions: Matter.js for both zones; `BALL_DROPPED.x` is always the fixed `Layout.zoneBEntry.x`.

> **Keep this section current.** As important phases finish, **rewrite** this paragraph to
> describe the project's state *now* — don't append a changelog or history. It should always
> read as a single snapshot of where things stand, so a fresh session knows what exists
> without inferring it from the code.
