# CLAUDE.md

A 2D portrait-mode mobile-browser arcade game. Two gameplay zones stacked vertically,
coupled through one contract module so two devs can build in parallel.

## What this is, in one screen

- **Zone A (top)** — fruit-merge puzzle. Drop balls; equal-value balls merge into the next
  power-of-2 tier. Overflowing the top boundary = game over.
- **Zone C (boundary)** — manually-tapped trap-door. Sucks the nearest ball from A into B,
  one at a time. Armed only in the B phase, and only while Zone B is empty.
- **Zone B (bottom)** — no-control physics arena. Gates split a ball into N copies of the
  same value; copies cascade and drain into a funnel. Score = sum of all drained values.
- **Two mutually exclusive phases**, joined by an input-locked camera pan: in the **A phase**
  only Zone A drop input works (Zone A shown large, Zone B bottom-cropped); once the ball
  buffer is spent and the board settles, the view pans down into the **B phase** where only
  the trap-door works (Zone A top-cropped, Zone B full); filling the score bar pans back up
  and refills the buffer.
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
  (B→C, drives the trap-door lock), `SCORE_CHANGED {total}` (B→HUD, the live per-drain lifetime
  total — feeds Zone A's game-over mirror; the HUD no longer *displays* it), `SCORE_HARVESTED
  {amount,x,y}` (B→HUD, once per cash-in — the round's banked haul + its screen-space launch
  point; THIS drives the HUD number, so the top total freezes through a B round and jumps once
  when the haul flies up and lands), `SCORE_BAR_CHANGED`/
  `SCORE_BAR_FILLED` (B→A, the per-level buffer-refill trigger — fires once per level as the
  bar rolls through a multi-level cash-in), `SCORE_BAR_CASHED_IN` (B→PhaseDirector: the whole
  roll finished — the pan-up trigger, held until the roll plays out in the B framing),
  `BALL_BUFFER_CHANGED {count}` (A→HUD/queue row), `PROGRESSION_CHANGED {level,scoreBarTarget,…}` (A→B),
  `ARENA_ZOOM {active}` (A→C, locks the trap-door while Zone A's milestone zoom-out animates),
  `PHASE_CHANGED {phase}` (PhaseDirector→all, drives the two-phase input locks) and
  `ZONE_A_DEPLETED` (A→PhaseDirector: buffer empty + board settled).

## Ownership boundaries — don't cross them casually

- **Dev 1:** Zone A (merge), Zone C (trap-door), HUD, scene bootstrap (`src/zoneA/`,
  `src/zoneC/`, `src/core/HUD.ts`, `main.ts`, `GameScene.ts`).
- **Dev 2:** Zone B (split arena, gates, funnel, scoring) — `src/zoneB/`.
- **Shared:** `src/core/` (`contracts.ts`, `EventBus.ts`, `Layout.ts`, `Materials.ts`,
  `MaterialPainter.ts`, `Theme.ts`) — changes need both. `Materials.ts` is the single
  tier ladder (name/colours/physics feel) both zones draw from; `MaterialPainter.ts`
  renders it; `Theme.ts` is the environment palette.

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

**All three zones play, fully art-directed, in a two-phase loop.** The shared shell is
complete and runnable: the seam (`src/core/contracts.ts`, `EventBus.ts`, `Layout.ts`), the
HUD (compact score + a numberless milestone progress bar), the thin `GameScene` + `?zone=`
routing, the Matter world bottom wall (at the Zone B world bottom, y=1238; Zone A's own
boundary + funnel floor live in `ArenaView`), and the isolation layer (`src/dev/` stubs +
harness) all work. **The world is taller than the 390×844 screen** (390×1238): Zone A
`{0,0,390,507}` (42px HUD row + a 465px board band; 507 = round(844 × 2/3 × 0.9) — the
old 2/3 split shrunk 10% in Zone B's favour), Zone C `{0,507,390,44}`, Zone B
`{0,551,390,687}` (the gate layouts are still authored for the old 607..1238 band; the
extra 56px is free-fall headroom above the first shelf). `src/core/PhaseDirector.ts` (a
scene-level `GameSystem`, created last in `ac`/`full` modes) owns the **two-phase camera
pan**: one 650ms Sine tween drives the main camera's `scrollY` (0↔394) and the arena
camera's viewport height (465↔71) in lockstep from a rounded proxy — `framingForPan` in
`src/core/phaseGeometry.ts` keeps the arena-bottom/Zone-C seam pixel-locked at every tick,
and the pure FSM lives in `src/core/phaseMachine.ts` (states `A`/`A_TO_B`/`B`/`B_TO_A`,
with a `refillQueued` turnaround for a score bar that fills mid-pan). The director listens
for `ZONE_A_DEPLETED` (pan down) and `SCORE_BAR_CASHED_IN` (pan up — fired once the score
bar's whole roll-through has finished, so the multi-level roll plays out in the B framing
before the camera leaves; `SCORE_BAR_FILLED` fires per-level and no longer drives the pan)
and broadcasts
`PHASE_CHANGED` — the input-lock signal: Zone A's aim is frozen outside the A phase, Zone
C's door is locked outside the B phase (it boots locked), and both stay locked during the
pans. **Both inputs accept the whole screen** — Zone A's drag-to-aim has no y-guard and
Zone C's door tap is a scene-level pointer listener (the door band is visual only) — safe
because the phase locks make them mutually exclusive. So the A phase gives Zone A (HUD +
board) **~60% of the screen** with Zone B bottom-cropped (score bar off-screen), and the
B phase top-crops Zone A to a **113px sliver** while Zone B fills the freed space exactly
— a pure pan, no zoom change. All screen-space chrome (HUD, queue row, game-over
overlay, debug UI) is pinned with `setScrollFactor(0)`. `?zone=b` skips the director and
statically frames the B phase (`cameras.main.setScroll(0, PAN_DISTANCE)`); `?zone=ac` runs
the full phase loop against a stub Zone B that fakes the score bar. A
debug overlay (`src/core/DebugMode.ts` + `src/dev/DebugHarness.ts`, toggled by `?debug=2` or
the **D** key) adds a DROP button (also SPACE) that fires `BALL_DROPPED` straight onto the
bus plus a live event log. Tooling is Phaser 4 + Vite + TypeScript (strict) + Vitest; pure
logic is unit-tested (`npm run test`) — the seam, the phase FSM (`phaseMachine.test.ts`),
the pan geometry (`phaseGeometry.test.ts`), the depletion settle gate
(`settleGate.test.ts`), Zone A's `ballMath`/`MergeLogic`, and the material ladder
(`Materials.test.ts`).

**Visual identity — "Bright Workshop"** (see SPEC.md Visual Theme for the design): the
whole game is a warm toy workshop — warm-paper backdrop, light-pine structure (Zone A
tray, Zone B rails and gate signs, the trap-door chute), brass accents, warm-brown ink.
Balls are **industrial materials**: `src/core/Materials.ts` holds the 20-material ladder
(5 families of 4, aligned with the 4-tier draw windows; wraps past 20 with a gold ring
per cycle) with per-material colours + **subtle physics multipliers**
(restitution/friction/density — wood bounces, metal slides, gems slip) that both zones
apply on top of their own tuned constants. `src/core/MaterialPainter.ts` draws the
procedural canvas recipes (shared base sphere + per-material detail pass; `'full'` LOD
for Zone A, `'small'` for Zone B's 10px balls where colour is the identity), and
`src/core/Theme.ts` names every environment colour (no hex literals in zone code). **The
environment palette swaps at every milestone**: `Theme` is now the *active* (mutable)
palette — `PALETTES` in `Theme.ts` holds five authored moods (workshop → dusk → night →
dawn → gilded; night/gilded flip to dark paper + light ink), each window-shift stage in
`progression.json` names one (`palette`, author-then-hold via `paletteNameForLevel` in
`Progression.ts`), and `src/core/ThemeDirector.ts` (a scene-level system in `ac`/`full`)
cross-fades `Theme` over the 1.2s milestone zoom (`PROGRESSION_CHANGED` picks the target,
`ARENA_ZOOM {active:true}` starts the fade; pure blend math in `themeMath.ts`,
unit-tested), emitting the contract's `THEME_CHANGED` (no payload) each tween tick. Every
surface that bakes a Theme colour at create keeps refs and restyles on that event
(GameScene backdrop + HTML letterbox, HUD, ArenaView walls/band, AimController queue row,
DeathLine, Zone C band, Zone B rails/gates/collector labels/score bar); balls/materials
are deliberately excluded — material look stays the tier signal. `GameScene.create()`
re-applies the workshop palette first thing, so `scene.restart()` can't leak a late-run
palette into the fresh bakes. Ball
faces carry **no value digits** — a ball's worth is hidden from the player; material look is
the only tier signal. Every score number the player *does* see (HUD total, Zone B's score-bar
label, the game-over final score) is formatted through `compactValue()` (531441 → "531K",
max 3 significant digits). `/material-preview.html` is a dev-only
proof sheet of the whole ladder — tune palette/recipes there, not in a live run. The old
`BallColors.ts` is deleted.

**Zone A** (`src/zoneA/`) plays: drag along the top to aim, release to drop; balls are
procedurally-textured Matter circles (material recipe, no value digits) that grow heavier and grippier by
tier, same-**value** collisions merge into the next tier with a neighbour-shoving blast.
Merging is **uncapped** — tiers climb forever — and each merge **triples** the value
(`tierToValue` is now `3^(tier-1)`, since merging two equal balls yields `1.5×(V+V)=3V`).
Ball radius reads the `RADII` table for tiers 1–10 and keeps growing geometrically past it
(`RADIUS_GROWTH` in `tuning.ts`); look + physics feel come from the shared
`src/core/Materials.ts` ladder (one source for both zones, so a transferred ball keeps its
material). Because balls grow without bound, the **arena expands at
recurring milestones**: every 25 levels Zone A input freezes, `ArenaView` grows the playfield
by a **per-milestone factor** = the *neutral ball-growth match* (`neutralGrowth` in
`ballMath.ts`: the window-max radius ratio, so apparent ball size holds constant) × the
stage's authored **`tightness`** in `progression.json` (<1 = tighter/harder, >1 = roomier
breather; current rhythm 0.92/1.05/0.85/1.05 — squeeze→breathe with deepening squeezes).
Ceiling rises, walls move out, funnel widens — always *outward*, never into Zone B — and a
**dedicated Zone-A camera** zooms out (`zoom = 1/s`) with relative positions intact. Past the
last authored window shift, milestones self-heal into plain levels (no growth). **Physics
feel is normalized to `s`** (`Board.ts`): each Zone A ball gets a supplemental
`(s−1)`-gravities force before every physics step (world gravity is shared with Zone B and
stays untouched), and the merge-blast radius/strength + rest-speed threshold scale by `s`,
so on-screen falls, shoves, and settling look identical at every milestone. Each
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
level 24, then jumps `[5,8]`/`[9,12]`/`[13,16]`/`[17,20]` at each 25-level milestone (each
shift stage also carries its `tightness`), and the `scoreBarTarget`s are rescaled to track
the (powers-of-three, now much larger) per-window value magnitudes, scaled down for the
smaller `bufferForLevel` supply at the halved milestone levels — the targets and the
tightness rhythm are starting numbers to tune by playtest. Past the last authored stage the
target **keeps growing geometrically** (`scoreBarTargetForLevel` / `TAIL_TARGET_GROWTH` =
3^(4/25) per level in `Progression.ts` — the authored curve's own rate), because merged ball
values are unbounded: a flat tail let one monster drain cross a frozen target thousands of
times, wedging the game behind hours of owed score-bar wraps. **Zone A also owns the phase
triggers**: it tracks the finite ball supply (`ZoneASystem.ballBuffer`, refilled per stage
from `progression.json`, broadcast via `BALL_BUFFER_CHANGED` to the queue row), and when
the buffer hits 0 a **settle gate** (`src/zoneA/settleGate.ts`: 350ms of contiguous
`Board.isSettled()` — no pending merges, every body asleep or under the scaled rest speed —
with a 4s hard timeout) emits `ZONE_A_DEPLETED` so the pan to the B phase waits for the
last drop's merges to resolve; the gate is skipped when the game is over, when a cash-in is
pending, or when the board is stalemate-shaped (buffer 0 + board empty + Zone B empty stays
`checkLoss`'s job). Filling the score bar is a
**cash-in reward beat** rather than an instant refill: the bar wraps through its levels LIVE as
Zone B drains (see Zone B's score-bar note), and `ZoneASystem` holds a
`scoreBarCashingIn`/`cashInPending` pair of flags so the stalemate check can't misfire while Zone
B finishes draining any balls still in flight. `SCORE_BAR_FILLED` now fires **once per level
crossed, live during the drain** (a burst when one drain earns several levels); its handler is
split for the phases: the level-up, stage apply, queue re-roll, and `PROGRESSION_CHANGED` emit
happen **immediately** per level (Zone B reads the new target synchronously for its next
crossing), but the **visible reward** — milestone zoom + drain, the ticked buffer refill, the drop
unlock — defers until the pan lands back in the A phase (released on `PHASE_CHANGED {phase:'A'}`),
so the milestone zoom never overlaps the pan, and a multi-level drain collapses into the final
level's one refill — but each burst level's zoom factor (`milestoneZoomFactor` in
`ballMath.ts`, computed per event while `internalLevel` is that level) **composes by product**
into the pending slot, so a roll-through that overshoots a milestone level still zooms. The
refill then launches one **brass particle per new slot**
(`animateBufferTo`, `bufferTickDelay` apart — `BUFFER_TICK_MS`=130 for ≤10 slots, then the
gap shrinks inversely with the count, floored at 55ms, so a big refill doesn't drag) that
arcs from the score bar up to the queue-row balls-left count
along a jittered bezier with a fading trail, and each slot only lands (count pop + audio
blip) when its particle arrives; drop unlocks on the first landing, so the refill reads as
the bar's energy flying up into the ball supply instead of a jump-cut. These particles are
drawn in a dedicated top-most **`src/core/OverlayScene.ts`** (registered after `GameScene`
in `main.ts`, transparent, input-disabled) — their path spans the whole screen (Zone B's
bottom → through Zone A → the HUD count), so they can't live on the main camera (Zone A's
dedicated camera paints an opaque band over it) nor its own scene camera; the overlay scene
is full-screen at scroll 0, so its coords are 1:1 with the screen chrome they target and it
renders above every Zone A ball and is immune to the phase pan. The **HUD score total** rides
the same overlay road: it does NOT display the live `SCORE_CHANGED`; instead it holds a frozen
`shownTotal` and, on each `SCORE_HARVESTED`, flies a brass `+N` number token (shrinking, with a
trail) from Zone B's haul spot up to the top total, which counts up + scale-pops when the token
lands — so the number stays frozen through a B round and jumps once, in sync with the pan up.
Both fly-ups (buffer refill + score harvest) share one helper, **`src/core/overlayFx.ts`**
(`launchOverlayFlyer`: jittered quadratic-bezier arc + fading trail, drive tween on GameScene so
a restart kills it, object on the surviving overlay scene). Because that overlay scene outlives a
`scene.restart()`, `HUD.destroy()` discards any in-flight harvest tokens. The game-over
overlay + RESTART are pinned screen-space (`setScrollFactor(0)`), so they render
full-screen and clickable in either phase framing.

**Zone B** (`src/zoneB/`) is fully implemented: balls spawn on `BALL_DROPPED`, three gate
types (static, translating, rotating) split balls into copies via a pending-queue pattern,
collectors (sensor areas, any position) drain balls and score their value × collector
multiplier, and walls guide trajectories.
The playfield is now one of **two layouts** (`LAYOUT_1`, `LAYOUT_2` in `zoneLayout.ts`),
chosen at random per run via `pickRandomLayout()` in `ZoneBSystem`'s constructor (so each
boot/`scene.restart()` re-rolls). Both are static "shelf cascade" layouts modelled on the
reference art: stacked horizontal multiplier gates split by vertical/diagonal guide rails,
funnelling into one bottom collector. Gates are painted wooden signs (green paint for
multiplier ≥4, brass below) with a dark stencilled `X#` label; multipliers are tuned (≤4)
so cascades stay balanced. Gate visuals live in `GateSystem.buildBody()`. The Zone B world
band is `y=607..1238` — taller than what the A phase shows (the bottom 394px, including the
score bar, are off-screen until the pan) and exactly filling the screen below Zone C in the
B phase. The ball supply lives in Zone A (`zoneB/BallBuffer.ts` is dead code, kept only by
its own unit test); Zone B's job is scoring (`SCORE_CHANGED`, `SCORE_BAR_CHANGED`,
`SCORE_BAR_FILLED`, `SCORE_HARVESTED`) and the busy/empty door signals. Above the bar hovers a
**haul label** — `ZoneBSystem` tracks `roundScore` (the total earned THIS B round, reset each
cash-in) and `pumpHaulLabel` shows it as a big `+N` chip that counts up and whose base scale
grows with the haul magnitude (capped at `HAUL_MAX_SCALE`, a Back-ease pop per add). At cash-in
(`harvestRound`, just before `SCORE_BAR_CASHED_IN`) it emits `SCORE_HARVESTED {amount,x,y}` with
the label's on-screen position (world→screen via `cameras.main.scrollY`, clamped inside the
bottom edge for a rare phase-A milestone-drain cash-in) and hides the label — the number never
flies itself (a world-space object would slide off-screen as the camera pans up); the HUD flies a
matching token instead (see below). The **score bar** is drawn by
`ZoneBSystem` along the bottom of the Zone B band (a groove-bg + brass fill rectangle with the
`X / Y` label centred inside a 16px-tall bar, lifted a small margin off the screen edge). Its
shown fill is tracked as a 0..1 `displayFraction`, and the bar fills and wraps **live as balls
drain** — no waiting for the drain to finish. `addScore` accumulates into `ScoreBar` and, in a
loop, consumes one level per target crossed: each crossing emits `SCORE_BAR_FILLED` (a real Zone A
level-up that — synchronously, via `PROGRESSION_CHANGED` → `setTarget` — raises the next target)
and queues one owed wrap, **capped at `MAX_LEVELS_PER_CASHIN` (10) per cash-in cycle** — at the
cap the rest of the fill is forfeited (`ScoreBar.forfeitOverflow`, bar rests at 99%), the safety
valve that keeps a freak monster drain from banking thousands of owed wraps. `animateBar` eases `displayFraction` toward the current fill when nothing
is owed, and when wraps are owed sweeps the bar to full (`WRAP_FILL_MS`) then celebrates + snaps it
to empty per wrap — so the fill → empty → fill roll plays out while the balls are still cascading.
`ScoreBar` is deliberately minimal (`filled` + `target`, `add`/`crossedTarget`/`consumeLevel`), so a
single big drain rolls through several levels and lands the exact remainder — N crossings = N
level-ups. Each bar-full fires the celebration (`celebrateFull`: groove+fill throb, brass sparkle,
`Sfx.goal()`). The **pan up waits for the drain, not the fills**: once Zone B drains with a level
owed, `ZONE_B_EMPTY` is deferred (so Zone C's door stays locked, no ball injected mid-cash-in) and
`updateResolve` waits for the bar to finish its wraps + a short `SETTLE_DWELL_MS` beat, then emits
`ZONE_B_EMPTY` + `SCORE_BAR_CASHED_IN` (the PhaseDirector's pan-up trigger).
Balls are small
(10px radius) and **collide with each other** (the `CAT_BALL` mask includes itself), so they
pile and nudge in the cascade. Ball textures use the same shared
`src/core/Materials.ts` + `MaterialPainter.ts` recipes as Zone A (small LOD), with the
material physics multipliers applied to Zone B's own constants (restitution capped at
0.5). The zone splits into `ZoneBSystem` plus
`GateSystem`, `CollectorSystem`, `WallSystem`, `ZoneBBall`, `BallBuffer`, and `zoneLayout.ts`;
the old `Funnel.ts` skeleton is **superseded by `CollectorSystem` and is now dead code**.

**Zone C** (`src/zoneC/ZoneCSystem.ts`) plays: the trap-door lock composes three flags —
`phaseLocked` (armed only in the B phase, boots locked), `zoomLocked` (`ARENA_ZOOM`), and
`locked` (Zone B's busy/empty events). While armed, the door band shows **nine evenly-spaced position markers**
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
double-drawn); since sucks only happen in the B-phase framing, the snapshot's spawn y adds
`cameras.main.scrollY` to convert those screen coords into the scrolled world. Only when the pop lands does it emit `BALL_DROPPED` at the frozen `x`, so Zone B
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
