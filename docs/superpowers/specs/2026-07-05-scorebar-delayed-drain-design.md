# Score bar delayed-drain reward — design

## Problem

Today, the moment a drained Zone B ball tips the score bar past its target, three things
happen on the same frame, invisibly: the internal level advances, the ball buffer is set
straight to the new stage's capacity, and the bar resets to 0. The only feedback is a
number that silently jumps in the queue-row HUD. Filling the bar is meant to be the game's
core reward loop, but it currently doesn't read as an event at all — there's no beat that
says "you earned this."

## Goal

Make filling the score bar feel like a deliberate, visible payoff: the bar visibly holds at
full, visibly drains, and the ball buffer visibly arrives — one slot at a time — rather than
snapping. This is a feedback/pacing change only; score-bar target values
(`progression.json`'s `scoreBarTarget`) are out of scope for this pass.

## Sequence

Triggered the instant `ScoreBar.add()` reports a fresh fill:

1. **Fill** — the bar visually pins at 100%. Any further points scored during the whole
   sequence below are banked into an `overflow` accumulator rather than shown — the display
   doesn't move again until cash-in. The existing `Sfx.goal()` cue plays here, at the
   instant of fill, regardless of whether Zone B is still busy.
2. **Hold (gated on Zone B emptying)** — if Zone B still has balls in flight (`inFlight > 0`)
   when the bar fills, the bar stays pinned full and static — no timer runs — for as long as
   balls are still cascading through gates and collectors. Only once Zone B reports empty
   (`ZONE_B_EMPTY`, i.e. `inFlight` reaches 0) does the dwell timer arm. If Zone B is already
   empty at the instant of fill, the dwell arms immediately — this is the common case for a
   single ball that drains without cascading.
3. **Dwell** (~600ms, tunable) — once armed, the bar sits full for this beat.
4. **Drain-out** (~400ms tween) — the bar's fill rectangle tweens from full width to 0,
   left-to-right. This is a cosmetic tween only — `ScoreBar`'s internal `filled` value does
   not change yet.
5. **Cash-in** — the instant the drain tween completes: `ScoreBar.completeCashIn()` resets
   `filled` to the banked `overflow` (0 if none) and clears `overflow`/`cashingIn`. Zone A's
   `SCORE_BAR_FILLED` handler ticks the ball buffer up from its current count to the new
   stage capacity, one slot every ~130ms, each tick popping the queue-row number and playing
   an ascending blip. Drop unlocks the moment the count first goes above 0, not at the end
   of the ticking.

The player keeps dropping into Zone A normally through the entire sequence — nothing
freezes. The internal level bump, `PROGRESSION_CHANGED` emit, and milestone-zoom check (in
`ZoneASystem`'s `ScoreBarFilled` handler) are **not** part of this delay — they still fire
the instant the fill happens, unchanged. Only the *buffer arrival* is what's being slowed
down and made visible.

### Cascade edge case

If the overflow banked during dwell+drain is itself ≥ the (possibly now-higher, since the
level also advanced) new target, `completeCashIn()` reports this and `ZoneBSystem`
immediately starts another full dwell→drain→cash-in cycle for it. This is the honest
consequence of overfilling the bar during the reward window, not a bug to special-case away.

### Skip case

If the new stage's buffer capacity is ≤ the current buffer count (shouldn't normally happen
since capacity is non-decreasing across stages, but a defensive default), Zone A skips the
ticked animation and applies the new count in one step, same as today.

## Components

### `zoneB/ScoreBar.ts` (pure logic, unit-tested)

- New private state: `cashingIn: boolean`, `overflow: number`.
- `add(points)`: if `cashingIn`, route `points` into `overflow` and return `false` (no new
  fill event — the bar is already mid cash-in). Otherwise accumulate into `filled` as today;
  if it crosses `target`, set `cashingIn = true` (but do **not** reset `filled` — it stays
  pinned at its current, over-target value so the visual can dwell there) and return `true`.
- New `completeCashIn(): boolean` — sets `filled = overflow`, `overflow = 0`,
  `cashingIn = false`; returns `filled >= target` (the cascade signal).
- `getFilled()` while `cashingIn` still returns the true filled/overflowed value for the
  drain tween's starting point if ever needed, but the drain tween itself is driven by
  `ZoneBSystem` off a fixed "full width → 0" cosmetic range, not by re-reading `getFilled()`
  mid-tween.

### `zoneB/ZoneBSystem.ts`

- `addScore()`: unchanged emit of `SCORE_CHANGED`; `scoreBar.add(points)` return value drives
  the new sequencing instead of emitting `ScoreBarFilled` synchronously.
- New `pendingCashIn: boolean` field — true once the bar has crossed target but Zone B still
  has balls in flight; cleared the moment `onBallDrained()` sees `inFlight` reach 0.
- New `onBarFilled()`: `Sfx.goal()` (kept at the original trigger point, plays regardless of
  Zone B's flight state) → if `inFlight === 0`, calls `armCashInTimer()` immediately;
  otherwise sets `pendingCashIn = true` and returns (the bar stays pinned full and static —
  no timer — until Zone B empties).
- New `armCashInTimer()`: `scene.time.delayedCall(CASH_IN_DWELL_MS, () => this.beginDrainOut())`.
  Only ever called once Zone B is confirmed empty (either immediately from `onBarFilled`, or
  deferred via `onBallDrained`).
- `onBallDrained()`: unchanged busy/empty bookkeeping, plus — the instant `inFlight` reaches
  0 and `ZONE_B_EMPTY` is emitted — if `pendingCashIn` is set, clears it and calls
  `armCashInTimer()`.
- New `beginDrainOut()`: `scene.tweens.add` on the bar-fill rectangle's `displayWidth` from
  its current width to 0 over `CASH_IN_DRAIN_MS`, `onUpdate` continuing to call the existing
  `updateBarVisual`-style rendering so the bar shrinks smoothly; `onComplete` calls
  `scoreBar.completeCashIn()`, re-emits `SCORE_BAR_CHANGED` with the reset values, emits
  `SCORE_BAR_FILLED`, and — if `completeCashIn()` returned true (cascade) — calls
  `onBarFilled()` again (re-entering the same fill-detection/Zone-B-empty gate, in case new
  balls entered Zone B during the dwell/drain window).
- New tunable constants alongside the existing `SCORE_BAR_TARGET`-style constants:
  `CASH_IN_DWELL_MS = 600`, `CASH_IN_DRAIN_MS = 400` (starting points, tune by playtest).

### `zoneA/ZoneASystem.ts`

- `ScoreBarFilled` handler keeps its existing instant work (level increment, `applyStage`
  for window/queue-seed purposes, `ProgressionChanged` emit, milestone-zoom check) —
  none of that is delayed.
- Only the buffer assignment changes: replace the direct
  `this.ballBuffer = stage.bufferCapacity` with a call to a new `animateBufferTo(newCapacity)`
  helper that uses `scene.time.addEvent({ delay: BUFFER_TICK_MS, repeat, callback })` to
  increment `this.ballBuffer` by 1 per tick, calling the existing `emitBuffer()` each time
  (which already drives `BallBufferChanged` + the queue row), and calling
  `this.aim?.setDropLocked(false)` the first time the count goes above 0. If
  `newCapacity <= this.ballBuffer`, skip straight to the one-shot assignment.
- New tunable constant `BUFFER_TICK_MS = 130` (starting point).

### `zoneA/AimController.ts`

- `setBallsLeft()` (or wherever the queue-row buffer number is rendered) gets a short
  pop/scale tween applied each time it's called, instead of a flat re-render. Since it's now
  called once per tick during a refill, this alone produces the "count up one by one with a
  pop" feel — no new queue-row architecture needed.

### `core/Sfx.ts`

- One new short cue, e.g. `Sfx.bufferTick(index)`, reusing the existing `comboPitch.ts`
  pitch-climb helper the same way merge/multiply chains already do, so a run of buffer ticks
  reads as a short ascending flourish rather than N identical blips.

No changes to `core/contracts.ts`. This reuses `SCORE_BAR_CHANGED`, `SCORE_BAR_FILLED`, and
`BALL_BUFFER_CHANGED` exactly as they exist today — the change is entirely about *when* and
*how gradually* those events fire, not their shape.

## Testing

- `ScoreBar.test.ts` (new or extended): unit-test the `cashingIn`/`overflow` state machine —
  points during cash-in don't move `filled`; `completeCashIn()` correctly resets and reports
  cascade; a fill exactly at target with no overflow does not cascade.
- Manual/visual verification (per `verify` skill): trigger a bar fill in `?zone=b` or
  `?zone=full` via the debug harness, confirm the dwell→drain→ticked-refill sequence plays,
  input stays live throughout, and a forced-overflow scenario (rapid drains during dwell)
  correctly cascades instead of losing points.

## Out of scope

- Rebalancing `scoreBarTarget` values in `progression.json`.
- Any change to milestone arena-zoom timing/locking.
- Physical balls falling into Zone A's play area (buffer refill stays an abstract HUD
  counter, per design decision above).
