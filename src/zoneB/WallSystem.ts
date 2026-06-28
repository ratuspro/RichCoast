import Phaser from 'phaser';
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
      scene.matter.body.setAngle(body, angle);

      // Visual: dim line
      const g = scene.add.graphics().setDepth(3);
      g.lineStyle(thickness, 0x8899bb, 0.8);
      g.lineBetween(wall.x1, wall.y1, wall.x2, wall.y2);
    }
  }

  update(_time: number, _delta: number): void {}
}
