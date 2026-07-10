import Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import type { GateDef, TranslatingGate, RotatingGate } from './zoneLayout';
import { getBallData, getBallImage, CAT_GATE, CAT_BALL } from './ZoneBBall';
import { isDebug } from '../core/DebugMode';
import { Theme } from '../core/Theme';
import { hexColor } from '../core/Materials';

export interface GateCallbacks {
  /** Called when a ball body hits a gate; system should handle inFlight bookkeeping + spawning copies. */
  onSplit(img: Phaser.Physics.Matter.Image, multiplier: number): void;
}

interface RuntimeGate {
  def: GateDef;
  body: MatterJS.BodyType;
  /** Elapsed ms used for oscillation. */
  elapsed: number;
  labelText: Phaser.GameObjects.Text;
  /** The painted sign rectangle (also reachable through the body, but kept here so a
   *  Theme swap can restyle it without the cast). */
  gfx: Phaser.GameObjects.Rectangle;
}

const GATE_THICKNESS = 16;

export class GateSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly gates: RuntimeGate[] = [];
  /** Map keyed by Matter body reference — same pattern as Zone A's ball registry. */
  private readonly gateBodyMap = new Map<MatterJS.BodyType, RuntimeGate>();
  private readonly pending: Array<{ img: Phaser.Physics.Matter.Image; multiplier: number }> = [];

  constructor(
    private readonly layout: GateDef[],
    private readonly callbacks: GateCallbacks,
  ) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;

    for (const def of this.layout) {
      const { body, labelText, gfx } = this.buildBody(scene, def);
      const rg: RuntimeGate = { def, body, elapsed: 0, labelText, gfx };
      this.gates.push(rg);
      this.gateBodyMap.set(body, rg);
      if (isDebug()) console.log('[GateSystem] gate created, label:', body.label, 'body id:', body.id);
    }

    scene.matter.world.on(
      Phaser.Physics.Matter.Events.COLLISION_START,
      (event: Phaser.Physics.Matter.Events.CollisionStartEvent) => {
        if (isDebug()) console.log('[GateSystem] COLLISION_START pairs:', event?.pairs?.length);
        for (const pair of (event?.pairs ?? [])) {
          if (isDebug()) console.log('[GateSystem] pair:', pair.bodyA?.label, 'vs', pair.bodyB?.label,
            '| A.parent:', pair.bodyA?.parent?.label, '| B.parent:', pair.bodyB?.parent?.label);
          this.checkSplit(pair.bodyA, pair.bodyB);
          this.checkSplit(pair.bodyB, pair.bodyA);
        }
      },
    );
  }

  update(_time: number, delta: number): void {
    const splits = this.pending.splice(0);
    for (const { img, multiplier } of splits) {
      if (img.active) this.callbacks.onSplit(img, multiplier);
    }

    for (const gate of this.gates) {
      gate.elapsed += delta;
      this.applyMotion(gate);
    }
  }

  /** Re-apply the active Theme to every gate sign (milestone palette swap). */
  restyle(): void {
    for (const { def, gfx, labelText } of this.gates) {
      gfx.setFillStyle(this.paintFor(def)).setStrokeStyle(2, Theme.pineShadow);
      labelText.setColor(hexColor(Theme.ink));
    }
  }

  /** Sign paint: green for high multipliers, brass for low. */
  private paintFor(def: GateDef): number {
    return def.multiplier >= 4 ? Theme.gatePaint : Theme.brass;
  }

  private buildBody(scene: Phaser.Scene, def: GateDef): { body: MatterJS.BodyType; labelText: Phaser.GameObjects.Text; gfx: Phaser.GameObjects.Rectangle } {
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
    scene.matter.body.setAngle(rect, angle);

    // Visual: a painted wooden sign — green paint for high multipliers, brass for low —
    // with a dark stencilled "X N" and a wood-shadow edge so it sits on the paper.
    const g = scene.add
      .rectangle(cx, cy, def.length, GATE_THICKNESS, this.paintFor(def))
      .setStrokeStyle(2, Theme.pineShadow)
      .setDepth(5);
    const labelText = scene.add.text(cx, cy, `X${def.multiplier}`, {
      fontFamily: 'sans-serif', fontSize: '15px', fontStyle: 'bold', color: hexColor(Theme.ink),
    }).setOrigin(0.5).setDepth(6);

    // Track the visual rect alongside the body so we can move it in update()
    (rect as unknown as { gfx: Phaser.GameObjects.Rectangle }).gfx = g;

    return { body: rect, labelText, gfx: g };
  }

  private applyMotion(gate: RuntimeGate): void {
    const { def, body, elapsed, labelText } = gate;
    if (def.type === 'static') return;
    if (!this.scene) return;

    const gfx = (body as unknown as { gfx: Phaser.GameObjects.Rectangle }).gfx;

    if (def.type === 'translating') {
      const td = def as TranslatingGate;
      const t = (Math.sin((elapsed / td.periodMs) * Math.PI * 2) + 1) / 2;
      const nx = td.ax + (td.bx - td.ax) * t;
      const ny = td.ay + (td.by - td.ay) * t;
      this.scene.matter.body.setPosition(body, { x: nx, y: ny });
      if (gfx) { gfx.x = nx; gfx.y = ny; }
      if (labelText) { labelText.x = nx; labelText.y = ny; }
    } else {
      // rotating
      const rd = def as RotatingGate;
      const angle = elapsed * rd.speedRadPerMs;
      this.scene.matter.body.setAngle(body, angle);
      if (gfx) { gfx.x = rd.cx; gfx.y = rd.cy; gfx.rotation = angle; }
      if (labelText) { labelText.x = rd.cx; labelText.y = rd.cy; }
    }
  }

  private checkSplit(maybeGate: MatterJS.BodyType, maybeBall: MatterJS.BodyType): void {
    // Lookup by body reference (like Zone A's registry), not by label.
    const runtimeGate = this.gateBodyMap.get(maybeGate);
    if (!runtimeGate) return;

    const ballRoot = maybeBall.parent ?? maybeBall;
    const data = getBallData(ballRoot);
    if (!data) {
      if (isDebug()) console.log('[GateSystem] checkSplit: gate hit but no ballData. ball label:', maybeBall.label, 'id:', maybeBall.id);
      return;
    }
    const img = getBallImage(ballRoot);
    if (!img?.active) {
      if (isDebug()) console.log('[GateSystem] checkSplit: no image or inactive');
      return;
    }
    if (isDebug()) console.log('[GateSystem] QUEUING SPLIT ×', runtimeGate.def.multiplier);
    this.pending.push({ img, multiplier: runtimeGate.def.multiplier });
  }
}
