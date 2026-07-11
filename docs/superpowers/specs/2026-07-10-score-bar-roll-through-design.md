# Score Bar Multi-Level Roll-Through — Design

## Problem

When a Zone B drain earns enough score to cross several score-bar targets in one go, the
bar today eases up to full, **pins**, and then snaps down once per cascaded level after the
drain settles. The player never sees the intermediate levels fill — and the accounting is
lossy (see below). The goal: after the bar fills once, it should visibly reset to the next
level empty and keep filling with the extra score, rolling through every level the drain
earns and landing on the final partial level.

## Decisions (locked)

1. **Each wrap is a real level-up.** Crossing N targets = N `SCORE_BAR_FILLED` emits = N
   Zone A level-ups (buffer refill + progression advance), exactly as the reward machinery
   does today. This is a visual layer over logic that already exists — plus a correctness
   fix to make the level count exact.
2. **Roll after the drain settles**, not live. During the cascade the bar fills and pins
   full (as today); once Zone B empties and the existing dwell elapses, one smooth
   roll-through plays: full → wrap to empty → fill → … → land on the final partial level.
3. **Snappy roll, pan as today.** The pan back up to Zone A still triggers on the first
   `SCORE_BAR_FILLED`. The roll is kept fast (~240ms/level) so a typical 3–4 level roll
   finishes about as the 650ms pan plays. No PhaseDirector / contract / pan-timing changes.
   A very long roll's tail may clip off-screen — rare, since targets are tuned to ~one
   level per drain.

## Bug this fixes (required by the feature)

`ScoreBar.completeCashIn()` collapses **all** banked `overflow` into `filled` in a single
step. Cascading through 2+ extra levels therefore loses the remainder: banking 3.3 levels
of score yields only 2 level-ups and the bar lands empty instead of 3 level-ups landing at
30%. For the bar to honestly animate "all the levels that get filled," the accounting must
carry an exact remainder across each level. This redesign replaces that model.

## Component 1 — `src/zoneB/ScoreBar.ts` (pure logic)

Remainder-carrying model:

- `filled` — progress toward the current target; pinned exactly at `target` while cashing in.
- `pending` — **all** points banked beyond a full bar, carried precisely across the roll.
- `add(points)`:
  - while cashing in → `pending += points`, return `false`.
  - else `filled += points`; if `filled >= target` → `pending = filled - target`,
    `filled = target`, `cashingIn = true`, return `true`; else return `false`.
- **`advanceLevel()`** (replaces `completeCashIn()`): consumes exactly one level against the
  **current** target (Zone A has already updated it via `ProgressionChanged` before this is
  called):
  - if `pending >= target` → `filled = target`, `pending -= target`, `cashingIn = true`,
    return `true` (another full level remains to roll).
  - else → `filled = pending`, `pending = 0`, `cashingIn = false`, return `false` (this is
    the final resting level).

Getters `getFilled()` / `getTarget()` / `isCashingIn()` / `setTarget()` stay. `getProgress()`
stays. The `SCORE_BAR_TARGET` export stays.

### Tests (`ScoreBar.test.ts`, rewritten)

- accumulates without filling;
- first crossing pins `filled` at `target` (not the over-value) and banks the excess to
  `pending`, returns `true`;
- further points while cashing in bank to `pending`, `filled` stays at `target`;
- `advanceLevel()` with `pending < target` → lands `filled = pending`, exits cash-in,
  returns `false`;
- `advanceLevel()` with `pending >= target` → `filled = target`, decrements `pending`,
  stays cashing-in, returns `true`;
- **3+ level carry:** bank ~3.3 targets, then `advanceLevel()` three times against a fixed
  target lands the exact 0.3-level remainder with the right true/true/false sequence (the
  case the old model lost);
- `advanceLevel()` honors a target changed via `setTarget()` between calls (variable
  per-level targets);
- `add` works normally again after a non-cascading `advanceLevel()`.

