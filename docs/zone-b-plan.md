# Zone B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Zone B — the ball-split physics arena — with three gate types (static, translating, rotating), flexible collector placement, physical walls, and a ball buffer / score-milestone losing condition.

**Architecture:** `ZoneBSystem` orchestrates four sub-systems: `GateSystem` (gates + split logic), `CollectorSystem` (score sensors), `WallSystem` (barriers), and `BallBuffer` (pure milestone logic). All physics bodies are produced by `ZoneBBall` helpers. `ZoneBSystem` is the sole emitter of contract events; sub-systems communicate back via callbacks. Pending splits and drains are **queued** during Matter collision callbacks and resolved at the start of `update()` — never inside the callback itself.

**Tech Stack:** Phaser 4 · TypeScript (strict) · Matter.js (via Phaser) · Vitest

## Global Constraints

- No changes to `src/zoneA/` files
- `src/core/contracts.ts` — additive only; no renames, no changes to existing event payloads
- Zone B files never import from `src/zoneA/` or `src/zoneC/`
- Procedural textures only — no external asset files
- Portrait 390 × 844; Zone B rect: `{ x:0, y:492, width:390, height:352 }` (`Layout.zoneB`)
- Entry point: `Layout.zoneBEntry = { x:195, y:492 }`
- Ball radius in Zone B: **14 px**; gate thickness: **8 px**
- TypeScript strict — no `any`, no `!` non-null assertions without a comment explaining why
- Physics mutations (split / drain) must be **queued during collision callbacks**, flushed in `update()`

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `src/zoneB/BallBuffer.ts` | **Create** | Pure logic: ball count + milestone tracking |
| `src/zoneB/BallBuffer.test.ts` | **Create** | Vitest unit tests for BallBuffer |
| `src/zoneB/zoneLayout.ts` | **Create** | Type definitions (GateDef, CollectorDef, WallDef) + initial layout data |
| `src/zoneB/ZoneBBall.ts` | **Create** | Create / destroy Zone B Matter balls; read `ballData` off a body |
| `src/zoneB/GateSystem.ts` | **Replace** | Three gate types: bodies, motion, split-queue |
| `src/zoneB/CollectorSystem.ts` | **Create** | Collector sensor bodies + drain queue (replaces `Funnel.ts` logic) |
| `src/zoneB/WallSystem.ts` | **Create** | Static wall segment bodies |
| `src/zoneB/ZoneBSystem.ts` | **Modify** | Orchestration: spawn, replaceBall, drain, buffer, all contract events, game-over overlay |
| `src/core/contracts.ts` | **Modify** | Add `BUFFER_CHANGED` + `BUFFER_EXHAUSTED` events and payloads |
| `src/core/HUD.ts` | **Modify** | Show ball-buffer count alongside the score |

> `src/zoneB/Funnel.ts` is left in place as an empty skeleton — its import in `ZoneBSystem` is removed in Task 6.

---

### Task 1: BallBuffer — pure logic

**Files:**
- Create: `src/zoneB/BallBuffer.ts`
- Create: `src/zoneB/BallBuffer.test.ts`

**Interfaces produced:**
```typescript
class BallBuffer {
  constructor(
    initialCount?: number,   // default 20
    refillAmount?: number,   // default 10
    initialMilestone?: number, // default 50
    milestoneMultiplier?: number // default 2.5
  )
  spend(): boolean            // decrement; false if already 0
  refillIfMilestone(total: number): boolean  // refill + advance if total >= nextMilestone
  getCount(): number
  getNextMilestone(): number
  isExhausted(): boolean      // count === 0
}
export const BUFFER_INITIAL_COUNT = 20;
export const BUFFER_REFILL_AMOUNT = 10;
export const BUFFER_INITIAL_MILESTONE = 50;
export const BUFFER_MILESTONE_MULTIPLIER = 2.5;
```

- [ ] **Step 1: Write failing tests**

`src/zoneB/BallBuffer.test.ts`:
```typescript
import { describe, test, expect } from 'vitest';
import { BallBuffer } from './BallBuffer';

describe('BallBuffer', () => {
  test('starts with initial count', () => {
    expect(new BallBuffer(20, 10, 50, 2.5).getCount()).toBe(20);
  });

  test('spend reduces count by 1 and returns true', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    expect(buf.spend()).toBe(true);
    expect(buf.getCount()).toBe(19);
  });

  test('spend returns false when exhausted', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.spend()).toBe(false);
    expect(buf.getCount()).toBe(0);
  });

  test('isExhausted when count reaches 0', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.isExhausted()).toBe(true);
  });

  test('refillIfMilestone returns false below milestone', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    expect(buf.refillIfMilestone(49)).toBe(false);
    expect(buf.getCount()).toBe(20);
  });

  test('refillIfMilestone adds refillAmount when milestone reached', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.refillIfMilestone(50)).toBe(true);
    expect(buf.getCount()).toBe(10);
  });

  test('milestone escalates after refill', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    expect(buf.getNextMilestone()).toBe(125);
  });

  test('does not refill twice for the same milestone', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    expect(buf.refillIfMilestone(50)).toBe(false);
  });

  test('milestone escalates again on second refill', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    buf.refillIfMilestone(125);
    expect(buf.getNextMilestone()).toBe(313);
  });
});
```

