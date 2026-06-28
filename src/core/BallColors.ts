/**
 * Single source of truth for ball colours, shared by Zone A and Zone B.
 *
 * Both zones regenerate their own ball textures, so without one shared palette they
 * drift and a ball transferred A→B changes colour. Zones must not import each other
 * (TECH_SPEC), so the palette lives here in `core/`. One flat colour per tier;
 * index = tier-1. Must have TIER_COUNT (10) entries.
 *
 * "Jewel Tones" — a deep, premium gemstone progression that pops against the dark
 * navy backdrop. The four spawnable low tiers (teal/sapphire/amethyst/orchid) are
 * spaced for maximum mutual contrast: they're seen most, and in Zone B they're tiny
 * unlabelled dots where colour is the only identity.
 */
export const TIER_COLORS: readonly number[] = [
  0x1fb6a6, 0x3b82f6, 0x6d5be0, 0xc44cd9, 0xec4f6b, 0xf2803c, 0xe8b53a, 0x8fc93a,
  0x2fb56b, 0xdcc27a,
];

/** Colour for a tier (1-based), wrapping safely past the table end. */
export function colorForTier(tier: number): number {
  return TIER_COLORS[(tier - 1) % TIER_COLORS.length] ?? TIER_COLORS[0];
}

/** 0xRRGGBB number → `#rrggbb` CSS string for the Canvas 2D API. */
export function hexColor(rgb: number): string {
  return `#${rgb.toString(16).padStart(6, '0')}`;
}
