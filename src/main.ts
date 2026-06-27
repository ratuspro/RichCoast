import Phaser from 'phaser';
import { WIDTH, HEIGHT } from './core/Layout';
import { GameScene, parseZoneMode, ZONE_MODE_KEY } from './GameScene';

const config: Phaser.Types.Core.GameConfig = {
  type: Phaser.AUTO,
  parent: 'app',
  backgroundColor: '#0b0d12',
  // Render at the design resolution and letterbox-fit it to any device, centered.
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
    width: WIDTH,
    height: HEIGHT,
  },
  physics: {
    default: 'matter',
    matter: {
      gravity: { x: 0, y: 1 },
      // Body/constraint outlines while developing; off in production builds.
      debug: import.meta.env.DEV,
    },
  },
  scene: [GameScene],
};

const game = new Phaser.Game(config);

// Decide once, here, which slice to run; the scene reads it on create.
game.registry.set(ZONE_MODE_KEY, parseZoneMode(window.location.search));

// Dev-only handle so the browser console and automated (Playwright) tests can
// inspect the running game. Stripped from production builds.
if (import.meta.env.DEV) {
  (window as unknown as { game: Phaser.Game }).game = game;
}