- [ ] **Step 2: Run tests — expect failure**

```bash
npm run test -- BallBuffer
```
Expected: 9 failures with "Cannot find module './BallBuffer'"

- [ ] **Step 3: Implement BallBuffer**

`src/zoneB/BallBuffer.ts`:
```typescript
export const BUFFER_INITIAL_COUNT = 20;
export const BUFFER_REFILL_AMOUNT = 10;
export const BUFFER_INITIAL_MILESTONE = 50;
export const BUFFER_MILESTONE_MULTIPLIER = 2.5;

export class BallBuffer {
  private count: number;
  private nextMilestone: number;

  constructor(
    private readonly initialCount = BUFFER_INITIAL_COUNT,
    private readonly refillAmount = BUFFER_REFILL_AMOUNT,
    initialMilestone = BUFFER_INITIAL_MILESTONE,
    private readonly milestoneMultiplier = BUFFER_MILESTONE_MULTIPLIER,
  ) {
    this.count = initialCount;
    this.nextMilestone = initialMilestone;
  }

  spend(): boolean {
    if (this.count <= 0) return false;
    this.count -= 1;
    return true;
  }

  refillIfMilestone(total: number): boolean {
    if (total < this.nextMilestone) return false;
    this.count += this.refillAmount;
    this.nextMilestone = Math.round(this.nextMilestone * this.milestoneMultiplier);
    return true;
  }

  getCount(): number { return this.count; }
  getNextMilestone(): number { return this.nextMilestone; }
  isExhausted(): boolean { return this.count === 0; }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
npm run test -- BallBuffer
```
Expected: 9 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/zoneB/BallBuffer.ts src/zoneB/BallBuffer.test.ts
git commit -m "feat(zoneB): BallBuffer pure logic with milestone tracking"
```

---

### Task 2: Extend contracts.ts with buffer events

**Files:**
- Modify: `src/core/contracts.ts`

**Interfaces produced:**
```typescript
GameEvent.BufferChanged  // 'BUFFER_CHANGED'
GameEvent.BufferExhausted  // 'BUFFER_EXHAUSTED'
interface BufferChangedPayload { count: number; nextMilestone: number }
```

- [ ] **Step 1: Add events and payload to contracts.ts**

Extend the `GameEvent` object (keep all existing entries):
```typescript
export const GameEvent = {
  BallDropped:      'BALL_DROPPED',
  ZoneBBusy:        'ZONE_B_BUSY',
  ZoneBEmpty:       'ZONE_B_EMPTY',
  ScoreChanged:     'SCORE_CHANGED',
  /** Zone B → HUD: buffer count or next-milestone changed. */
  BufferChanged:    'BUFFER_CHANGED',
  /** Zone B → scene: buffer exhausted + Zone B empty = game over. */
  BufferExhausted:  'BUFFER_EXHAUSTED',
} as const;
```

Add after `ScoreChangedPayload`:
```typescript
export interface BufferChangedPayload {
  count: number;
  nextMilestone: number;
}
```

Extend `GameEventMap`:
```typescript
export interface GameEventMap {
  [GameEvent.BallDropped]:     BallDroppedPayload;
  [GameEvent.ZoneBBusy]:       void;
  [GameEvent.ZoneBEmpty]:      void;
  [GameEvent.ScoreChanged]:    ScoreChangedPayload;
  [GameEvent.BufferChanged]:   BufferChangedPayload;
  [GameEvent.BufferExhausted]: void;
}
```

- [ ] **Step 2: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new TypeScript errors

- [ ] **Step 3: Commit**

```bash
git add src/core/contracts.ts
git commit -m "feat(contracts): add BUFFER_CHANGED and BUFFER_EXHAUSTED events"
```

---

### Task 3: Layout types + ZoneBBall helper

**Files:**
- Create: `src/zoneB/zoneLayout.ts`
- Create: `src/zoneB/ZoneBBall.ts`

**Interfaces produced:**

From `zoneLayout.ts`:
```typescript
interface StaticGate   { type: 'static';      cx: number; cy: number; angle: number; length: number; multiplier: number }
interface TranslatingGate { type: 'translating'; ax: number; ay: number; bx: number; by: number; angle: number; length: number; multiplier: number; periodMs: number }
interface RotatingGate { type: 'rotating';    cx: number; cy: number; length: number; multiplier: number; speedRadPerMs: number }
type GateDef = StaticGate | TranslatingGate | RotatingGate;

