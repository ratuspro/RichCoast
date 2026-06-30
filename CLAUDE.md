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
  {count,nextMilestone}` (B→HUD), `BUFFER_EXHAUSTED` (B→scene, triggers Zone B game-over),
  `ARENA_ZOOM {active}` (A→C, locks the trap-door while Zone A's milestone zoom-out animates).

## Ownership boundaries — don't cross them casually

- **Dev 1:** Zone A (merge), Zone C (trap-door), HUD, scene bootstrap (`src/zoneA/`,
  `src/zoneC/`, `src/core/HUD.ts`, `main.ts`, `GameScene.ts`).
- **Dev 2:** Zone B (split arena, gates, funnel, scoring) — `src/zoneB/`.
- **Shared:** `src/core/` (`contracts.ts`, `EventBus.ts`, `Layout.ts`, `BallColors.ts`) —
  changes need both. `BallColors.ts` is the single tier palette both zones draw from.

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
+ buffer count), the thin `GameScene` + `?zone=` routing, the Matter world bottom wall (Zone A's
own boundary + funnel floor now live in `ArenaView`), and the isolation layer (`src/dev/` stubs
+ harness) all work. A
debug overlay (`src/core/DebugMode.ts` + `src/dev/DebugHarness.ts`, toggled by `?debug=2` or
the **D** key) adds a DROP button (also SPACE) that fires `BALL_DROPPED` straight onto the
bus plus a live event log. Tooling is Phaser 4 + Vite + TypeScript (strict) + Vitest; pure
logic is unit-tested (`npm run test`) — the seam, Zone A's `ballMath`/`MergeLogic`, and Zone
B's `BallBuffer`.

**Zone A** (`src/zoneA/`) plays: drag along the top to aim, release to drop; balls are
procedurally-textured Matter circles (colour + value) that grow heavier and grippier by
tier, same-**value** collisions merge into the next tier with a neighbour-shoving blast.
Merging is **uncapped** — tiers climb forever — and each merge **triples** the value
(`tierToValue` is now `3^(tier-1)`, since merging two equal balls yields `1.5×(V+V)=3V`).
Ball radius reads the `RADII` table for tiers 1–10 and keeps growing geometrically past it
(`RADIUS_GROWTH` in `tuning.ts`); colours come from the shared `src/core/BallColors.ts`
"Jewel Tones" palette and **cycle** (modulo) past tier 10 (one source for both zones, so a
transferred ball keeps its colour). Because balls grow without bound, the **arena expands at
recurring milestones**: every 50 levels Zone A input freezes, `ArenaView` grows the playfield
(scale `s ×= GROW=1.18`, so `s` ramps **geometrically** 1,1.18,1.39,…: ceiling rises, walls move
out, funnel widens — always *outward*, never into Zone B) and a **dedicated Zone-A camera** zooms
out (`zoom = 1/s`) so balls keep their real size yet appear ~1/GROW smaller each milestone with
relative positions intact. Each
milestone also **shifts the draw window up by 4 tiers** (hand-authored in `progression.json`:
`[1,4]→[5,8]→[9,12]→…`), so the lowest 4 tiers are **blacklisted** from future spawns — the
in-hand and Next pieces are **re-rolled** off any blacklisted tier (`BallQueue.reroll` +
`AimController.refreshQueue`), and any now-obsolete balls still on the board are **drained
together into Zone B** in one synchronized slide
(`Board.takeBallsBelow` + `ZoneASystem.drainBlacklisted`, emitting `BALL_DROPPED` per ball when
its slide lands — mirroring Zone C's up-front `ZONE_B_BUSY` so the emptying board can't read as
a stalemate). Input + Zone C restore only once the ~1.2s zoom **and** the drain finish. The
dedicated camera renders a Phaser **layer** of all Zone A gameplay; the
main camera ignores that layer and the arena camera ignores the screen-space HUD/queue row, so
the two never double-draw and the HUD stays full-size on top. Spawn row + death line scale with
`s`. Zone C is locked during the tween via the `ARENA_ZOOM` event. A top-right **queue row** in
`AimController` pairs the next-ball preview with the balls-left-to-drop count on one styled
line; aiming maps the pointer through the arena camera (`getWorldPoint`) so it stays correct
when zoomed. Zone A owns the run's game-over: both conditions —
a ball resting above the death line for ~1s (overflow) and the stalemate (buffer 0 + Zone A
empty + Zone B empty) — converge on one handler that pauses the whole Matter world and draws
a single **full-screen** overlay showing `GAME OVER`, the final score (mirrored from the
existing `SCORE_CHANGED` event), and a **RESTART** button that calls `scene.restart()` (which
resets the arena scale/camera/walls to `s=1`). A red `DeathLine` warning stays hidden until a
ball rests within `WARN_BAND` of the (scaled) death line (`Board` reports the danger
transition; `isNearDeath` in `ballMath.ts` is the pure predicate). Game-over needs no contract
event. Every dropped ball stamps `body.ballData` so Zone C can find it. The zone splits into
`tuning.ts`, `ballMath.ts`, `BallFactory.ts`, `AimController.ts`, `Board.ts`, `DeathLine.ts`,
`ArenaView.ts`, plus the existing `BallQueue`/`MergeLogic`. The boundary walls + funnel floor
now live in `ArenaView` (movable), not `GameScene` (which keeps only the off-screen bottom
wall). `progression.json` is now **milestone-structured**: `ballWindow` holds at `[1,4]` through
level 49, then jumps `[5,8]`/`[9,12]`/`[13,16]`/`[17,20]` at each 50-level milestone, and the
`scoreBarTarget`s are rescaled to track the (powers-of-three, now much larger) per-window value
magnitudes — both are starting numbers to tune by playtest.

**Zone B** (`src/zoneB/`) is fully implemented: balls spawn on `BALL_DROPPED`, three gate
types (static, translating, rotating) split balls into copies via a pending-queue pattern,
collectors (sensor areas, any position) drain balls and score their value × collector
multiplier, and walls guide trajectories.
The playfield is now one of **two layouts** (`LAYOUT_1`, `LAYOUT_2` in `zoneLayout.ts`),
chosen at random per run via `pickRandomLayout()` in `ZoneBSystem`'s constructor (so each
boot/`scene.restart()` re-rolls). Both are static "shelf cascade" layouts modelled on the
reference art: stacked horizontal multiplier gates split by vertical/diagonal guide rails,
funnelling into one bottom collector. Gate bars are tier-coloured (green for multiplier ≥4,
gold below) with a bold white `X#` label; multipliers are tuned (≤4) so cascades stay
balanced. Gate visuals live in `GateSystem.buildBody()`. A `BallBuffer` tracks a finite
supply that
refills when Zone B score crosses escalating milestones — exhausting the buffer while Zone B
is empty triggers a local game-over overlay. The `BUFFER_CHANGED` / `BUFFER_EXHAUSTED` events
feed Zone A's queue-row balls-left count (the HUD itself is now score-only). Balls are small
(10px radius) and **collide with each other** (the `CAT_BALL` mask includes itself), so they
pile and nudge in the cascade. Ball textures use
the same shared `src/core/BallColors.ts` palette as Zone A. The zone splits into `ZoneBSystem` plus
`GateSystem`, `CollectorSystem`, `WallSystem`, `ZoneBBall`, `BallBuffer`, and `zoneLayout.ts`;
the old `Funnel.ts` skeleton is **superseded by `CollectorSystem` and is now dead code**.

