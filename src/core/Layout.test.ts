import { describe, expect, it } from 'vitest';
import { HEIGHT, WIDTH, zoneA, zoneB, zoneBEntry, zoneC } from './Layout';
import { ARENA_VIEW_H_B, HUD_H, PAN_DISTANCE } from './phaseGeometry';

describe('Layout', () => {
  it('stacks the zones top-to-bottom, each starting where the previous ends', () => {
    expect(zoneA.y).toBe(0);
    expect(zoneC.y).toBe(zoneA.y + zoneA.height);
    expect(zoneB.y).toBe(zoneC.y + zoneC.height);
  });

  it('overhangs the screen bottom by exactly the phase-pan distance', () => {
    // The world is taller than the screen: the B-phase camera scrolls down PAN_DISTANCE
    // to bring Zone B's bottom edge flush with the screen bottom.
    expect(zoneB.y + zoneB.height - HEIGHT).toBe(PAN_DISTANCE);
  });

  it('sizes Zone B to exactly fill the B-phase frame', () => {
    // Screen = HUD + top-cropped Zone A band + Zone C + all of Zone B.
    expect(HEIGHT - HUD_H - ARENA_VIEW_H_B - zoneC.height).toBe(zoneB.height);
  });

  it('spans the full width in every zone', () => {
    for (const z of [zoneA, zoneB, zoneC]) {
      expect(z.x).toBe(0);
      expect(z.width).toBe(WIDTH);
    }
  });

  it('puts the Zone B entry on the top edge of Zone B, within its bounds', () => {
    expect(zoneBEntry.y).toBe(zoneB.y);
    expect(zoneBEntry.x).toBeGreaterThanOrEqual(zoneB.x);
    expect(zoneBEntry.x).toBeLessThanOrEqual(zoneB.x + zoneB.width);
  });
});