interface CollectorDef { x: number; y: number; width: number; height: number; scoreMultiplier: number }
interface WallDef      { x1: number; y1: number; x2: number; y2: number; thickness?: number }

interface ZoneBLayout  { gates: GateDef[]; collectors: CollectorDef[]; walls: WallDef[] }
export const INITIAL_LAYOUT: ZoneBLayout;
```

From `ZoneBBall.ts`:
```typescript
export const BALL_RADIUS = 14;
// Collision filter categories
export const CAT_BALL = 0x0001;
export const CAT_GATE = 0x0002;
export const CAT_WALL = 0x0004;
export const CAT_COLLECTOR = 0x0008;

export function createZoneBBall(scene: Phaser.Scene, x: number, y: number, value: number, tier: number): Phaser.Physics.Matter.Image
export function destroyZoneBBall(img: Phaser.Physics.Matter.Image): void
export function getBallData(body: MatterJS.BodyType): BallSpec | null
```

- [ ] **Step 1: Create zoneLayout.ts**

Zone B is 390 × 352 starting at y = 492. All coordinates below are **absolute screen coordinates**.

`src/zoneB/zoneLayout.ts`:
```typescript
export interface StaticGate {
  type: 'static';
  cx: number; cy: number;
  angle: number;   // radians; 0 = horizontal
  length: number;
  multiplier: number;
}
export interface TranslatingGate {
  type: 'translating';
  ax: number; ay: number;
  bx: number; by: number;
  angle: number;
  length: number;
  multiplier: number;
  periodMs: number;  // ms for one full A→B→A cycle
}
export interface RotatingGate {
  type: 'rotating';
  cx: number; cy: number;
  length: number;
  multiplier: number;
  speedRadPerMs: number;  // positive = clockwise
}
export type GateDef = StaticGate | TranslatingGate | RotatingGate;

export interface CollectorDef {
  x: number; y: number;
  width: number; height: number;
  scoreMultiplier: number;  // ball.value × scoreMultiplier added to score
}
export interface WallDef {
  x1: number; y1: number;
  x2: number; y2: number;
  thickness?: number;   // default 6
}
export interface ZoneBLayout {
  gates: GateDef[];
  collectors: CollectorDef[];
  walls: WallDef[];
}

// Initial layout. All y values are absolute (zone B starts at y=492).
export const INITIAL_LAYOUT: ZoneBLayout = {
  gates: [
    // Static, slightly tilted, centre of arena
    { type: 'static', cx: 195, cy: 580, angle: 0.15, length: 90, multiplier: 2 },
    // Translating gate — slides left↔right across the left half
    { type: 'translating', ax: 70, ay: 660, bx: 180, by: 660, angle: 0, length: 70, multiplier: 2, periodMs: 2200 },
    // Rotating gate — spins around a fixed pivot on the right side
    { type: 'rotating', cx: 310, cy: 720, length: 65, multiplier: 3, speedRadPerMs: 0.0025 },
  ],
  collectors: [
    { x: 10,  y: 820, width: 100, height: 20, scoreMultiplier: 1 },
    { x: 145, y: 820, width: 100, height: 20, scoreMultiplier: 2 },
    { x: 280, y: 820, width: 100, height: 20, scoreMultiplier: 1 },
  ],
  walls: [
    // Diagonal ramps funnelling balls toward the collectors
    { x1: 0,   y1: 790, x2: 110, y2: 820 },
    { x1: 110, y1: 820, x2: 145, y2: 820 },
    { x1: 245, y1: 820, x2: 280, y2: 820 },
    { x1: 380, y1: 790, x2: 280, y2: 820 },
  ],
};
```

- [ ] **Step 2: Create ZoneBBall.ts**

`src/zoneB/ZoneBBall.ts`:
```typescript
import type Phaser from 'phaser';
import type MatterJS from 'matter-js';
import type { BallSpec } from '../core/contracts';

export const BALL_RADIUS = 14;

export const CAT_BALL      = 0x0001;
export const CAT_GATE      = 0x0002;
export const CAT_WALL      = 0x0004;
export const CAT_COLLECTOR = 0x0008;

// Freshly-split balls ignore gates for 300 ms so they don't immediately re-trigger.
const SPLIT_GRACE_MS = 300;

const TIER_HUES = [200, 160, 120, 80, 40, 20, 0, 300, 260, 220] as const;