**Zone C** (`src/zoneC/ZoneCSystem.ts`) plays: the trap-door lock is driven by Zone B's
busy/empty events. While armed, the door band shows **nine evenly-spaced position markers**
(dim dots, inset ~one ball radius from each Zone B edge); the lit one **steps** edge→edge and
back in a ping-pong (driven each frame in `update()` off the `locked` flag — `STEP_MS` per
position, tuned so one full pass equals the old `SWEEP_MS` leg, keeping the original cadence;
reading the live state per frame means the sweep always reappears the instant Zone B clears).
A tap **freezes on the lit position** — its column is the Zone B entry — and picks the Zone-A
ball nearest the door by **edge distance** (Euclidean centre-to-mouth minus the body's
`circleRadius`, so a bigger ball whose edge reaches nearer wins). It locks the door + emits
`ZONE_B_BUSY` up front (so Zone A's stalemate check can't misfire while the ball is
mid-transit), removes the ball from Zone A by destroying its Matter.Image (the Board
self-prunes off the DESTROY event), then runs a cosmetic **suck → pop** on a throwaway
snapshot sprite (slide to the frozen column at the door, then a quick pop up to Zone B ball
size). The snapshot starts at the ball's **apparent on-screen** position + size — `toApparent`
maps Zone-A world coords through the `'arena'` camera (`worldView` + `zoom`), so after a
milestone zoom-out the suck begins at the ball's shrunken on-screen size instead of the
texture's full native size (and the arena camera is told to ignore the snapshot so it isn't
double-drawn). Only when the pop lands does it emit `BALL_DROPPED` at the frozen `x`, so Zone B
spawns a fresh fixed-radius (10px) ball of the same tier **exactly under the lit position**
— deferring the emit avoids double-ball flicker, and the source ball's Zone-A size never
carries over. The markers hide on lock and resume on `ZONE_B_EMPTY`. Frozen decisions:
Matter.js for both zones; **`BALL_DROPPED.x` is now the player-chosen column** (one of nine
discrete positions, no longer a fixed `Layout.zoneBEntry.x`) — Zone B already spawned at
`ball.x`, so this needed no Zone B change, only honest comments on the seam.

**Audio** (`src/core/Sfx.ts`) plays: a procedural Web Audio engine (soft synth bells/marimba,
no asset files) initialised once in `GameScene` from Phaser's own AudioContext (so the mobile
autoplay-unlock is handled), self-silencing until init and a no-op under HTML5/NoAudio. It's a
shared singleton each zone calls at its own local hook — drop/merge (Zone A), transition (Zone
C), multiply/collect/goal (Zone B) — so the frozen contract stays untouched. Merge and Zone B
multiply pitch-climb through a fast chain via the pure, unit-tested `comboPitch.ts` (<0.5s
window, separate channels). **M** toggles mute. Volumes are tuned by relevance (goal loudest,
collect quietest).

> **Keep this section current.** As important phases finish, **rewrite** this paragraph to
> describe the project's state *now* — don't append a changelog or history. It should always
> read as a single snapshot of where things stand, so a fresh session knows what exists
> without inferring it from the code.
