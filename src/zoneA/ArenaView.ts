import Phaser from 'phaser';
import * as Layout from '../core/Layout';
import { DEATH_LINE_Y, SPAWN_Y } from './tuning';

/**
 * The expanding Zone A arena + its own camera.
 *
 * Zone A is a power-of-three merge board with no tier ceiling, so balls grow without
 * bound. To make room, the arena GROWS at recurring milestones: its scale `s` multiplies
 * by GROW (geometrically: 1, 1.18, 1.39, …), the boundary walls/floor move outward, and a dedicated Zone-A camera zooms out
 * (zoom = 1/s) so balls keep their real physics size yet appear smaller and keep their
 * relative positions. The funnel apex stays pinned at the Zone A/C boundary so it still
 * feeds Zone C, and the arena only ever grows UP (ceiling into negative y) and OUT (walls
 * past the screen edges) — never down into Zone B.
 *
 * Owned by Zone A (ZoneASystem). Other zones never touch the camera; Zone C reacts to the
 * `ArenaZoom` bus event for its input lock.
 */

/** Multiplier applied to the arena scale at each milestone (geometric growth: `s` ramps
 *  1, 1.18, 1.39, 1.64, …). The camera zoom is 1/s, so each milestone shrinks balls by the
 *  same 1/GROW ratio. */
export const GROW = 1.18;

/** Camera tween duration for one zoom-out, in ms. */
const ZOOM_MS = 1200;

/** Inward tilt of each half of the funnel floor (degrees) — a shallow V toward centre. */
const FLOOR_FUNNEL_DEG = 5;

/** Static-body wall thickness (kept mostly off the visible arena). */
const WALL_T = 40;

/**
 * Screen y where the arena camera's viewport starts — just below the HUD chrome bar
 * (HUD.BAND_H = 42), so the bar (drawn by the main camera) is never overdrawn by the
 * zoomed balls and the framing at s=1 matches the original single-camera view exactly.
 */
const ARENA_VIEW_TOP = 42;

/** Funnel apex y — the Zone A/C boundary. Constant: it always feeds Zone C. */
const FLOOR_Y = Layout.zoneA.y + Layout.zoneA.height;

/** Visible arena viewport height (Zone A band minus the HUD bar). */
const VIEW_H = Layout.zoneA.height - ARENA_VIEW_TOP;

interface Point {
  x: number;
  y: number;
}

export class ArenaView {
  private s = 1;
  private camera!: Phaser.Cameras.Scene2D.Camera;
  /** Holds every Zone A gameplay object so the main camera can ignore them in one call. */
  layer!: Phaser.GameObjects.Layer;
  private walls: MatterJS.BodyType[] = [];
  private wallGfx!: Phaser.GameObjects.Graphics;
  private animating = false;

  constructor(private readonly scene: Phaser.Scene) {}

  create(): void {
    this.layer = this.scene.add.layer();

    this.camera = this.scene.cameras.add(0, ARENA_VIEW_TOP, Layout.WIDTH, VIEW_H);
    this.camera.setName('arena');
    this.camera.setBackgroundColor(0x141925); // the Zone A band fill (replaces the backdrop's)

    // The main camera draws everything EXCEPT Zone A gameplay (one call, covers future balls).
    this.scene.cameras.main.ignore(this.layer);

    // The arena camera must not draw the screen-space UI that already exists (HUD chrome +
    // text, the ignored backdrop). Snapshot-ignore everything present now except our layer;
    // objects added later are either claimed onto the layer (balls/aim/death line) or live
    // below the funnel (Zone B/C — culled by the viewport) or are intentionally drawn over
    // the band (the game-over overlay). AimController ignores its own queue-row separately.
    for (const child of this.scene.children.list) {
      if (child !== (this.layer as unknown as Phaser.GameObjects.GameObject)) {
        this.camera.ignore(child);
      }
    }

    this.wallGfx = this.scene.add.graphics();
    this.claim(this.wallGfx);
    this.buildBodies();
    this.applyCamera(this.s);
  }

  /** Add a Zone A gameplay object to the arena layer (rendered zoomed, ignored by main). */
  claim(obj: Phaser.GameObjects.GameObject): void {
    this.layer.add(obj);
  }

  /** Hide a screen-space object (e.g. a queue-row element) from the zoomed arena camera. */
  ignoreOnArenaCamera(obj: Phaser.GameObjects.GameObject | Phaser.GameObjects.GameObject[]): void {
    this.camera.ignore(obj);
  }

  get scale(): number { return this.s; }
  get isAnimating(): boolean { return this.animating; }

  /** Spawn row y in arena world space (scales up the band as it grows). */
  get spawnY(): number { return FLOOR_Y - (Layout.zoneA.height - SPAWN_Y) * this.s; }
  /** Death/overflow line y in arena world space. */
  get deathLineY(): number { return FLOOR_Y - (Layout.zoneA.height - DEATH_LINE_Y) * this.s; }
  /** Inner left/right wall x (move outward, past the screen edges, as the arena grows). */
  get minX(): number { return Layout.WIDTH / 2 - (Layout.WIDTH / 2) * this.s; }
  get maxX(): number { return Layout.WIDTH / 2 + (Layout.WIDTH / 2) * this.s; }
  private get ceilingY(): number { return FLOOR_Y - Layout.zoneA.height * this.s; }

  /** Screen→arena-world point, so aiming maps correctly under the zoomed/scrolled camera. */
  worldPoint(screenX: number, screenY: number): Phaser.Math.Vector2 {
    return this.camera.getWorldPoint(screenX, screenY);
  }