export function createZoneBBall(
  scene: Phaser.Scene,
  x: number,
  y: number,
  value: number,
  tier: number,
  fromSplit = false,
): Phaser.Physics.Matter.Image {
  const key = `zb-ball-t${tier}`;
  if (!scene.textures.exists(key)) {
    const hue = (TIER_HUES[(tier - 1) % TIER_HUES.length] ?? 200) / 360;
    const color = Phaser.Display.Color.HSLToColor(hue, 0.7, 0.55).color;
    const g = scene.make.graphics({ x: 0, y: 0, add: false });
    g.fillStyle(color, 1);
    g.fillCircle(BALL_RADIUS, BALL_RADIUS, BALL_RADIUS);
    g.generateTexture(key, BALL_RADIUS * 2, BALL_RADIUS * 2);
    g.destroy();
  }

  const collisionFilter = fromSplit
    ? { category: CAT_BALL, mask: CAT_WALL | CAT_COLLECTOR }          // skip gates during grace
    : { category: CAT_BALL, mask: CAT_GATE | CAT_WALL | CAT_COLLECTOR };

  const img = scene.matter.add.image(x, y, key, undefined, {
    shape: { type: 'circle', radius: BALL_RADIUS },
    restitution: 0.35,
    friction: 0.05,
    frictionAir: 0.008,
    label: 'zoneB-ball',
    collisionFilter,
  });

  const mb = img.body as MatterJS.BodyType;
  (mb as unknown as { ballData: BallSpec }).ballData = { value, tier };

  if (fromSplit) {
    scene.time.delayedCall(SPLIT_GRACE_MS, () => {
      if (!img.active) return;
      Phaser.Physics.Matter.Matter.Body.set(mb, 'collisionFilter', {
        category: CAT_BALL,
        mask: CAT_GATE | CAT_WALL | CAT_COLLECTOR,
      });
    });
  }

  return img;
}

export function destroyZoneBBall(img: Phaser.Physics.Matter.Image): void {
  img.destroy(); // Phaser removes the Matter body automatically
}

export function getBallData(body: MatterJS.BodyType): BallSpec | null {
  return (body as unknown as { ballData?: BallSpec }).ballData ?? null;
}
```

- [ ] **Step 3: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new errors

- [ ] **Step 4: Commit**

```bash
git add src/zoneB/zoneLayout.ts src/zoneB/ZoneBBall.ts
git commit -m "feat(zoneB): layout types, initial layout data, and ZoneBBall helper"
```

---

### Task 4: GateSystem — three gate types with split queuing

**Files:**
- Modify (replace): `src/zoneB/GateSystem.ts`

**Interfaces consumed:**
- `GateDef`, `INITIAL_LAYOUT` from `./zoneLayout`
- `getBallData`, `createZoneBBall`, `destroyZoneBBall`, `CAT_GATE`, `CAT_BALL`, `BALL_RADIUS` from `./ZoneBBall`

**Interfaces produced:**
```typescript
interface GateCallbacks {
  onSplit(img: Phaser.Physics.Matter.Image, multiplier: number): void;
}
class GateSystem implements GameSystem {
  constructor(layout: GateDef[], callbacks: GateCallbacks)
  create(scene: Phaser.Scene): void
  update(time: number, delta: number): void
}
```

- [ ] **Step 1: Replace GateSystem.ts**

`src/zoneB/GateSystem.ts`:
```typescript
import type Phaser from 'phaser';
import type MatterJS from 'matter-js';
import type { GameSystem } from '../core/contracts';
import type { GateDef, TranslatingGate, RotatingGate } from './zoneLayout';
import { getBallData, createZoneBBall, destroyZoneBBall, CAT_GATE, CAT_BALL, BALL_RADIUS } from './ZoneBBall';

export interface GateCallbacks {
  /** Called when a ball body hits a gate; system should handle inFlight bookkeeping + spawning copies. */
  onSplit(img: Phaser.Physics.Matter.Image, multiplier: number): void;
}

interface RuntimeGate {
  def: GateDef;
  body: MatterJS.BodyType;
  /** Elapsed ms used for oscillation. */
  elapsed: number;
}

const GATE_THICKNESS = 8;

