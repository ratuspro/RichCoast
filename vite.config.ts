import { defineConfig } from 'vitest/config';

// Single config for both Vite (dev/build) and Vitest (unit tests).
export default defineConfig(() => ({
  // Expose the dev server on the LAN so the game can be opened on a real phone
  // (portrait, touch) instead of only an emulated desktop viewport.
  server: { host: true },

  test: {
    // The tested modules (contracts, EventBus, Layout, MergeLogic) are
    // Phaser-free by design, so they run in plain Node — no jsdom needed.
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
}));
