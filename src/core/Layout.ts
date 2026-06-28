/**
 * Single source of truth for screen + zone geometry.
 *
 * Plain numbers, Phaser-free, so both halves and the tests can read it without
 * booting the engine. Both devs read this; it changes rarely and only by mutual
 * agreement (it sits behind the seam).
 */

/** Design resolution — locked portrait. The Scale manager fits this to the device. */
export const WIDTH = 390;
export const HEIGHT = 844;

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

// Vertical split (tunable). Zone A = big merge board on top, Zone C = thin
// trap-door band, Zone B = pinball arena below. The three tile the full height.
// Zone A was shortened by 30% (448 → 314); the freed height flows into Zone B,
// which absorbs it automatically as the remainder below.
const ZONE_A_HEIGHT = Math.round(448 * 0.7);
const ZONE_C_HEIGHT = 44;
const ZONE_B_HEIGHT = HEIGHT - ZONE_A_HEIGHT - ZONE_C_HEIGHT;

export const zoneA: Rect = { x: 0, y: 0, width: WIDTH, height: ZONE_A_HEIGHT };
export const zoneC: Rect = { x: 0, y: zoneA.y + zoneA.height, width: WIDTH, height: ZONE_C_HEIGHT };
export const zoneB: Rect = { x: 0, y: zoneC.y + zoneC.height, width: WIDTH, height: ZONE_B_HEIGHT };

/**
 * The fixed column every ball enters Zone B through (top-center of Zone B).
 *
 * FROZEN: this is the `x` Zone C stamps into every `BALL_DROPPED` payload. See the
 * note on `BallDroppedPayload.x` in contracts.ts.
 */
export const zoneBEntry = { x: WIDTH / 2, y: zoneB.y };