export class GateSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly gates: RuntimeGate[] = [];
  private readonly pending: Array<{ img: Phaser.Physics.Matter.Image; multiplier: number }> = [];

  constructor(
    private readonly layout: GateDef[],
    private readonly callbacks: GateCallbacks,
  ) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;

    for (const def of this.layout) {
      const body = this.buildBody(scene, def);
      this.gates.push({ def, body, elapsed: 0 });
    }

    scene.matter.world.on(
      Phaser.Physics.Matter.Events.COLLISION_START,
      (event: Phaser.Physics.Matter.Events.CollisionStartEvent) => {
        for (const pair of event.pairs) {
          this.checkSplit(pair.bodyA, pair.bodyB);
          this.checkSplit(pair.bodyB, pair.bodyA);
        }
      },
    );
  }

  update(_time: number, delta: number): void {
    for (const gate of this.gates) {
      gate.elapsed += delta;
      this.applyMotion(gate);
    }

    const splits = this.pending.splice(0);
    for (const { img, multiplier } of splits) {
      if (img.active) this.callbacks.onSplit(img, multiplier);
    }
  }

  private buildBody(scene: Phaser.Scene, def: GateDef): MatterJS.BodyType {
    const opts: Phaser.Types.Physics.Matter.MatterBodyConfig = {
      isStatic: true,
      isSensor: false,
      label: 'gate',
      collisionFilter: { category: CAT_GATE, mask: CAT_BALL },
      friction: 0,
      restitution: 0.4,
    };

    let cx: number, cy: number, angle: number;
    if (def.type === 'static') {
      ({ cx, cy, angle } = def);
    } else if (def.type === 'translating') {
      cx = def.ax; cy = def.ay; angle = def.angle;
    } else {
      cx = def.cx; cy = def.cy; angle = 0;
    }

    const rect = scene.matter.add.rectangle(cx, cy, def.length, GATE_THICKNESS, opts);
    Phaser.Physics.Matter.Matter.Body.setAngle(rect, angle);

    // Visual: a white/grey bar with "×N" label
    const g = scene.add.rectangle(cx, cy, def.length, GATE_THICKNESS, 0xccddff).setDepth(5);
    scene.add.text(cx, cy - 10, `×${def.multiplier}`, {
      fontFamily: 'monospace', fontSize: '11px', color: '#ccddff',
    }).setOrigin(0.5).setDepth(6);

    // Track the visual rect alongside the body so we can move it in update()
    (rect as unknown as { gfx: Phaser.GameObjects.Rectangle }).gfx = g;

    return rect;
  }

  private applyMotion(gate: RuntimeGate): void {
    const { def, body, elapsed } = gate;
    if (def.type === 'static') return;

    const gfx = (body as unknown as { gfx: Phaser.GameObjects.Rectangle }).gfx;

    if (def.type === 'translating') {
      const t = (Math.sin((elapsed / (def as TranslatingGate).periodMs) * Math.PI * 2) + 1) / 2;
      const nx = def.ax + (def.bx - def.ax) * t;
      const ny = def.ay + (def.by - def.ay) * t;
      Phaser.Physics.Matter.Matter.Body.setPosition(body, { x: nx, y: ny });
      if (gfx) { gfx.x = nx; gfx.y = ny; }
    } else {
      // rotating
      const rd = def as RotatingGate;
      const angle = elapsed * rd.speedRadPerMs;
      Phaser.Physics.Matter.Matter.Body.setAngle(body, angle);
      if (gfx) { gfx.x = rd.cx; gfx.y = rd.cy; gfx.rotation = angle; }
    }
  }

  private checkSplit(maybeGate: MatterJS.BodyType, maybeBall: MatterJS.BodyType): void {
    if (maybeGate.label !== 'gate') return;
    if (maybeBall.label !== 'zoneB-ball') return;
    const data = getBallData(maybeBall);
    if (!data) return;

    // Find the gate def to get the multiplier.
    const gate = this.gates.find((g) => g.body === maybeGate);
    if (!gate) return;

    const img = (maybeBall as unknown as { gameObject?: Phaser.Physics.Matter.Image }).gameObject;
    if (!img?.active) return;

    this.pending.push({ img, multiplier: gate.def.multiplier });
  }
}
```

- [ ] **Step 2: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add src/zoneB/GateSystem.ts
git commit -m "feat(zoneB): GateSystem with static, translating, and rotating gates"
```

---

### Task 5: CollectorSystem — sensor drain areas

**Files:**
- Create: `src/zoneB/CollectorSystem.ts`

**Interfaces consumed:**
- `CollectorDef`, `INITIAL_LAYOUT` from `./zoneLayout`
- `getBallData`, `CAT_COLLECTOR`, `CAT_BALL` from `./ZoneBBall`

**Interfaces produced:**
```typescript
interface CollectorCallbacks {
  onDrain(img: Phaser.Physics.Matter.Image, value: number, scoreMultiplier: number): void;
}
class CollectorSystem implements GameSystem {
  constructor(layout: CollectorDef[], callbacks: CollectorCallbacks)
  create(scene: Phaser.Scene): void
  update(time: number, delta: number): void
}
```

- [ ] **Step 1: Create CollectorSystem.ts**

