import Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import { Theme } from '../core/Theme';
import type { CollectorDef } from './zoneLayout';
import { getBallData, getBallImage, CAT_COLLECTOR, CAT_BALL } from './ZoneBBall';

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

      // Visual: the mouth of a toy collection crate — shadowed wood fill, brass rim.
      scene.add
        .rectangle(cx, cy, def.width, def.height, Theme.pineDark, 0.4)
        .setStrokeStyle(2, Theme.brass)
        .setDepth(4);
      if (def.scoreMultiplier !== 1) {
        scene.add
          .text(cx, cy, `×${def.scoreMultiplier}`, {
            fontFamily: 'monospace', fontSize: '11px', color: '#3f3428', // Theme.ink
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

    const data = getBallData(maybeBall);
    if (!data) return; // not a Zone B ball

    const img = getBallImage(maybeBall);
    if (!img?.active) return;

    this.pending.push({ img, value: data.value, scoreMultiplier: def.scoreMultiplier });
  }
}
