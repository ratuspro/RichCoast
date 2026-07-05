# Score Bar Delayed Drain — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the score-bar-fill → buffer-refill moment feel like an earned reward instead
of a silent, instant number change — the bar dwells full, visibly drains out, and the
buffer count ticks up one slot at a time.

**Architecture:** `ScoreBar` (pure logic) gains a `cashingIn`/`overflow` state machine so
crossing the target pins the display instead of resetting it immediately. `ZoneBSystem`
sequences a dwell → tween-drain → resolve cycle around that state, and only then emits the
existing `SCORE_BAR_FILLED` event (unchanged shape — timing only). `ZoneASystem`'s handler
for that event now ticks `ballBuffer` up to the new capacity one slot per timer tick instead
of assigning it in one shot, calling the existing `emitBuffer()`/HUD path each tick. A small
pop animation on the queue-row count (`AimController`) and an ascending blip cue (`Sfx`) ride
along on those same per-tick calls.

**Tech Stack:** Phaser 4 + TypeScript (strict) + Vite, Vitest for pure-logic unit tests.

## Global Constraints

- No changes to `src/core/contracts.ts` — this reuses `SCORE_BAR_CHANGED`,
  `SCORE_BAR_FILLED`, and `BALL_BUFFER_CHANGED` exactly as they exist today.
- No changes to `progression.json` values (score-bar target rebalancing is explicitly out
  of scope for this pass).
- `npm run test` (Vitest) and `npm run typecheck` (tsc strict) must both pass before any
  commit that isn't purely a test-scaffolding step.
- No external asset dependencies — audio stays procedural Web Audio, as today.
- Follow the ownership boundaries in `TECH_SPEC.md`: `zoneB/` changes are Dev 2's area,
  `zoneA/` and `core/Sfx.ts` are Dev 1's/shared — this plan touches both, which is expected
  since the feature spans the seam, but keep edits scoped to exactly the files listed below.

---

### Task 1: `ScoreBar` cash-in/overflow state machine

**Files:**
- Modify: `src/zoneB/ScoreBar.ts` (full rewrite, file is 30 lines today)
- Test: `src/zoneB/ScoreBar.test.ts` (new)

**Interfaces:**
- Produces: `ScoreBar.add(points: number): boolean` (existing signature, new semantics — see
  below), `ScoreBar.completeCashIn(): boolean` (new), `ScoreBar.isCashingIn(): boolean` (new).
  `getFilled()`, `getTarget()`, `getProgress()`, `setTarget()` keep their existing signatures
  and are consumed by Task 4.

- [ ] **Step 1: Write the failing tests**

Create `src/zoneB/ScoreBar.test.ts`:

```ts
import { describe, test, expect } from 'vitest';
import { ScoreBar } from './ScoreBar';

describe('ScoreBar', () => {
  test('accumulates points without filling', () => {
    const bar = new ScoreBar(10);
    expect(bar.add(4)).toBe(false);
    expect(bar.getFilled()).toBe(4);
  });

  test('returns true and pins filled at the over-target value on first fill', () => {
    const bar = new ScoreBar(10);
    expect(bar.add(7)).toBe(false);
    expect(bar.add(6)).toBe(true); // 13 >= 10
    expect(bar.getFilled()).toBe(13);
    expect(bar.isCashingIn()).toBe(true);
  });

  test('further points while cashing in are banked, not shown', () => {
    const bar = new ScoreBar(10);
    bar.add(10); // fills exactly, enters cash-in
    expect(bar.add(5)).toBe(false); // banked, no new fill event
    expect(bar.getFilled()).toBe(10); // unchanged while cashing in
  });

  test('completeCashIn resets to the banked overflow and exits cash-in when below target', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.add(3); // banked overflow
    expect(bar.completeCashIn()).toBe(false);
    expect(bar.getFilled()).toBe(3);
    expect(bar.isCashingIn()).toBe(false);
  });

  test('completeCashIn cascades when the banked overflow alone reaches target', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.add(12); // overflow alone already >= target
    expect(bar.completeCashIn()).toBe(true);
    expect(bar.getFilled()).toBe(12);
    expect(bar.isCashingIn()).toBe(true);
  });

  test('completeCashIn with no overflow resets to zero', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    expect(bar.completeCashIn()).toBe(false);
    expect(bar.getFilled()).toBe(0);
  });

  test('add works normally again after a non-cascading completeCashIn', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.completeCashIn();
    expect(bar.add(9)).toBe(false);
    expect(bar.add(2)).toBe(true);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm run test -- ScoreBar`
