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
// trap-door band, Zone B = pinball arena below.
//
// The three zones NO LONGER tile the 844px screen: the world is 1238px tall and the
// game alternates between two camera framings (see core/phaseGeometry.ts):
//  - A-phase (scroll 0): the full Zone A band is on screen — 2/3 of the screen
//    (42px HUD + 521px board = 563 = round(844 × 2/3)); Zone B is bottom-cropped by 394px.
//  - B-phase (scroll 394): Zone A shows only its bottom 1/5 of the screen (42px HUD +
//    127px board = 169 = round(844 / 5), top-cropped); Zone B's full 631px band exactly
//    fills the screen down to y=844.
// Zone A's band: 42 (HUD chrome) + 521, where 42 + 521 = round(844 × 2/3).
// Zone B's band: 844 − 42 − 127 − 44 = 631, where 42 + 127 = round(844 / 5).
const ZONE_A_HEIGHT = 42 + 521;
const ZONE_C_HEIGHT = 44;
const ZONE_B_HEIGHT = 631;

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
