import { describe, expect, it } from 'vitest';
import { HEIGHT, WIDTH, zoneA, zoneB, zoneBEntry, zoneC } from './Layout';

describe('Layout', () => {
  it('stacks the zones top-to-bottom, each starting where the previous ends', () => {
    expect(zoneA.y).toBe(0);
    expect(zoneC.y).toBe(zoneA.y + zoneA.height);
    expect(zoneB.y).toBe(zoneC.y + zoneC.height);
  });

  it('tiles the full screen height with no gap or overlap', () => {
    expect(zoneA.height + zoneC.height + zoneB.height).toBe(HEIGHT);
    expect(zoneB.y + zoneB.height).toBe(HEIGHT);
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