Expected: FAIL — `isCashingIn` and `completeCashIn` are not functions (current `ScoreBar`
resets `filled` to 0 synchronously inside `add`, so several assertions above don't hold).

- [ ] **Step 3: Rewrite `ScoreBar.ts`**

Replace the full contents of `src/zoneB/ScoreBar.ts`:

```ts
/** Points needed to fill the bar and trigger a buffer refill. Tunable. */
export const SCORE_BAR_TARGET = 10;

/**
 * Pure score-bar logic — no Phaser dependency, fully unit-testable.
 *
 * `add(points)` accumulates points and returns `true` the moment the bar first
 * crosses `target`. Crossing does NOT reset the bar immediately — it enters a
 * "cashing in" state where `filled` stays pinned at its (possibly over-target)
 * value, so a caller can visually dwell on a full bar, and any further points
 * are banked into `overflow` instead of shown. Call `completeCashIn()` once
 * that dwell/drain-out sequence finishes to actually reset the bar and carry
 * the banked overflow into the next cycle.
 */
export class ScoreBar {
  private filled = 0;
  private overflow = 0;
  private cashingIn = false;

  constructor(private target = SCORE_BAR_TARGET) {}

  /** Returns true the moment this addition first crosses the target (enters cash-in). */
  add(points: number): boolean {
    if (this.cashingIn) {
      this.overflow += points;
      return false;
    }
    this.filled += points;
    if (this.filled >= this.target) {
      this.cashingIn = true;
      return true;
    }
    return false;
  }

  /**
   * Resolve a cash-in: reset `filled` to the banked `overflow` (0 if none) and
   * clear it. If the carried-over amount alone already reaches `target`, stays
   * in cash-in state and returns true (the caller should immediately begin
   * another dwell/drain-out cycle); otherwise clears cash-in state and returns
   * false.
   */
  completeCashIn(): boolean {
    this.filled = this.overflow;
    this.overflow = 0;
    this.cashingIn = this.filled >= this.target;
    return this.cashingIn;
  }

  isCashingIn(): boolean { return this.cashingIn; }
  setTarget(target: number): void { this.target = target; }
  getFilled(): number { return this.filled; }
  getTarget(): number { return this.target; }
  getProgress(): number { return this.filled / this.target; }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm run test -- ScoreBar`
Expected: PASS (7 tests)

- [ ] **Step 5: Typecheck**

Run: `npm run typecheck`
Expected: no errors (nothing else references `ScoreBar` yet except `ZoneBSystem`, which
still only calls `add`/`getFilled`/`getTarget`/`getProgress`/`setTarget` — all unchanged
signatures — so it should already compile).

- [ ] **Step 6: Commit**

```bash
git add src/zoneB/ScoreBar.ts src/zoneB/ScoreBar.test.ts
git commit -m "Add cash-in/overflow state machine to ScoreBar"
```

---

### Task 2: `Sfx.bufferTick` cue

**Files:**
- Modify: `src/core/Sfx.ts:96-109` (insert a new method after `collect`, before `goal`)

**Interfaces:**
- Produces: `Sfx.bufferTick(index: number): void` — consumed by Task 5.

- [ ] **Step 1: Add the cue**

In `src/core/Sfx.ts`, insert this method directly after the existing `collect(value)` method
(currently ending at line 101) and before the `goal()` method:

```ts
  /** Zone A: one ball buffer slot arrives during a score-bar cash-in refill. Ascending
   *  blip, `index`-th in the refill sequence (0-based) — climbs a semitone per step like
   *  the merge/multiply combo chains, but driven directly by the caller's own counter since
   *  buffer ticks already run on a fixed cadence rather than player-timed hits. */
  bufferTick(index: number): void {
    const base = 784.0; // G5
    const mult = 2 ** (Math.min(index, 8) / 12);
    this.tone({ freq: base * mult, type: 'sine', attack: 0.003, decay: 0.1, gain: 0.16 });
  }
```

- [ ] **Step 2: Typecheck**

Run: `npm run typecheck`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/core/Sfx.ts
git commit -m "Add Sfx.bufferTick cue for the buffer-refill sequence"
```

---

### Task 3: `AimController` buffer-count pop animation

**Files:**
- Modify: `src/zoneA/AimController.ts:87-90` (the `setBallsLeft` method)

**Interfaces:**
- Consumes: `Phaser.GameObjects.Text` (`this.countText`, already a field), `this.scene`
  (already a field).
- Produces: no signature change to `setBallsLeft(count: number): void` — same call sites in
  `ZoneASystem` keep working unmodified.

- [ ] **Step 1: Replace `setBallsLeft`**

In `src/zoneA/AimController.ts`, replace:

```ts
  /** Update the balls-left-to-drop count in the queue row (revealed on first call). */
  setBallsLeft(count: number): void {
    this.countText.setText(`${count} left`).setVisible(true);
  }
```

with:

```ts
  /**
   * Update the balls-left-to-drop count in the queue row (revealed on first call). Pops the
   * number briefly on every change — called once per tick during a score-bar cash-in refill,
   * this is what makes the buffer visibly "arrive" one slot at a time instead of snapping.
   */
  setBallsLeft(count: number): void {
    this.countText.setText(`${count} left`).setVisible(true);
    this.scene.tweens.killTweensOf(this.countText);
    this.countText.setScale(1.35);
    this.scene.tweens.add({
      targets: this.countText,
      scale: 1,
      duration: 160,
      ease: 'Back.easeOut',
    });
  }
```

- [ ] **Step 2: Typecheck**

Run: `npm run typecheck`
Expected: no errors.

- [ ] **Step 3: Manual verification**

Run: `npm run dev`, open `http://localhost:5173/?debug=2`, drop a few balls with SPACE/DROP
until the ball buffer count changes (any normal drop already calls `setBallsLeft` once).
Confirm the queue-row number pops (scales up then eases back) instead of just re-rendering
flat. This is a cosmetic-only change with no gameplay effect, safe to verify visually before
Task 5 wires it into the ticked refill.

- [ ] **Step 4: Commit**

```bash
git add src/zoneA/AimController.ts
git commit -m "Pop the queue-row buffer count on every change"
```

---

### Task 4: `ZoneBSystem` dwell → drain-out → cash-in sequencing

**Files:**
- Modify: `src/zoneB/ZoneBSystem.ts` (constants near line 17-19, `addScore` at line
  135-145, `updateBarVisual` at line 181-189, plus two new private methods and one new
  field)

**Interfaces:**
- Consumes: `ScoreBar.add()` / `ScoreBar.completeCashIn()` / `ScoreBar.isCashingIn()` from
  Task 1 (already implemented). `Sfx.goal()` (existing, unchanged).
- Produces: `GameEvent.ScoreBarFilled` now fires after a ~1000ms dwell+drain sequence
  instead of synchronously inside `addScore` — Task 5 depends on this timing, not the event
  shape (which is unchanged: no payload).

- [ ] **Step 1: Add the new timing constants**

In `src/zoneB/ZoneBSystem.ts`, replace the existing constants block:

```ts
const BAR_HEIGHT = 10;
const BAR_COLOR_BG = 0xe0d2b8; // a groove pressed into the paper (between paper and pine)
const BAR_COLOR_FILL = 0xc9973f; // Theme.brass — the bar fills with brass
```

with:

```ts
const BAR_HEIGHT = 10;
const BAR_COLOR_BG = 0xe0d2b8; // a groove pressed into the paper (between paper and pine)
const BAR_COLOR_FILL = 0xc9973f; // Theme.brass — the bar fills with brass

/** How long the bar sits pinned full before it starts draining out. Tune by playtest. */
const CASH_IN_DWELL_MS = 600;
/** How long the drain-out tween (full -> 0) takes. Tune by playtest. */
const CASH_IN_DRAIN_MS = 400;
```

- [ ] **Step 2: Add the `draining` field**

Replace:

```ts
  private inFlight = 0;
  private total = 0;

  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;
```

with:

```ts
  private inFlight = 0;
  private total = 0;
  /** True only during the drain-out tween — see beginDrainOut(). Suppresses the normal
   *  fill-width recompute in updateBarVisual() so the tween's own width writes aren't
   *  immediately overwritten by a same-tick emitScoreBar() (e.g. from a ProgressionChanged
   *  listener firing inside the ScoreBarFilled emit below). */
  private draining = false;

  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;
```

- [ ] **Step 3: Replace `addScore` and add the two sequencing methods**

Replace:

```ts
  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });

    const filled = this.scoreBar.add(points);
    this.emitScoreBar();
    if (filled) {
      this.bus.emit(GameEvent.ScoreBarFilled);
      Sfx.goal();
    }
  }
```

with:

```ts
  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });

    const filled = this.scoreBar.add(points);
    this.emitScoreBar();
    if (filled) this.beginCashIn();
  }

  /**
   * The bar just crossed its target. Play the reward cue immediately, then hold the bar
   * full for a dwell beat before draining it out — the buffer refill (ScoreBarFilled) only
   * fires once the drain-out visual finishes, so filling the bar reads as an event instead
   * of an instant, invisible jump.
   */
  private beginCashIn(): void {
    Sfx.goal();
    this.scene?.time.delayedCall(CASH_IN_DWELL_MS, () => this.beginDrainOut());
  }

  /** Tween the bar's fill width from full to 0, then resolve the cash-in and hand off to Zone A. */
  private beginDrainOut(): void {
    if (!this.scene || !this.barFill) return;
    this.draining = true;
    const proxy = { w: Layout.zoneB.width };
    this.scene.tweens.add({
      targets: proxy,
      w: 0,
      duration: CASH_IN_DRAIN_MS,
      ease: 'Cubic.easeIn',
      onUpdate: () => {
        if (this.barFill) this.barFill.width = proxy.w;
      },
      onComplete: () => {
        this.draining = false;
        // Emit first: Zone A's ScoreBarFilled handler bumps the level and (synchronously,
        // via the ProgressionChanged listener below) may update this.scoreBar's target
        // before we resolve the cash-in — so a cascade check compares the banked overflow
        // against the new target, not the one that was current when the bar filled.
        this.bus.emit(GameEvent.ScoreBarFilled);
        const cascade = this.scoreBar.completeCashIn();
        this.emitScoreBar();
        if (cascade) this.beginCashIn();
      },
    });
  }
```

- [ ] **Step 4: Guard `updateBarVisual` against the drain tween**

Replace:

```ts
  private updateBarVisual(): void {
    if (!this.barFill) return;
    const { x, width } = Layout.zoneB;
    this.barFill.width = width * this.scoreBar.getProgress();
    this.barFill.x = x;
    this.barLabel?.setText(
      `${this.scoreBar.getFilled()} / ${this.scoreBar.getTarget()}`,
    );
  }
```

with:

```ts
  private updateBarVisual(): void {
    if (!this.barFill || this.draining) return;
    const { x, width } = Layout.zoneB;
    this.barFill.width = width * Math.min(1, this.scoreBar.getProgress());
    this.barFill.x = x;
    this.barLabel?.setText(
      `${this.scoreBar.getFilled()} / ${this.scoreBar.getTarget()}`,
    );
  }
```

(The `Math.min(1, …)` clamp is needed independent of the drain tween: while `cashingIn` is
true, `getFilled()` can sit above `getTarget()`, and without the clamp the fill rectangle
would render wider than the bar's track.)

- [ ] **Step 5: Typecheck**

Run: `npm run typecheck`
Expected: no errors.

- [ ] **Step 6: Run the full test suite**

Run: `npm run test`
Expected: PASS — no existing test exercises `ZoneBSystem` directly (it's Phaser-dependent,
not unit-tested), so this step confirms nothing else broke.

- [ ] **Step 7: Manual verification**

Run: `npm run dev`, open `http://localhost:5173/?debug=2`. Mash SPACE/DROP to drain enough
value into the collector to cross the level-1 score-bar target (4 points — one drop of any
tier already exceeds it). Confirm: the bar visually holds full for a beat, then visibly
drains left-to-right back toward empty, and only after that does the queue-row buffer count
start changing (Task 5 will make it tick — until Task 5 lands, expect it to still jump in
one step, since `ZoneASystem` hasn't been updated yet). Also confirm dropping more balls
during the dwell/drain window doesn't visually distort the bar (no double-width, no
negative width).

- [ ] **Step 8: Commit**

```bash
git add src/zoneB/ZoneBSystem.ts
git commit -m "Sequence score-bar fill as dwell -> drain-out before refilling the buffer"
```

---

### Task 5: `ZoneASystem` ticked buffer refill

**Files:**
- Modify: `src/zoneA/ZoneASystem.ts` (constants near line 17-36, `create()` at line 55-131,
  `applyStage` at line 219-223, `onBallDropped`/`emitBuffer` are unchanged but referenced)

**Interfaces:**
- Consumes: `Sfx.bufferTick(index)` from Task 2, `AimController.setBallsLeft` (already
  pop-animated by Task 3, called via the existing `emitBuffer()` — no new call needed),
  `AimController.setDropLocked(locked: boolean)` (existing).
- Produces: no contract/event shape changes. `BallBufferChanged` now fires once per tick
  during a refill instead of once total — any other future listener must tolerate that.

- [ ] **Step 1: Add the tick-cadence constant**

In `src/zoneA/ZoneASystem.ts`, replace:

```ts
/**
 * A stalemate must persist this long before the run actually ends. Balls hand off between
 * zones through transient empty states, so we confirm the stalemate after a short grace
 * and only end if it still holds. A real stalemate persists; a transient resolves.
 */
const STALEMATE_GRACE_MS = 250;
```

with:

```ts
/**
 * A stalemate must persist this long before the run actually ends. Balls hand off between
 * zones through transient empty states, so we confirm the stalemate after a short grace
 * and only end if it still holds. A real stalemate persists; a transient resolves.
 */
const STALEMATE_GRACE_MS = 250;

/** Ball-buffer refill cadence during a score-bar cash-in: one slot every this many ms. */
const BUFFER_TICK_MS = 130;
```

- [ ] **Step 2: Stop `applyStage` from setting the buffer directly**

Replace:

```ts
  private applyStage(stage: ProgressionStage, queue: BallQueue): void {
    queue.setWindow(stage.ballWindow[0], stage.ballWindow[1]);
    if (stage.bufferBalls) queue.seed(stage.bufferBalls);
    this.ballBuffer = stage.bufferCapacity;
  }
```

with:

```ts
  private applyStage(stage: ProgressionStage, queue: BallQueue): void {
    queue.setWindow(stage.ballWindow[0], stage.ballWindow[1]);
    if (stage.bufferBalls) queue.seed(stage.bufferBalls);
  }
```

- [ ] **Step 3: Set the buffer directly on initial boot (no animation on game start)**

In `create()`, replace:

```ts
    const queue = new BallQueue();
    this.queue = queue;
    const initialStage = getStage(this.internalLevel);
    this.applyStage(initialStage, queue);
```

with:

```ts
    const queue = new BallQueue();
    this.queue = queue;
    const initialStage = getStage(this.internalLevel);
    this.applyStage(initialStage, queue);
    this.ballBuffer = initialStage.bufferCapacity;
```

- [ ] **Step 4: Wire the ticked refill + safe unlock into the `ScoreBarFilled` handler**

In `create()`, replace the `ScoreBarFilled` handler:

```ts
    this.bus.on(GameEvent.ScoreBarFilled, () => {
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      // applyStage may have re-seeded the queue's current/next (stages with bufferBalls),
      // so re-sync the in-hand ball + Next preview — otherwise the player aims one tier
      // and drops another. The milestone path below re-rolls and refreshes again on top.
      this.aim?.refreshQueue();
      this.emitBuffer();
      this.bus.emit(GameEvent.ProgressionChanged, {
        level: this.internalLevel,
        minTier: stage.ballWindow[0],
        maxTier: stage.ballWindow[1],
        bufferCapacity: stage.bufferCapacity,
        scoreBarTarget: stage.scoreBarTarget,
      });
      // Every MILESTONE_EVERY levels the draw window jumps up, blacklisting the lowest
      // tiers, and the arena zooms out (input frozen during the tween) by the neutral
      // ball-growth match × the stage's authored tightness — so apparent ball size holds
      // constant at tightness 1 and the arena-to-ball headroom is exactly the tightness
      // rhythm authored in progression.json. Past the last authored window shift the stage
      // stops moving, so milestones become plain levels (no growth — the tail self-heals).
      // Otherwise just lift the buffer-empty drop lock as before.
      const prev = getStage(this.internalLevel - 1);
      const shifted =
        stage.ballWindow[0] !== prev.ballWindow[0] || stage.ballWindow[1] !== prev.ballWindow[1];
      if (this.internalLevel % MILESTONE_EVERY === 0 && shifted) {
        const factor =
          neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
        this.beginMilestoneZoom(factor, stage.ballWindow[0]);
      } else {
        this.aim?.setDropLocked(false);
      }
    });
```

with:

```ts
    this.bus.on(GameEvent.ScoreBarFilled, () => {
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      // applyStage may have re-seeded the queue's current/next (stages with bufferBalls),
      // so re-sync the in-hand ball + Next preview — otherwise the player aims one tier
      // and drops another. The milestone path below re-rolls and refreshes again on top.
      this.aim?.refreshQueue();
      this.animateBufferTo(stage.bufferCapacity);
      this.bus.emit(GameEvent.ProgressionChanged, {
        level: this.internalLevel,
        minTier: stage.ballWindow[0],
        maxTier: stage.ballWindow[1],
        bufferCapacity: stage.bufferCapacity,
        scoreBarTarget: stage.scoreBarTarget,
      });
      // Every MILESTONE_EVERY levels the draw window jumps up, blacklisting the lowest
      // tiers, and the arena zooms out (input frozen during the tween) by the neutral
      // ball-growth match × the stage's authored tightness — so apparent ball size holds
      // constant at tightness 1 and the arena-to-ball headroom is exactly the tightness
      // rhythm authored in progression.json. Past the last authored window shift the stage
      // stops moving, so milestones become plain levels (no growth — the tail self-heals).
      // Otherwise just lift the buffer-empty drop lock as before (guarded: the ticked
      // buffer refill above may not have delivered its first slot yet).
      const prev = getStage(this.internalLevel - 1);
      const shifted =
        stage.ballWindow[0] !== prev.ballWindow[0] || stage.ballWindow[1] !== prev.ballWindow[1];
      if (this.internalLevel % MILESTONE_EVERY === 0 && shifted) {
        const factor =
          neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
        this.beginMilestoneZoom(factor, stage.ballWindow[0]);
      } else {
        this.maybeUnlockDrop();
      }
    });
```

- [ ] **Step 5: Guard the milestone-zoom completion's unlock the same way**

In `beginMilestoneZoom`, replace:

```ts
    this.arena?.grow(factor, () => {
      this.drainBlacklisted(newMinTier, () => {
        this.aim?.setFrozen(false);
        this.aim?.setDropLocked(false);
        this.bus.emit(GameEvent.ArenaZoom, { active: false });
      });
    });
```

with:

```ts
    this.arena?.grow(factor, () => {
      this.drainBlacklisted(newMinTier, () => {
        this.aim?.setFrozen(false);
        this.maybeUnlockDrop();
        this.bus.emit(GameEvent.ArenaZoom, { active: false });
      });
    });
```

- [ ] **Step 6: Add `animateBufferTo` and `maybeUnlockDrop`**

Directly after the `applyStage` method (now ending after the two-line body from Step 2),
add:

```ts
  /**
   * Refill the ball buffer to `newCapacity`, one slot at a time, so the HUD's queue-row
   * count visibly ticks up instead of instantly jumping — this is what makes filling the
   * score bar read as a reward. Drop unlocks the moment the first tick lands, via
   * maybeUnlockDrop(). If the buffer is already at or above the new capacity (shouldn't
   * normally happen, since capacity is non-decreasing across stages), applies it in one
   * step instead.
   */
  private animateBufferTo(newCapacity: number): void {
    if (newCapacity <= this.ballBuffer) {
      this.ballBuffer = newCapacity;
      this.emitBuffer();
      this.maybeUnlockDrop();
      return;
    }
    const ticksNeeded = newCapacity - this.ballBuffer;
    let ticksDone = 0;
    this.scene?.time.addEvent({
      delay: BUFFER_TICK_MS,
      repeat: ticksNeeded - 1,
      callback: () => {
        this.ballBuffer += 1;
        this.emitBuffer();
        Sfx.bufferTick(ticksDone);
        ticksDone += 1;
        this.maybeUnlockDrop();
      },
    });
  }

  /** Unlock dropping only once the buffer actually has a slot — safe to call from both the
   *  ticked refill and the milestone-zoom completion regardless of which finishes first. */
  private maybeUnlockDrop(): void {
    if (this.ballBuffer > 0) this.aim?.setDropLocked(false);
  }
```

- [ ] **Step 7: Typecheck**

Run: `npm run typecheck`
Expected: no errors.

- [ ] **Step 8: Run the full test suite**

Run: `npm run test`
Expected: PASS.

- [ ] **Step 9: Manual verification**

Run: `npm run dev`, open `http://localhost:5173/?debug=2`. Mash SPACE/DROP to cross the
score-bar target. Confirm the full sequence now plays end-to-end: bar dwells full → drains
out → queue-row buffer count ticks up one at a time with a pop and an ascending blip per
tick → dropping becomes available again the moment the first tick lands (try dropping
immediately after the drain-out finishes; it should work even before all ticks land).

Also verify a milestone level-up (would require reaching level 50 in a real run — instead,
confirm by code inspection that `beginMilestoneZoom`'s completion callback now calls
`maybeUnlockDrop()` and would correctly no-op if `ballBuffer` were still 0 at that point).

- [ ] **Step 10: Build**

Run: `npm run build`
Expected: succeeds (typecheck + production bundle).

- [ ] **Step 11: Commit**

```bash
git add src/zoneA/ZoneASystem.ts
git commit -m "Tick the ball buffer up one slot at a time on score-bar refill"
```

---

## Self-review notes

- **Spec coverage:** dwell (Task 4 `CASH_IN_DWELL_MS`), drain-out tween (Task 4
  `beginDrainOut`), banked overflow during cash-in (Task 1), ticked buffer arrival with pop
  + sfx (Tasks 2, 3, 5), cascade edge case (Task 1 `completeCashIn` + Task 4's
  `if (cascade) this.beginCashIn()`), skip case for non-increasing capacity (Task 5
  `animateBufferTo`'s early-return branch), no contract changes (verified — no task touches
  `contracts.ts`), no `scoreBarTarget` rebalancing (verified — no task touches
  `progression.json`). All spec sections are covered.
- **Type consistency:** `ScoreBar.add`/`getFilled`/`getTarget`/`getProgress`/`setTarget`
  signatures are unchanged from what `ZoneBSystem` already calls; only `completeCashIn` and
  `isCashingIn` are new and both are introduced and consumed within this plan.
  `AimController.setBallsLeft(count: number): void` keeps its exact signature. `Sfx.bufferTick
  (index: number): void` is introduced in Task 2 and consumed with a matching signature in
  Task 5.
