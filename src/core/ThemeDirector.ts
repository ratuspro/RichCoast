import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { paletteNameForLevel } from './Progression';
import { PALETTES, Theme, applyPalette, type PaletteName } from './Theme';
import { lerpPalette } from './themeMath';

/** Cross-fade duration — matches ArenaView's ZOOM_MS so the palette lands with the zoom. */
const FADE_MS = 1200;

/**
 * Scene-level owner of the milestone colour swap (shared shell, like the PhaseDirector).
 *
 * PROGRESSION_CHANGED tells it which authored palette the run has earned
 * (`paletteNameForLevel`); the next ARENA_ZOOM {active:true} — only ever emitted by the
 * milestone zoom, whose input freeze we piggyback — cross-fades the active `Theme` there,
 * emitting THEME_CHANGED per tick so every baked surface restyles in step with the zoom.
 * It never touches a zone: `Theme` is the shared data, the bus is the repaint signal.
 */
export class ThemeDirector implements GameSystem {
  private scene?: Phaser.Scene;
  private current: PaletteName = 'workshop';
  private target: PaletteName = 'workshop';
  private tween?: Phaser.Tweens.Tween;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    // GameScene resets Theme to workshop before anything bakes a colour; mirror that truth.
    this.current = 'workshop';
    this.target = 'workshop';

    this.bus.on(GameEvent.ProgressionChanged, ({ level }) => {
      this.target = paletteNameForLevel(level);
    });
    this.bus.on(GameEvent.ArenaZoom, ({ active }) => {
      if (active) this.beginFade();
    });
  }

  update(_time: number, _delta: number): void {}

  destroy(): void {
    this.tween?.remove();
    this.tween = undefined;
  }

  private beginFade(): void {
    if (this.target === this.current) return;
    const from = { ...Theme };
    const to = PALETTES[this.target];
    this.current = this.target;

    this.tween?.remove();
    const proxy = { t: 0 };
    this.tween = this.scene?.tweens?.add({
      targets: proxy,
      t: 1,
      duration: FADE_MS,
      ease: 'Sine.easeInOut',
      onUpdate: () => {
        applyPalette(lerpPalette(from, to, proxy.t));
        this.bus.emit(GameEvent.ThemeChanged);
      },
      onComplete: () => {
        this.tween = undefined;
        applyPalette(to);
        this.bus.emit(GameEvent.ThemeChanged);
      },
    });
    // Headless/edge case: no tweens available — snap so the palette can't be left stale.
    if (!this.tween) {
      applyPalette(to);
      this.bus.emit(GameEvent.ThemeChanged);
    }
  }
}
