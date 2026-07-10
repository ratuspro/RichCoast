import Phaser from 'phaser';
import { EventBus } from './core/EventBus';
import { GameEvent, type GameSystem } from './core/contracts';
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
import { PhaseDirector } from './core/PhaseDirector';
import { PAN_DISTANCE } from './core/phaseGeometry';
import { Sfx } from './core/Sfx';
import { PALETTES, Theme, applyPalette } from './core/Theme';
import { hexColor } from './core/Materials';
import { ThemeDirector } from './core/ThemeDirector';

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
  private debugHarness?: DebugHarness;
  private backdrop?: Phaser.GameObjects.Graphics;

  constructor() {
    super('GameScene');
  }

  create(): void {
    // Theme is module-global mutable state: a restart would otherwise leak the previous
    // run's palette into every colour about to be baked below. Reset before ANY drawing.
    applyPalette(PALETTES.workshop);

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
        // PhaseDirector goes LAST so its initial PHASE_CHANGED('A') lands after every
        // system has subscribed in its own create().
        return [
          hud,
          new ZoneASystem(this.bus),
          new ZoneCSystem(this.bus),
          new StubZoneB(this.bus),
          new ThemeDirector(this.bus),
          new PhaseDirector(this.bus),
        ];

      case 'b': {
        // No Zone A / director in this slice: park the camera in the B-phase framing so
        // the whole (taller-than-screen) Zone B band and its score bar are on screen.
        this.cameras.main.setScroll(0, PAN_DISTANCE);
        const driver = new StubZoneAC(this.bus);
        return [hud, new ZoneBSystem(this.bus), driver, new Harness(this.bus, driver)];
      }

      case 'full':
      default: {
        this.debugHarness = new DebugHarness(this.bus);
        return [
          hud,
          new ZoneASystem(this.bus),
          new ZoneCSystem(this.bus),
          new ZoneBSystem(this.bus),
          this.debugHarness,
          new ThemeDirector(this.bus),
          new PhaseDirector(this.bus),
        ];
      }
    }
  }

  /**
   * Warm-paper zone backdrops + dividers, straight from Layout, so all three regions
   * are visible even while every zone is still a skeleton.
   */
  private drawBackdrop(): void {
    this.backdrop = this.add.graphics().setDepth(-1000);
    this.restyleBackdrop();
    this.bus.on(GameEvent.ThemeChanged, () => this.restyleBackdrop());
  }

  private restyleBackdrop(): void {
    const g = this.backdrop;
    if (!g) return;
    this.cameras.main.setBackgroundColor(Theme.paper);
    // The HTML letterbox bars around the canvas are styled to Theme.paper in index.html —
    // keep them matched so a palette swap doesn't strand warm-paper bars beside the game.
    document.body.style.background = hexColor(Theme.paper);
    g.clear();
    // Zone A's band fill + funnel divider are owned by ArenaView now (its dedicated camera
    // fills the band and draws the funnel on the zooming layer), so the backdrop only paints
    // the static Zone C/B bands and their divider.
    const bands: Array<[Layout.Rect, number]> = [
      [Layout.zoneC, Theme.paperZoneC],
      [Layout.zoneB, Theme.paper],
    ];
    for (const [rect, color] of bands) {
      g.fillStyle(color, 1).fillRect(rect.x, rect.y, rect.width, rect.height);
    }
    g.lineStyle(2, Theme.pineDark, 1);
    g.lineBetween(0, Layout.zoneB.y, Layout.WIDTH, Layout.zoneB.y);
  }

  /**
   * Shared static Matter geometry. Zone A's own boundary (ceiling, side walls, funnel floor)
   * is owned by ArenaView because it must move outward as the arena grows; the only piece left
   * here is the off-screen bottom safety wall under Zone B.
   */
  private buildWorldGeometry(): void {
    const t = 40; // wall thickness, kept off-screen
    const w = Layout.WIDTH;
    // The world is taller than the screen (Zone B's band ends below y=HEIGHT and is only
    // fully framed in the B-phase camera scroll), so the safety wall sits under the WORLD
    // bottom, not the screen bottom — otherwise balls would rest on an invisible mid-arena floor.
    const worldBottom = Layout.zoneB.y + Layout.zoneB.height;
    this.matter.add.rectangle(w / 2, worldBottom + t / 2, w, t, { isStatic: true }); // bottom
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
