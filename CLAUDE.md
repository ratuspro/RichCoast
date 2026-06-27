# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
npm run dev       # start Vite dev server (hot-reload)
npm run build     # tsc type-check + Vite production build
npm run preview   # serve the production build locally
npx tsc --noEmit  # type-check only, no output files
```

There are no tests. Type-checking (`npx tsc --noEmit`) is the primary correctness gate.

## Architecture

A Phaser 4 / TypeScript mobile game (390×844 portrait canvas). All textures are generated procedurally in `BootScene` — there are no image assets to load.

### Scene lifecycle
`main.ts` → `BootScene` (preload/generate textures) → `GameScene` (runs indefinitely, restarts itself on game-over).

### Two-zone split
The play area is divided at `NET_Y = 380` (exported from `UpperZone.ts`). Two developers own one zone each:

- **`src/zones/UpperZone.ts`** — upper half (y 0–380): boulder obstacles, net segments, trapdoors, ball spawn/aim/fire, game-over detection.
- **`src/zones/LowerZone.ts`** — lower half (y 380–844): ramps, multiplier gates (×2 / ×3), ball multiplication logic.

**`src/scenes/GameScene.ts`** is a thin orchestrator: it owns the shared `balls` group, the four boundary walls, score UI, and game-over overlay. It calls `zone.create()` then `zone.update()` each frame. Cross-zone communication is via typed callback interfaces (`UpperZoneCallbacks`, `LowerZoneCallbacks`).

### Shared state
Both zones receive the same `Phaser.Physics.Arcade.Group` (`balls`) and `Phaser.Physics.Arcade.StaticGroup` (`walls`) from `GameScene`. All physics colliders are registered inside the zone that owns them.

### Physics notes
- World physics: arcade, gravity `y=800`.
- Balls use `setCollideWorldBounds(true)` — the game area is fully closed.
- Wall bodies are inset so their **inner** edge sits exactly on the canvas boundary (e.g. top wall: `cy = WALL/2`). Visual edges are drawn with a glow effect (two overlapping `Graphics` lines).
- The waiting ball (pre-fire) has `allowGravity = false`; gravity is enabled on fire.
- Trapdoor collision is handled manually each `update()` frame (arcade physics doesn't support toggling static body active state cleanly).

### TypeScript constraints
`erasableSyntaxOnly` is enabled — **parameter properties (`private x` in constructors) are forbidden**. Always declare fields explicitly and assign them in the constructor body. `noUnusedLocals` and `noUnusedParameters` are also enforced.

### Object classes
- `src/objects/Gate.ts` — multiplier gate (×2 or ×3) with a cooldown after triggering.
- `src/objects/TrapDoor.ts` — toggles open/closed on a timer; gap in the net that balls can pass through.
