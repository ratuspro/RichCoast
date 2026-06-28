import Phaser from 'phaser';
import { EventBus } from './core/EventBus';
import type { GameSystem } from './core/contracts';
import * as Layout from './core/Layout';
import { HUD } from './core/HUD';
import { ZoneASystem } from './zoneA/ZoneASystem';
import { ZoneCSystem } from './zoneC/ZoneCSystem';
import { ZoneBSystem } from './zoneB/ZoneBSystem';
import { StubZoneB } from './dev/stubZoneB';
import { StubZoneAC } from './dev/stubZoneAC';
import { Harness } from './dev/harness';
import { DebugHarness } from './dev/DebugHarness';
import { isDebug, toggleDebug } from './core/DebugMode';
import { Sfx } from './core/Sfx';

/**
 * Which slice of the game is wired up:
 *  - `ac`   — real Zone A + C + HUD, faked Zone B (Dev 1's isolation build)
 *  - `b`    — real Zone B + HUD, faked + instrumented A/C (Dev 2's isolation build)
 *  - `full` — every real system together (the integration target / default)
 */
export type ZoneMode = 'ac' | 'b' | 'full';
export const ZONE_MODE_KEY = 'zoneMode';

interface Point {
  x: number;
  y: number;
}

/** Inward tilt of each half of the Zone A floor — a funnel toward centre. */
const FLOOR_FUNNEL_DEG = 5;

export function parseZoneMode(search: string): ZoneMode {
  const zone = new URLSearchParams(search).get('zone');
  return zone === 'ac' || zone === 'b' ? zone : 'full';
}

/**
 * The single scene. It stays deliberately thin: build the bus + shared geometry,
 * instantiate the systems for the active mode, then just fan `update` out to them.
 * Zones never reference each other here — they only share the bus.
 */
export class GameScene extends Phaser.Scene {
  private readonly bus = new EventBus();
  private systems: GameSystem[] = [];
  private debugHarness?: DebugHarness;

  constructor() {
    super('GameScene');
  }

  create(): void {
    const mode = (this.registry.get(ZONE_MODE_KEY) as ZoneMode | undefined) ?? 'full';

    this.drawBackdrop();
    this.buildWorldGeometry();

    Sfx.init(this);

    this.systems = this.buildSystems(mode);
    for (const system of this.systems) system.create(this);

    this.applyDebug(isDebug());
    this.input.keyboard?.on('keydown-D', () => {
      toggleDebug();
      this.applyDebug(isDebug());
    });
    this.input.keyboard?.on('keydown-M', () => Sfx.toggleMute());

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => this.teardown());
  }

  override update(time: number, delta: number): void {
    for (const system of this.systems) system.update(time, delta);
  }

  private buildSystems(mode: ZoneMode): GameSystem[] {
    const hud = new HUD(this.bus);

    switch (mode) {
      case 'ac':
        return [new ZoneASystem(this.bus), new ZoneCSystem(this.bus), hud, new StubZoneB(this.bus)];

      case 'b': {
        const driver = new StubZoneAC(this.bus);
        return [new ZoneBSystem(this.bus), hud, driver, new Harness(this.bus, driver)];
      }

      case 'full':
      default: {
        this.debugHarness = new DebugHarness(this.bus);
        return [
          new ZoneASystem(this.bus),
          new ZoneCSystem(this.bus),
          new ZoneBSystem(this.bus),
          hud,
          this.debugHarness,
        ];
      }
    }
  }

  /**
   * Dark zone backdrops + dividers, straight from Layout, so all three regions
   * are visible even while every zone is still a skeleton.
   */
  private drawBackdrop(): void {
    const g = this.add.graphics().setDepth(-1000);
    const bands: Array<[Layout.Rect, number]> = [
      [Layout.zoneA, 0x141925],
      [Layout.zoneC, 0x0e1119],
      [Layout.zoneB, 0x10141d],
    ];
    for (const [rect, color] of bands) {
      g.fillStyle(color, 1).fillRect(rect.x, rect.y, rect.width, rect.height);
    }
    g.lineStyle(2, 0x2a3346, 1);
    // The Zone A/C divider follows the funnel floor's top edge (a shallow V).
    const [left, apex, right] = GameScene.floorEdge();
    g.beginPath();
    g.moveTo(left.x, left.y);
    g.lineTo(apex.x, apex.y);
    g.lineTo(right.x, right.y);
    g.strokePath();
    g.lineBetween(0, Layout.zoneB.y, Layout.WIDTH, Layout.zoneB.y);
  }

  /**
   * Shared static Matter geometry: the four outer walls. Scene-owned because both
   * halves depend on it — neither zone should invent its own world bounds.
   */
  private buildWorldGeometry(): void {
    const t = 40; // wall thickness, kept mostly off-screen
    const { WIDTH: w, HEIGHT: h } = Layout;
    const walls: ReadonlyArray<readonly [number, number, number, number]> = [
      [w / 2, -t / 2, w, t], // top
      [w / 2, h + t / 2, w, t], // bottom
      [-t / 2, h / 2, t, h], // left
      [w + t / 2, h / 2, t, h], // right
    ];
    for (const [x, y, ww, hh] of walls) {
      this.matter.add.rectangle(x, y, ww, hh, { isStatic: true });
    }

    // Zone A floor: where merge balls rest, keeping them above the divider. The
    // trap-door is logical — Zone C reads ball bodies off the shared world and
    // removes the consumed one — so it needs no physical gap and a solid floor is
    // correct. Built as two segments tilted 2° toward the centre (a shallow V),
    // so resting balls drift toward the middle where Zone C's trap-door waits.
    const [left, apex, right] = GameScene.floorEdge();
    this.addFloorSegment(left, apex, t);
    this.addFloorSegment(apex, right, t);
  }

  /**
   * The three top-edge points of the funnel floor: the two side corners (raised)
   * and the centre apex (lowest), sitting exactly on the Zone A/C boundary. Each
   * half slopes inward at FLOOR_FUNNEL_DEG, so the left half tilts clockwise and
   * the right half counter-clockwise — funnelling balls to the centre.
   */
  private static floorEdge(): [Point, Point, Point] {
    const floorTop = Layout.zoneA.y + Layout.zoneA.height;
    const drop = (Layout.WIDTH / 2) * Math.tan((FLOOR_FUNNEL_DEG * Math.PI) / 180);
    return [
      { x: 0, y: floorTop - drop },
      { x: Layout.WIDTH / 2, y: floorTop },
      { x: Layout.WIDTH, y: floorTop - drop },
    ];
  }

  /** One static, rotated floor rectangle whose top edge runs from p0 to p1. */
  private addFloorSegment(p0: Point, p1: Point, thickness: number): void {
    const dx = p1.x - p0.x;
    const dy = p1.y - p0.y;
    const len = Math.hypot(dx, dy);
    const angle = Math.atan2(dy, dx);
    // Drop the body's centre a half-thickness below the top edge (perpendicular),
    // and overlap a touch at the apex/walls so there's no seam.
    const cx = (p0.x + p1.x) / 2 + (-dy / len) * (thickness / 2);
    const cy = (p0.y + p1.y) / 2 + (dx / len) * (thickness / 2);
    this.matter.add.rectangle(cx, cy, len + 8, thickness, { isStatic: true, angle });
  }

  private applyDebug(on: boolean): void {
    this.matter.world.drawDebug = on;
    if (!on) this.matter.world.debugGraphic?.clear();
    this.debugHarness?.setVisible(on);
  }

  private teardown(): void {
    for (const system of this.systems) system.destroy?.();
    this.systems = [];
    this.bus.clear();
  }
}
