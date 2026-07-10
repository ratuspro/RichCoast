/**
 * The environment palette — every player-facing surface colour that isn't a ball
 * material (those live in `Materials.ts` and are the tier-identity signal, so they
 * NEVER change with the theme). One named table instead of hex literals scattered
 * across the zones, so the identity stays coherent and re-tunable.
 *
 * Since the milestone colour-swap feature, `Theme` is the *active* palette — a
 * mutable object whose values are re-written by `applyPalette` as the run crosses
 * milestones (the `ThemeDirector` cross-fades it during the milestone zoom and
 * broadcasts `THEME_CHANGED` so baked surfaces restyle). Consumers keep reading
 * `Theme.brass` etc. as before; anything that bakes a colour into a Graphics /
 * Rectangle / Text at create() must also re-apply it on `THEME_CHANGED`.
 *
 * The authored palettes live in `PALETTES`. Authoring rules every palette must
 * hold to: `ink`/`inkSoft` contrast against `paper*`/`cream`, `danger` reads
 * against `paper`, and `brassBright` is brighter than `brass` (unit-checked in
 * `themeMath.test.ts`). Starting values are a tuning pass, like `tightness`.
 *
 * Shared `core/` file: both zones style themselves from it (both-devs-agree to change).
 */

export interface ThemePalette {
  // — Backdrop bands —
  /** Page + canvas background, and the Zone B band. */
  paper: number;
  /** Zone A band — slightly lighter, the "workbench top" where the action is. */
  paperZoneA: number;
  /** Zone C band — slightly deeper, so the trap-door band reads as a slot. */
  paperZoneC: number;

  // — Structure (wood + accents) —
  /** Light pine: walls, funnel, rails, gate signs. */
  pine: number;
  /** Darker pine: grain lines, structural edges. */
  pineDark: number;
  /** Deepest wood tone: outlines, inner shadow lines, dividers. */
  pineShadow: number;
  /** Brass accents: HUD rule, door hinges, dim markers, score-bar fill. */
  brass: number;
  /** Polished brass: the lit marker, highlights, button strokes. */
  brassBright: number;

  // — Ink & panels —
  /** Primary text on panel surfaces. */
  ink: number;
  /** Muted labels ("NEXT", units). */
  inkSoft: number;
  /** Panel fill (HUD band, queue row chrome). */
  cream: number;

  // — Feedback & fixtures —
  /** Death-line / warning red (must read against `paper`). */
  danger: number;
  /** Game-over scrim (with ~0.85 alpha). */
  scrim: number;
  /** Painted face of Zone B's high-multiplier (≥4) gate signs. */
  gatePaint: number;
  /** Zone B score-bar groove background. */
  groove: number;
}

/**
 * The authored milestone palettes. `workshop` is the boot look; `progression.json`
 * names one of the others on each draw-window-shift stage (author-then-hold past
 * the last). Ordered as the run meets them: workshop → dusk → night → dawn → gilded.
 */
export const PALETTES = {
  /** The base "Bright Workshop": warm, light toy workshop. */
  workshop: {
    paper: 0xf2e7d5,
    paperZoneA: 0xf7efe0,
    paperZoneC: 0xe9dcc4,
    pine: 0xd9b07c,
    pineDark: 0xa87e4f,
    pineShadow: 0x7d5a33,
    brass: 0xc9973f,
    brassBright: 0xf0c060,
    ink: 0x3f3428,
    inkSoft: 0x8a7a64,
    cream: 0xfdf6ea,
    danger: 0xd64545,
    scrim: 0x2b2115,
    gatePaint: 0x6aa84f,
    groove: 0xe0d2b8,
  },
  /** Sundown copper: the workshop at dusk — deep enough that the first-ever
   *  milestone swap reads instantly (the original amber was too close to workshop). */
  dusk: {
    paper: 0xe0a970,
    paperZoneA: 0xeabd85,
    paperZoneC: 0xd2975a,
    pine: 0xb26f43,
    pineDark: 0x84492a,
    pineShadow: 0x5c2f1a,
    brass: 0xb85c2e,
    brassBright: 0xf28448,
    ink: 0x3c2113,
    inkSoft: 0x82573c,
    cream: 0xf4d8ab,
    danger: 0xcc2f2f,
    scrim: 0x2a150c,
    gatePaint: 0x718f3a,
    groove: 0xd3a468,
  },
  /** Deep night blues: dark paper, light ink, moonlit-silver accents. */
  night: {
    paper: 0x243046,
    paperZoneA: 0x2b3952,
    paperZoneC: 0x1d2839,
    pine: 0x3e5372,
    pineDark: 0x2c3e59,
    pineShadow: 0x18243a,
    brass: 0x8ea6c4,
    brassBright: 0xcfe0f2,
    ink: 0xe8eef8,
    inkSoft: 0x9fb0c8,
    cream: 0x35486a,
    danger: 0xff6b5e,
    scrim: 0x060a12,
    gatePaint: 0x5fae7a,
    groove: 0x1a2436,
  },
  /** Pale rose morning: soft pinks, rose-gold accents. */
  dawn: {
    paper: 0xf6e3e3,
    paperZoneA: 0xfaeceb,
    paperZoneC: 0xecd2d3,
    pine: 0xd898a0,
    pineDark: 0xac6a76,
    pineShadow: 0x7c4551,
    brass: 0xcf7f6a,
    brassBright: 0xf5ac92,
    ink: 0x4a2f38,
    inkSoft: 0x97707c,
    cream: 0xfdf3f1,
    danger: 0xc93a4d,
    scrim: 0x2a161c,
    gatePaint: 0x6fa668,
    groove: 0xe8cfd0,
  },
  /** Rich gold on dark walnut: the endgame look. */
  gilded: {
    paper: 0x4a3421,
    paperZoneA: 0x56402a,
    paperZoneC: 0x3c2917,
    pine: 0x8a6534,
    pineDark: 0x6a4a22,
    pineShadow: 0x2e1e0d,
    brass: 0xd4a437,
    brassBright: 0xffd766,
    ink: 0xf7e9c8,
    inkSoft: 0xc9ab7a,
    cream: 0x5f4527,
    danger: 0xff5c4d,
    scrim: 0x120b04,
    gatePaint: 0x8fae4a,
    groove: 0x33230f,
  },
} as const satisfies Record<string, ThemePalette>;

export type PaletteName = keyof typeof PALETTES;

/** The ACTIVE palette. Mutable — always read at use time, never cache values across frames. */
export const Theme: ThemePalette = { ...PALETTES.workshop };

/** Re-point the active palette (mutates `Theme` in place so every import sees it).
 *  (For Text styles, format with `hexColor` from `Materials.ts`.) */
export function applyPalette(palette: ThemePalette): void {
  Object.assign(Theme, palette);
}
