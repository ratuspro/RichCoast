import { beforeEach, describe, expect, it } from 'vitest';
import { EventBus } from './EventBus';
import { GameEvent } from './contracts';
import { PALETTES, Theme, applyPalette } from './Theme';
import { ThemeDirector } from './ThemeDirector';

// A scene with no tween manager exercises the director's snap fallback — the same
// listener wiring as the real path, minus the animation frames.
const tweenlessScene = { tweens: undefined } as unknown as Phaser.Scene;

describe('ThemeDirector', () => {
  let bus: EventBus;
  let director: ThemeDirector;
  let themeChanges: number;

  beforeEach(() => {
    applyPalette(PALETTES.workshop);
    bus = new EventBus();
    director = new ThemeDirector(bus);
    director.create(tweenlessScene);
    themeChanges = 0;
    bus.on(GameEvent.ThemeChanged, () => { themeChanges += 1; });
  });

  it('swaps the palette on the milestone zoom after the level earns one', () => {
    bus.emit(GameEvent.ProgressionChanged, {
      level: 25, minTier: 5, maxTier: 8, bufferCapacity: 31, scoreBarTarget: 1300,
    });
    expect(Theme.paper).toBe(PALETTES.workshop.paper); // nothing until the zoom
    bus.emit(GameEvent.ArenaZoom, { active: true });
    expect(Theme.paper).toBe(PALETTES.dusk.paper);
    expect(themeChanges).toBeGreaterThan(0);
  });

  it('does nothing on zooms that do not change the palette', () => {
    bus.emit(GameEvent.ProgressionChanged, {
      level: 10, minTier: 1, maxTier: 4, bufferCapacity: 16, scoreBarTarget: 110,
    });
    bus.emit(GameEvent.ArenaZoom, { active: true });
    expect(Theme.paper).toBe(PALETTES.workshop.paper);
    expect(themeChanges).toBe(0);
  });

  it('ignores the zoom-end signal', () => {
    bus.emit(GameEvent.ProgressionChanged, {
      level: 50, minTier: 9, maxTier: 12, bufferCapacity: 56, scoreBarTarget: 105000,
    });
    bus.emit(GameEvent.ArenaZoom, { active: false });
    expect(Theme.paper).toBe(PALETTES.workshop.paper);
  });
});
