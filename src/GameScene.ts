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

/**
 * Which slice of the game is wired up:
 *  - `ac`   — real Zone A + C + HUD, faked Zone B (Dev 1's isolation build)
 *  - `b`    — real Zone B + HUD, faked + instrumented A/C (Dev 2's isolation build)
 *  - `full` — every real system together (the integration target / default)
 */
export type ZoneMode = 'ac' | 'b' | 'full';
export const ZONE_MODE_KEY = 'zoneMode';

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

  constructor() {
    super('GameScene');
  }

  create(): void {
    const mode = (this.registry.get(ZONE_MODE_KEY) as ZoneMode | undefined) ?? 'full';

    this.drawBackdrop();
    this.buildWorldGeometry();

    this.systems = this.buildSystems(mode);
    for (const system of this.systems) system.create(this);

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => this.teardown());
  }

  override update(time: number, delta: number): void {
    for (const system of this.systems) system.update(time, delta);
  }

  private buildSystems(mode: ZoneMode): GameSystem[] {
    const hud = new HUD(this.bus);

    switch (mode) {
      case 'ac':
        return [new ZoneASystem(), new ZoneCSystem(this.bus), hud, new StubZoneB(this.bus)];

      case 'b': {
        const driver = new StubZoneAC(this.bus);
        return [new ZoneBSystem(this.bus), hud, driver, new Harness(this.bus, driver)];
      }

      case 'full':
      default:
        return [
          new ZoneASystem(),
          new ZoneCSystem(this.bus),
          new ZoneBSystem(this.bus),
          hud,
        ];
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
    g.lineBetween(0, Layout.zoneC.y, Layout.WIDTH, Layout.zoneC.y);
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

    // TODO(seam): the Zone A/B divider with the trap-door gap belongs here, once
    // Zone C's tunnel geometry is fixed. Left open while zones are skeletons so
    // test balls aren't trapped above an unfinished floor.
  }

  private teardown(): void {
    for (const system of this.systems) system.destroy?.();
    this.systems = [];
    this.bus.clear();
  }
}
