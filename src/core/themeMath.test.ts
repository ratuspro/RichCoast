import { describe, expect, it } from 'vitest';
import { lerpColor, lerpPalette } from './themeMath';
import { PALETTES } from './Theme';

describe('lerpColor', () => {
  it('returns the endpoints at t=0 and t=1', () => {
    expect(lerpColor(0x123456, 0xfedcba, 0)).toBe(0x123456);
    expect(lerpColor(0x123456, 0xfedcba, 1)).toBe(0xfedcba);
  });

  it('mixes each RGB channel independently at the midpoint', () => {
    // 0x00→0xff = 0x80 (rounded), 0xff→0x00 = 0x80, 0x40→0xc0 = 0x80
    expect(lerpColor(0x00ff40, 0xff00c0, 0.5)).toBe(0x808080);
  });

  it('does not bleed between channels', () => {
    // Only the blue channel differs; red/green must be untouched at any t.
    expect(lerpColor(0xa1b200, 0xa1b2ff, 0.25)).toBe(0xa1b240);
  });
});

describe('lerpPalette', () => {
  it('blends every key of the two palettes', () => {
    const mid = lerpPalette(PALETTES.workshop, PALETTES.night, 0.5);
    for (const key of Object.keys(PALETTES.workshop) as (keyof typeof PALETTES.workshop)[]) {
      expect(mid[key]).toBe(lerpColor(PALETTES.workshop[key], PALETTES.night[key], 0.5));
    }
  });

  it('reproduces the source palette at t=0', () => {
    expect(lerpPalette(PALETTES.workshop, PALETTES.dusk, 0)).toEqual(PALETTES.workshop);
  });
});

describe('PALETTES', () => {
  it('every authored palette carries the exact workshop key set', () => {
    const keys = Object.keys(PALETTES.workshop).sort();
    for (const [name, palette] of Object.entries(PALETTES)) {
      expect(Object.keys(palette).sort(), `palette "${name}"`).toEqual(keys);
    }
  });

  it('keeps brassBright brighter than brass in every palette', () => {
    const luminance = (c: number) =>
      0.2126 * ((c >> 16) & 0xff) + 0.7152 * ((c >> 8) & 0xff) + 0.0722 * (c & 0xff);
    for (const [name, palette] of Object.entries(PALETTES)) {
      expect(luminance(palette.brassBright), `palette "${name}"`).toBeGreaterThan(
        luminance(palette.brass),
      );
    }
  });
});