  /**
   * Arena-world point → on-screen (main-camera) point. The inverse of `worldPoint`, for
   * snapshots that start where a ball appears but then leave the arena into Zone B (which the
   * arena camera's viewport culls), so they must be drawn on the main camera at screen coords.
   */
  screenPoint(worldX: number, worldY: number): { x: number; y: number } {
    const view = this.camera.worldView;
    return {
      x: this.camera.x + (worldX - view.x) * this.camera.zoom,
      y: this.camera.y + (worldY - view.y) * this.camera.zoom,
    };
  }

  /** Apparent-size multiplier: a world length of L looks `L * viewScale` px on screen. */
  get viewScale(): number { return this.camera.zoom; }

  /**
   * Grow the arena one milestone step: scale up, move the walls/floor outward (always away
   * from the balls, so no static-into-dynamic overlap), and tween the camera zoom-out.
   * `onComplete` fires when the tween lands (the caller re-enables input there).
   */
  grow(onComplete: () => void): void {
    this.animating = true;
    this.s *= GROW;
    this.buildBodies();

    const from = { z: this.camera.zoom };
    const target = 1 / this.s;
    this.scene.tweens.add({
      targets: from,
      z: target,
      duration: ZOOM_MS,
      ease: 'Cubic.easeInOut',
      onUpdate: () => this.applyCameraZoom(from.z),
      onComplete: () => {
        this.applyCamera(this.s);
        this.animating = false;
        onComplete();
      },
    });
  }

  destroy(): void {
    this.scene.tweens.killTweensOf(this.camera);
    const world = this.scene.matter.world;
    if (world) for (const body of this.walls) world.remove(body);
    this.walls = [];
    this.scene.cameras.remove(this.camera);
    this.layer?.destroy(true);
  }

  /** Apply zoom + recentre for the current scale (called at rest, start, and tween end). */
  private applyCamera(s: number): void {
    this.applyCameraZoom(1 / s);
  }

  /** Set the camera zoom and recentre so the floor stays pinned to the band's bottom edge. */
  private applyCameraZoom(zoom: number): void {
    const s = 1 / zoom;
    this.camera.setZoom(zoom);
    // Visible world spans VIEW_H*s tall ending at FLOOR_Y, and WIDTH*s wide centred on WIDTH/2.
    this.camera.centerOn(Layout.WIDTH / 2, FLOOR_Y - (VIEW_H / 2) * s);
  }

  /** Destroy + rebuild the boundary walls + funnel floor at the current scale, and redraw them. */
  private buildBodies(): void {
    const world = this.scene.matter.world;
    if (world) for (const body of this.walls) world.remove(body);
    this.walls = [];

    const left = this.minX;
    const right = this.maxX;
    const top = this.ceilingY;
    const midY = (top + FLOOR_Y) / 2;
    const spanY = FLOOR_Y - top + 2 * WALL_T;
    const add = (x: number, y: number, w: number, h: number, angle = 0): void => {
      this.walls.push(this.scene.matter.add.rectangle(x, y, w, h, { isStatic: true, angle }));
    };

    add(Layout.WIDTH / 2, top - WALL_T / 2, right - left + 2 * WALL_T, WALL_T); // ceiling
    add(left - WALL_T / 2, midY, WALL_T, spanY); // left wall
    add(right + WALL_T / 2, midY, WALL_T, spanY); // right wall

    const [l, apex, r] = this.floorEdge();
    this.addFloorSegment(l, apex);
    this.addFloorSegment(apex, r);

    this.redrawWalls(l, apex, r, top);
  }

  /** Funnel top-edge points: raised side corners + the centre apex at FLOOR_Y. Scaled. */
  private floorEdge(): [Point, Point, Point] {
    const half = (Layout.WIDTH / 2) * this.s;
    const drop = half * Math.tan((FLOOR_FUNNEL_DEG * Math.PI) / 180);
    return [
      { x: this.minX, y: FLOOR_Y - drop },
      { x: Layout.WIDTH / 2, y: FLOOR_Y },
      { x: this.maxX, y: FLOOR_Y - drop },
    ];
  }

  /** One static, rotated floor rectangle whose top edge runs from p0 to p1. */
  private addFloorSegment(p0: Point, p1: Point): void {
    const dx = p1.x - p0.x;
    const dy = p1.y - p0.y;
    const len = Math.hypot(dx, dy);
    const angle = Math.atan2(dy, dx);
    const cx = (p0.x + p1.x) / 2 + (-dy / len) * (WALL_T / 2);
    const cy = (p0.y + p1.y) / 2 + (dx / len) * (WALL_T / 2);
    this.walls.push(
      this.scene.matter.add.rectangle(cx, cy, len + 8, WALL_T, { isStatic: true, angle }),
    );
  }

  /** Visible arena boundary: side walls + funnel V (on the layer, so it zooms with the balls). */
  private redrawWalls(l: Point, apex: Point, r: Point, top: number): void {
    const g = this.wallGfx.clear().lineStyle(2, 0x2a3346, 1);
    g.lineBetween(this.minX, top, l.x, l.y);
    g.lineBetween(this.maxX, top, r.x, r.y);
    g.beginPath();
    g.moveTo(l.x, l.y);
    g.lineTo(apex.x, apex.y);
    g.lineTo(r.x, r.y);
    g.strokePath();
  }
}