## Component 2 — `src/zoneB/ZoneBSystem.ts` (orchestration + display)

### Display: fraction-based

Replace `displayFilled` (absolute) with `displayFraction` (0..1).

`renderBar()` paints `barFill.width = fillW * displayFraction` and labels
`compactValue(round(displayFraction * target)) / compactValue(target)`. As the bar wraps,
the numerator resets to 0 and climbs again; the denominator picks up each new level's target.

### Phase A — live fill (Zone B busy)

`animateBar(delta)` eases `displayFraction` toward `min(1, filled/target)`, snapping **down**
instantly on a reset (the bar only animates upward otherwise). The instant `displayFraction`
reaches 1, fire `celebrateFull()` once (guarded by `celebrated`). Bar then pins full;
`ScoreBar.add` banks overflow to `pending`. **No `SCORE_BAR_FILLED` yet** — crediting is
deferred to the roll.

### Phase B — roll (Zone B empty → `CASH_IN_DWELL_MS` dwell)

The dwell arming is unchanged (`onBarFilled` → `pendingCashIn` when in-flight, armed on
`onBallDrained` reaching 0; armed immediately when already empty). After the dwell, instead
of the old `resolveCashIn`, enter the roll:

```
startRoll():
  rollActive = true
  cashInFullLevel()          // the bar is displayed full and already celebrated (live fill)

cashInFullLevel():           // bar is visually full; cash this level in and advance
  bus.emit(ScoreBarFilled)                     // Zone A: level up + new target (sync)
  more = scoreBar.advanceLevel()               // consume one level vs the updated target
  emitScoreBar()
  if more:
    animateWrapAndFill(1, onDone = () => { celebrateFull(); cashInFullLevel() })
  else:
    animateWrapAndFill(scoreBar.getFilled() / scoreBar.getTarget(),
                       onDone = () => finishRoll())

finishRoll():
  rollActive = false
  celebrated = (displayFraction >= 1)          // reset guard so the next drain celebrates
```

`animateWrapAndFill(rest, onDone)` — two-segment tween on `displayFraction`:
- segment 1 (WRAP): `1 → 0` over `WRAP_MS` (~90ms);
- segment 2 (FILL): `0 → rest` over `FILL_MS` (~150ms), ease-out; `onComplete → onDone`.