`src/zoneB/CollectorSystem.ts`:
```typescript
import type Phaser from 'phaser';
import type MatterJS from 'matter-js';
import type { GameSystem } from '../core/contracts';
import type { CollectorDef } from './zoneLayout';
import { getBallData, CAT_COLLECTOR, CAT_BALL } from './ZoneBBall';

export interface CollectorCallbacks {
  onDrain(img: Phaser.Physics.Matter.Image, value: number, scoreMultiplier: number): void;
}

interface PendingDrain {
  img: Phaser.Physics.Matter.Image;
  value: number;
  scoreMultiplier: number;
}

export class CollectorSystem implements GameSystem {
  private readonly collectorBodies = new Map<MatterJS.BodyType, CollectorDef>();
  private readonly pending: PendingDrain[] = [];

  constructor(
    private readonly layout: CollectorDef[],
    private readonly callbacks: CollectorCallbacks,
  ) {}

  create(scene: Phaser.Scene): void {
    for (const def of this.layout) {
      const cx = def.x + def.width / 2;
      const cy = def.y + def.height / 2;
      const body = scene.matter.add.rectangle(cx, cy, def.width, def.height, {
        isStatic: true,
        isSensor: true,
        label: 'collector',
        collisionFilter: { category: CAT_COLLECTOR, mask: CAT_BALL },
      });
      this.collectorBodies.set(body, def);

      // Visual: a semi-transparent filled rect with ×N label
      scene.add
        .rectangle(cx, cy, def.width, def.height, 0x44ff88, 0.25)
        .setStrokeStyle(1, 0x44ff88)
        .setDepth(4);
      if (def.scoreMultiplier !== 1) {
        scene.add
          .text(cx, cy, `×${def.scoreMultiplier}`, {
            fontFamily: 'monospace', fontSize: '11px', color: '#44ff88',
          })
          .setOrigin(0.5)
          .setDepth(5);
      }
    }

    scene.matter.world.on(
      Phaser.Physics.Matter.Events.COLLISION_START,
      (event: Phaser.Physics.Matter.Events.CollisionStartEvent) => {
        for (const pair of event.pairs) {
          this.checkDrain(pair.bodyA, pair.bodyB);
          this.checkDrain(pair.bodyB, pair.bodyA);
        }
      },
    );
  }

  update(_time: number, _delta: number): void {
    const drains = this.pending.splice(0);
    for (const { img, value, scoreMultiplier } of drains) {
      if (img.active) this.callbacks.onDrain(img, value, scoreMultiplier);
    }
  }

  private checkDrain(maybeCollector: MatterJS.BodyType, maybeBall: MatterJS.BodyType): void {
    const def = this.collectorBodies.get(maybeCollector);
    if (!def) return;
    if (maybeBall.label !== 'zoneB-ball') return;

    const data = getBallData(maybeBall);
    if (!data) return;

    const img = (maybeBall as unknown as { gameObject?: Phaser.Physics.Matter.Image }).gameObject;
    if (!img?.active) return;

    this.pending.push({ img, value: data.value, scoreMultiplier: def.scoreMultiplier });
  }
}
```

- [ ] **Step 2: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add src/zoneB/CollectorSystem.ts
git commit -m "feat(zoneB): CollectorSystem — sensor drain areas with score multipliers"
```

---

### Task 6: WallSystem — static line-segment barriers

**Files:**
- Create: `src/zoneB/WallSystem.ts`

**Interfaces consumed:**
- `WallDef`, `INITIAL_LAYOUT` from `./zoneLayout`
- `CAT_WALL`, `CAT_BALL` from `./ZoneBBall`

**Interfaces produced:**
```typescript
class WallSystem implements GameSystem {
  constructor(layout: WallDef[])
  create(scene: Phaser.Scene): void
  update(time: number, delta: number): void
}
```

- [ ] **Step 1: Create WallSystem.ts**

`src/zoneB/WallSystem.ts`:
```typescript
import type Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import type { WallDef } from './zoneLayout';
import { CAT_WALL, CAT_BALL } from './ZoneBBall';

const DEFAULT_THICKNESS = 6;

export class WallSystem implements GameSystem {
  constructor(private readonly layout: WallDef[]) {}

  create(scene: Phaser.Scene): void {
    for (const wall of this.layout) {
      const thickness = wall.thickness ?? DEFAULT_THICKNESS;
      const dx = wall.x2 - wall.x1;
      const dy = wall.y2 - wall.y1;
      const length = Math.sqrt(dx * dx + dy * dy);
      const cx = (wall.x1 + wall.x2) / 2;
      const cy = (wall.y1 + wall.y2) / 2;
      const angle = Math.atan2(dy, dx);

      const body = scene.matter.add.rectangle(cx, cy, length, thickness, {
        isStatic: true,
        isSensor: false,
        label: 'zoneB-wall',
        collisionFilter: { category: CAT_WALL, mask: CAT_BALL },
        friction: 0.1,
        restitution: 0.3,
      });
      Phaser.Physics.Matter.Matter.Body.setAngle(body, angle);

      // Visual: dim line
      const g = scene.add.graphics().setDepth(3);
      g.lineStyle(thickness, 0x8899bb, 0.8);
      g.lineBetween(wall.x1, wall.y1, wall.x2, wall.y2);
    }
  }

  update(_time: number, _delta: number): void {}
}
```

- [ ] **Step 2: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add src/zoneB/WallSystem.ts
git commit -m "feat(zoneB): WallSystem — static barrier line segments"
```

---

