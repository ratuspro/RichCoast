import Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import * as Layout from '../core/Layout';
import { Theme } from '../core/Theme';
import type { WallDef } from './zoneLayout';
import { CAT_WALL, CAT_BALL } from './ZoneBBall';

const DEFAULT_THICKNESS = 6;
/** Thickness of the invisible Zone B containment border (kept mostly off-screen). */
const BOUND_THICKNESS = 40;

export class WallSystem implements GameSystem {
  /** Rail graphics kept alongside their defs so a Theme swap can repaint them in place. */
  private readonly rails: Array<{ g: Phaser.GameObjects.Graphics; wall: WallDef }> = [];

  constructor(private readonly layout: WallDef[]) {}

  create(scene: Phaser.Scene): void {
    this.addContainment(scene);

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

      const g = scene.add.graphics().setDepth(3);
      this.rails.push({ g, wall });
      this.drawRail(g, wall);
    }
  }

  update(_time: number, _delta: number): void {}

  /** Re-apply the active Theme to every rail (milestone palette swap). */
  restyle(): void {
    for (const { g, wall } of this.rails) this.drawRail(g, wall);
  }

  /** Visual: a pine guide rail — light wood over a darker shadow edge, matching
   *  Zone A's tray so the whole machine reads as one piece of joinery. */
  private drawRail(g: Phaser.GameObjects.Graphics, wall: WallDef): void {
    const thickness = wall.thickness ?? DEFAULT_THICKNESS;
    g.clear();
    if (wall.fillBelow) {
      // Solid wood between the ramp and the Zone B bottom edge.
      const bottom = Layout.zoneB.y + Layout.zoneB.height;
      g.fillStyle(Theme.pine, 1);
      g.beginPath();
      g.moveTo(wall.x1, wall.y1);
      g.lineTo(wall.x2, wall.y2);
      g.lineTo(wall.x2, bottom);
      g.lineTo(wall.x1, bottom);
      g.closePath();
      g.fillPath();
    }
    g.lineStyle(thickness + 2, Theme.pineShadow, 1);
    g.lineBetween(wall.x1, wall.y1, wall.x2, wall.y2);
    g.lineStyle(thickness, Theme.pine, 1);
    g.lineBetween(wall.x1, wall.y1, wall.x2, wall.y2);
  }

  /**
   * Invisible left/right/bottom border so balls are always contained within the
   * screen. The scene's outer world walls use the default collision category, which
   * Zone B balls deliberately don't mask against (so they pass through them and
   * escape off-screen, leaving inFlight stuck). These use CAT_WALL — which every
   * ball collides with — so nothing can leave the play area. The bottom edge sits
   * behind the funnel ramps, whose only opening is the collector mouth, so a ball
   * reaching the bottom always drains rather than resting there.
   */
  private addContainment(scene: Phaser.Scene): void {
    const { x, y, width, height } = Layout.zoneB;
    const t = BOUND_THICKNESS;
    const cy = y + height / 2;
    const opts: Phaser.Types.Physics.Matter.MatterBodyConfig = {
      isStatic: true,
      label: 'zoneB-bound',
      collisionFilter: { category: CAT_WALL, mask: CAT_BALL },
      friction: 0.1,
      restitution: 0.3,
    };
    // Left / right verticals: inner faces exactly at x and x+width, extended below
    // to meet the bottom. Bottom horizontal: inner face at y+height (screen bottom).
    scene.matter.add.rectangle(x - t / 2, cy, t, height + t, opts);
    scene.matter.add.rectangle(x + width + t / 2, cy, t, height + t, opts);
    scene.matter.add.rectangle(x + width / 2, y + height + t / 2, width + t * 2, t, opts);
  }
}
