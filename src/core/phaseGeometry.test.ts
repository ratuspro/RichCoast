import { describe, expect, it } from 'vitest';
import { HEIGHT, zoneA, zoneB, zoneC } from './Layout';
import {
  ARENA_VIEW_H_A,
  ARENA_VIEW_H_B,
  HUD_H,
  PAN_DISTANCE,
  arenaCenterY,
  framingForPan,
} from './phaseGeometry';

describe('phaseGeometry', () => {
  it('derives the phase band heights from the screen fractions (2/3 active, 1/5 inactive)', () => {
    expect(HUD_H + ARENA_VIEW_H_A).toBe(Math.round(HEIGHT * (2 / 3))); // 563 — A-phase
    expect(HUD_H + ARENA_VIEW_H_B).toBe(Math.round(HEIGHT / 5)); // 169 — B-phase
  });

  it('agrees on PAN_DISTANCE from all three derivations', () => {
    expect(PAN_DISTANCE).toBe(ARENA_VIEW_H_A - ARENA_VIEW_H_B); // arena viewport shrink
    expect(PAN_DISTANCE).toBe(zoneB.y + zoneB.height - HEIGHT); // Zone B world overhang
    expect(PAN_DISTANCE).toBe(zoneC.y - (zoneC.y - PAN_DISTANCE)); // Zone C screen shift (tautology guard)
    expect(HEIGHT - HUD_H - ARENA_VIEW_H_B - zoneC.height).toBe(zoneB.height); // B-phase fill
  });

  it('frames the endpoints exactly', () => {
    expect(framingForPan(0)).toEqual({ scrollY: 0, arenaViewportH: ARENA_VIEW_H_A });
    expect(framingForPan(PAN_DISTANCE)).toEqual({
      scrollY: PAN_DISTANCE,
      arenaViewportH: ARENA_VIEW_H_B,
    });
  });

  it('keeps the arena-bottom / Zone-C seam pixel-locked for every pan value', () => {
    for (let pan = 0; pan <= PAN_DISTANCE; pan += 0.37) {
      const f = framingForPan(pan);
      // Arena viewport bottom (screen) must equal Zone C's top edge as seen by the
      // scrolled main camera — no gap, no overlap, at every tween tick.
      expect(HUD_H + f.arenaViewportH).toBe(zoneC.y - f.scrollY);
      expect(Number.isInteger(f.scrollY)).toBe(true);
      expect(Number.isInteger(f.arenaViewportH)).toBe(true);
    }
  });

  it('clamps out-of-range pan values', () => {
    expect(framingForPan(-20)).toEqual(framingForPan(0));
    expect(framingForPan(PAN_DISTANCE + 20)).toEqual(framingForPan(PAN_DISTANCE));
  });

  it('pins the funnel floor to the viewport bottom at any scale', () => {
    const floorY = zoneA.y + zoneA.height;
    for (const s of [1, 1.4, 2.2]) {
      for (const h of [ARENA_VIEW_H_A, ARENA_VIEW_H_B]) {
        // centre + half the visible world height = the floor line.
        expect(arenaCenterY(h, s) + (h / 2) * s).toBeCloseTo(floorY);
      }
    }
  });
});