### Task 7: ZoneBSystem — full wiring + HUD buffer display + game-over overlay

This task wires all sub-systems together, implements the ball buffer loop, and updates the HUD.

**Files:**
- Modify: `src/zoneB/ZoneBSystem.ts`
- Modify: `src/core/HUD.ts`

**Key logic:**
- `BALL_DROPPED` → spawn ball → `buffer.spend()` → emit `BUFFER_CHANGED`
- Gate callback → `replaceBall(multiplier)` → spawn N copies (each `createZoneBBall(..., fromSplit=true)`)
- Drain callback → `addScore(value × scoreMultiplier)` → `buffer.refillIfMilestone(total)` → maybe emit `BUFFER_CHANGED` → `onBallDrained()`
- `onBallDrained`: if `inFlight === 0` → emit `ZONE_B_EMPTY` → if `buffer.isExhausted()` → emit `BUFFER_EXHAUSTED` + show overlay

- [ ] **Step 1: Replace ZoneBSystem.ts**

`src/zoneB/ZoneBSystem.ts`:
```typescript
import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { INITIAL_LAYOUT } from './zoneLayout';
import { GateSystem } from './GateSystem';
import { CollectorSystem } from './CollectorSystem';
import { WallSystem } from './WallSystem';
import { BallBuffer } from './BallBuffer';
import {
  createZoneBBall,
  destroyZoneBBall,
  getBallData,
} from './ZoneBBall';

export class ZoneBSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly buffer = new BallBuffer();

  private readonly gates = new GateSystem(INITIAL_LAYOUT.gates, {
    onSplit: (img, multiplier) => this.handleSplit(img, multiplier),
  });
  private readonly collectors = new CollectorSystem(INITIAL_LAYOUT.collectors, {
    onDrain: (img, value, scoreMultiplier) => this.handleDrain(img, value, scoreMultiplier),
  });
  private readonly walls = new WallSystem(INITIAL_LAYOUT.walls);

  private inFlight = 0;
  private total = 0;
  private exhausted = false;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.gates.create(scene);
    this.collectors.create(scene);
    this.walls.create(scene);

    // Emit initial buffer state so the HUD shows the starting count.
    this.emitBuffer();

    this.bus.on(GameEvent.BallDropped, (ball) => {
      if (this.exhausted) return;
      this.buffer.spend();
      this.emitBuffer();

      const img = createZoneBBall(scene, ball.x, Layout.zoneBEntry.y, ball.value, ball.tier);
      this.onBallSpawned();
      void img; // img is live in the Matter world; lifecycle managed by destroy on drain/split
    });
  }

  update(time: number, delta: number): void {
    this.gates.update(time, delta);
    this.collectors.update(time, delta);
    this.walls.update(time, delta);
  }

  // --- Internal handlers ---------------------------------------------------

  private handleSplit(img: Phaser.Physics.Matter.Image, multiplier: number): void {
    const data = getBallData(img.body as MatterJS.BodyType);
    if (!data || !this.scene) return;

    destroyZoneBBall(img);
    this.replaceBall(multiplier);

    // Spawn copies in a small fan around the original position.
    const angleStep = Math.PI / (multiplier + 1);
    for (let i = 0; i < multiplier; i++) {
      const angle = angleStep * (i + 1) - Math.PI / 2; // fan downward
      const ox = Math.cos(angle) * 20;
      const oy = Math.sin(angle) * 20;
      const copy = createZoneBBall(
        this.scene,
        img.x + ox,
        img.y + oy,
        data.value,
        data.tier,
        true, // fromSplit — temporary gate grace period
      );
      // Give each copy a gentle push in the fan direction.
      const mb = copy.body as MatterJS.BodyType;
      Phaser.Physics.Matter.Matter.Body.setVelocity(mb, { x: ox * 0.15, y: Math.abs(oy) * 0.15 + 2 });
    }
  }

  private handleDrain(
    img: Phaser.Physics.Matter.Image,
    value: number,
    scoreMultiplier: number,
  ): void {
    destroyZoneBBall(img);
    this.addScore(value * scoreMultiplier);
    this.onBallDrained();
  }

  // --- Contract plumbing ---------------------------------------------------

  private onBallSpawned(): void {
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);
  }

  private onBallDrained(): void {
    if (this.inFlight === 0) return;
    this.inFlight -= 1;
    if (this.inFlight === 0) {
      this.bus.emit(GameEvent.ZoneBEmpty);
      if (this.buffer.isExhausted() && !this.exhausted) {
        this.exhausted = true;
        this.bus.emit(GameEvent.BufferExhausted);
        this.showGameOver();
      }
    }
  }

  /**
   * Called when a gate splits a ball: adjust inFlight by (multiplier - 1).
   * The original ball is already destroyed; multiplier copies will be spawned.
   */
  private replaceBall(multiplier: number): void {
    this.inFlight += multiplier - 1;
    // inFlight was >= 1 and multiplier >= 2, so still >= 2. No BUSY/EMPTY to emit.
  }

  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });
    if (this.buffer.refillIfMilestone(this.total)) {
      this.emitBuffer();
    }
  }

  private emitBuffer(): void {
    this.bus.emit(GameEvent.BufferChanged, {
      count: this.buffer.getCount(),
      nextMilestone: this.buffer.getNextMilestone(),
    });
  }

  // --- Game-over overlay (self-contained in Zone B) -------------------------

  private showGameOver(): void {
    if (!this.scene) return;
    const { x, y, width, height } = Layout.zoneB;
    const cx = x + width / 2;
    const cy = y + height / 2;

    this.scene.add.rectangle(cx, cy, width, height, 0x000000, 0.75).setDepth(3000);
    this.scene.add
      .text(cx, cy - 24, 'GAME OVER', {
        fontFamily: 'monospace', fontSize: '28px', color: '#ff4466',
      })
      .setOrigin(0.5)
      .setDepth(3001);
    this.scene.add
      .text(cx, cy + 16, `Score: ${this.total}`, {
        fontFamily: 'monospace', fontSize: '18px', color: '#ffffff',
      })
      .setOrigin(0.5)
      .setDepth(3001);
    this.scene.add
      .text(cx, cy + 44, 'Ball buffer exhausted', {
        fontFamily: 'monospace', fontSize: '13px', color: '#aaaaaa',
      })
      .setOrigin(0.5)
      .setDepth(3001);
  }
}
```