Level 1's celebrate happened during live fill; levels 2..N celebrate at the completion of
each roll fill (the `more` branch's `onDone`). One throb + sparkle + `Sfx.goal()` per bar
that fills.

While `rollActive`, `animateBar` skips the ease and just calls `renderBar()` — the tween
owns `displayFraction`.

### Edge cases

- **Exact fill, zero overflow:** `pending = 0` at cash-in → `advanceLevel()` returns
  `false` with `filled = 0` → `animateWrapAndFill(0)` wraps to empty and rests there. Correct
  ("changes to the next level empty").
- **Many levels:** recurses one level per `cashInFullLevel()`, each celebrating on fill.
- **Target changes mid-roll:** each `SCORE_BAR_FILLED` → Zone A `ProgressionChanged` →
  `scoreBar.setTarget(new)` before `advanceLevel()` reads it. Variable per-level targets
  handled.

### SFX change

Move `Sfx.goal()` out of `onBarFilled` into `celebrateFull()`, so every full bar sounds —
the live first fill and every rolled level.

## Unchanged / not touched

- **Contract** (`contracts.ts`) — no new events; `SCORE_BAR_FILLED` / `SCORE_BAR_CHANGED`
  semantics preserved. The phase machine already tolerates multiple `SCORE_BAR_FILLED`
  emits (today's cascade fires several).
- **PhaseDirector / pan timing** — pan up still triggers on the first `SCORE_BAR_FILLED`.
- **Zone A reward machinery** — multiple `SCORE_BAR_FILLED` collapse into the final level's
  buffer refill and one milestone check exactly as today (`pendingCashIn` single-slot,
  `runCashInSequence` reads the final `internalLevel`). `scoreBarCashingIn` stays set through
  the whole roll (each intermediate `filled == target`) and clears when the final level lands
  `filled < target`.
- The dwell gate, `pendingCashIn`/`ZONE_B_EMPTY` sequencing, `celebrateFull` visuals.

## Tuning constants (playtest)

- `WRAP_MS` ≈ 90, `FILL_MS` ≈ 150 (≈240ms/level).
- `CASH_IN_DWELL_MS` unchanged (600).
- `FILL_LERP` unchanged (0.2) for the live ease.

## Addendum (correction during implementation): hold the pan until the roll ends

Decision 3 above ("snappy roll, pan as today") turned out to be wrong in practice. The pan
up is triggered by the first `SCORE_BAR_FILLED`, and the score bar lives at the bottom of
Zone B's world — so the pan carries the bar off-screen the instant level 1 cashes in, hiding
**every** level past the first. Runtime evidence (Playwright driving the real phase-B flow)
confirmed the roll logic worked but the camera left before levels 2..N were visible.

Fix (user-approved): the pan up now **waits for the whole roll**.

- New contract event **`SCORE_BAR_CASHED_IN`** (`void`) — fired once, in `finishRoll()`, when
  the entire roll-through has resolved. PhaseDirector panes on this instead of
  `SCORE_BAR_FILLED`. The pure `phaseMachine` is unchanged — the new event is just routed to
  the existing `'barFilled'` input, preserving the `refillQueued` turnaround.
- `SCORE_BAR_FILLED` stays the per-level level-up (Zone A + the `?zone=ac` stub keep it). The
  stub, which does no multi-level roll, emits `SCORE_BAR_CASHED_IN` in the same beat.
- `ZoneBSystem` **defers `ZONE_B_EMPTY`** through the cash-in: `onBallDrained` skips it when a
  cash-in is pending, so Zone B reads as "busy" and Zone C's trap door stays locked (no ball
  injected mid-roll); `finishRoll` emits `ZONE_B_EMPTY` + `SCORE_BAR_CASHED_IN`.

Verified at runtime: a 5-level roll in phase B cashed all five levels in the B framing, then
`PAN START` fired only after `ROLL DONE`.

## Addendum 2 (correction): fill LIVE as balls drain, not after the drain settles

The "after the drain settles" timing (Decision 2) left a visible stall: the first fill happened
live during the drain, but the subsequent wraps waited for the drain + dwell (the post-drain
roll). The user wanted the whole fill/empty/fill roll to play out **live, as balls drain**, with
no stall between fills.

Fix (user-approved — reverses Decision 2 to "live as balls drain"):

- **`ScoreBar` simplified** to `filled` + `target` with `add` / `crossedTarget` / `consumeLevel`
  (no more `pending` / `cashingIn` / `advanceLevel` — there is no post-drain roll to bank for).
- **`ZoneBSystem.addScore`** wraps live: after `add`, a `while (crossedTarget())` loop consumes
  one level per crossing, emitting `SCORE_BAR_FILLED` (Zone A levels up + raises the target
  synchronously) and queuing one owed **`pendingWraps`**.
- **`animateBar`** works off `pendingWraps`: sweep to full (`WRAP_FILL_MS`), celebrate, snap to
  empty, decrement — one visible wrap per owed level, interleaved with the live ease.
- The post-drain roll (`startRoll`/`cashInFullLevel`/`animateWrapAndFill`/`finishRoll`) is gone.
- The pan still waits for the **drain** (not the fills): when Zone B drains with a level owed,
  `ZONE_B_EMPTY` is deferred; `updateResolve` waits for `pendingWraps == 0` + a short
  `SETTLE_DWELL_MS`, then emits `ZONE_B_EMPTY` + `SCORE_BAR_CASHED_IN`.

Verified at runtime: crossings fired at `inFlight` = 24/21/17/11/5 (balls still draining) with no
stall between them, and `SCORE_BAR_CASHED_IN` / the pan fired only after the drain fully settled.

## Addendum 3 (bug fix): milestone zoom lost when the roll overshot a milestone

The "Unchanged" claim that Zone A's reward machinery needed no change ("`pendingCashIn`
single-slot, `runCashInSequence` reads the final `internalLevel`") was wrong. Each burst event
**overwrote** the single pending slot, and the milestone check read the burst's *final* level —
so a roll-through that crossed a milestone mid-burst (e.g. 49 → 50 → 51, milestone at 50)
silently dropped the arena zoom + blacklist drain: the window shifted but the arena never grew.
Before this feature, `SCORE_BAR_FILLED` fired once per cash-in, so the single slot was safe.

Fix: the per-level zoom decision is now the pure `milestoneZoomFactor(level, prevWindow,
window, tightness)` in `ballMath.ts` (1 = no zoom; unit-tested, including the overshoot
regression), evaluated **inside the `SCORE_BAR_FILLED` handler while `internalLevel` is that
event's level**. The pending slot stores `{ stage, zoomFactor }` and composes the burst's
factors by product (`ArenaView.grow` is multiplicative), so a mid-burst milestone keeps its
zoom while the refill still collapses to the final level.

Verified at runtime (Playwright, real bus, real PhaseDirector pan): the 49→51 burst zoomed the
arena camera 1 → 0.491 on the pre-existing code path it previously skipped; the same scenario
on the pre-fix code reproduced NO ZOOM; a plain 51→53 burst stayed un-zoomed and an exact
49→50 landing still zoomed.

## Addendum 4 (bug fix): endless-wrap wedge past the authored progression table

Field report: a very high merged-tier ball drained late-game left the bar wrapping for 5+
minutes with the pan never firing. Two compounding causes, both consequences of this
feature's live wrap loop meeting the endless tail:

1. Past `progression.json`'s last stage (level 100), `getStage` held `scoreBarTarget` flat at
   670M **forever**, while merged ball values keep tripling per tier without bound (and Zone B
   multiplies copies). One drain can then cross the frozen target thousands of times — each a
   real level-up + one owed wrap at `WRAP_FILL_MS` + a celebration — and `updateResolve` holds
   `ZONE_B_EMPTY`/`SCORE_BAR_CASHED_IN` until `pendingWraps == 0`, so the pan is wedged for
   hours and Zone A's level/buffer explode.
2. Nothing bounded the crossings even in authored territory.

Fix (user-approved: "extrapolate + safety cap"):

- **`scoreBarTargetForLevel(level)`** (`Progression.ts`): authored targets through level 100,
  then geometric growth at **`TAIL_TARGET_GROWTH` = 3^(4/25) per level** — the authored
  curve's own rate (105K@50 → 670M@100 ≈ ×3⁸/50 levels, i.e. one draw-window of value per
  milestone span) — so one good drain keeps earning ~one level forever. `ZoneASystem`'s
  `PROGRESSION_CHANGED` emit now uses it instead of `stage.scoreBarTarget`.
- **`MAX_LEVELS_PER_CASHIN` = 10** (`ZoneBSystem.addScore`): a cash-in cycle banks at most 10
  level-ups; at the cap the remaining fill is forfeited via the new
  `ScoreBar.forfeitOverflow()` (bar rests at 99%) so the loop terminates and the roll stays a
  ~1.5s beat. The counter resets when the cycle resolves in `updateResolve`.

Verified at runtime (Playwright, real cascade): a tier-31 ball (3³⁰ ≈ 2×10¹⁴ points) dropped
into Zone B banked exactly 10 fills, resolved `SCORE_BAR_CASHED_IN` + panned up in 4.6s with
`pendingWraps` at 0 and the bar resting at 99%; a level-150 fill broadcast an extrapolated
target of 5.24e12 (vs the frozen 670M).
