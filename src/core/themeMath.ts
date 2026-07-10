/**
 * Pure colour math for the milestone palette cross-fade. Phaser-free so the
 * ThemeDirector's blend is unit-testable in plain Node.
 */
import type { ThemePalette } from './Theme';

/** Blend two 0xRRGGBB colours per channel; t in [0,1], rounded to whole channels. */
export function lerpColor(a: number, b: number, t: number): number {
  const mix = (shift: number) => {
    const ca = (a >> shift) & 0xff;
    const cb = (b >> shift) & 0xff;
    return Math.round(ca + (cb - ca) * t);
  };
  return (mix(16) << 16) | (mix(8) << 8) | mix(0);
}

/** Blend every key of two palettes — one tick of the milestone cross-fade. */
export function lerpPalette(a: ThemePalette, b: ThemePalette, t: number): ThemePalette {
  const out = {} as ThemePalette;
  for (const key of Object.keys(a) as (keyof ThemePalette)[]) {
    out[key] = lerpColor(a[key], b[key], t);
  }
  return out;
}