- [ ] **Step 2: Update HUD.ts to show buffer count**

In `src/core/HUD.ts`, add a second text object and listen to `BUFFER_CHANGED`:
```typescript
import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { WIDTH } from './Layout';

export class HUD implements GameSystem {
  private scoreText?: Phaser.GameObjects.Text;
  private bufferText?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scoreText = scene.add
      .text(WIDTH / 2, 16, '0', {
        fontFamily: 'monospace',
        fontSize: '28px',
        color: '#ffffff',
      })
      .setOrigin(0.5, 0)
      .setDepth(1000);

    this.bufferText = scene.add
      .text(WIDTH - 12, 16, '×20', {
        fontFamily: 'monospace',
        fontSize: '18px',
        color: '#88ccff',
      })
      .setOrigin(1, 0)
      .setDepth(1000);

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.scoreText?.setText(String(total));
    });

    this.bus.on(GameEvent.BufferChanged, ({ count }) => {
      this.bufferText?.setText(`×${count}`);
    });
  }

  update(_time: number, _delta: number): void {}
}
```

- [ ] **Step 3: Verify compilation**

```bash
npm run build 2>&1 | head -30
```
Expected: no new errors

- [ ] **Step 4: Run all tests**

```bash
npm run test
```
Expected: all existing tests + BallBuffer tests pass

- [ ] **Step 5: Smoke test in browser (`?zone=b`)**

```bash
npm run dev
```
Open `http://localhost:5173?zone=b`. Press SPACE or click DROP repeatedly.
Verify:
- Balls appear at the top of Zone B and fall under gravity
- Balls bounce off gate bodies; split balls fan out on contact
- Split balls resume gate collisions after ~300 ms
- Balls entering collector areas are removed and the score updates
- Buffer count (top-right HUD) decreases with each DROP
- Buffer count refills when the score milestone is reached
- When buffer hits 0 and Zone B drains, the game-over overlay appears

- [ ] **Step 6: Commit**

```bash
git add src/zoneB/ZoneBSystem.ts src/core/HUD.ts
git commit -m "feat(zoneB): full wiring — spawn, split, drain, buffer, HUD, game-over overlay"
```

---

## Self-review notes

- **BallBuffer exhaustion + ZONE_B_EMPTY race:** `onBallDrained` only checks exhaustion when `inFlight` reaches 0, after emitting `ZONE_B_EMPTY`. This is correct — the trap-door re-arms (via Zone C), but since `this.exhausted = true` is set, subsequent `BALL_DROPPED` events are silently ignored in `ZoneBSystem.create`.
- **Gate re-collision:** `fromSplit = true` disables gate collision for 300 ms via `collisionFilter`. After the grace period, copies re-enter the full filter. This prevents a copy immediately re-triggering the gate it was born from.
- **Pending-queue pattern:** both `GateSystem` and `CollectorSystem` flush their queues at the *start* of `update()`, before any new collision events (which only fire during physics stepping). This guarantees no mutation happens inside a callback.
- **`Funnel.ts`:** the file is an empty skeleton and its import is removed in `ZoneBSystem`. It need not be deleted — it simply goes unused.
- **Layout coordinates:** all values in `INITIAL_LAYOUT` are absolute screen coordinates (zone B starts at y = 492). Keep this convention for any future layout additions.
