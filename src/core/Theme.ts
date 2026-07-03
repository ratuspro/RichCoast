/**
 * The "Bright Workshop" environment palette — every player-facing surface colour that
 * isn't a ball material (those live in `Materials.ts`). One named table instead of hex
 * literals scattered across the zones, so the identity stays coherent and re-tunable.
 *
 * The world is a warm, light toy workshop: warm-paper backdrop, light-pine structure
 * (walls, rails, signs), brass accents (rules, hinges, markers), warm-brown ink for
 * text. Feedback colours (danger red, the game-over scrim) are the only departures.
 *
 * Shared `core/` file: both zones style themselves from it (both-devs-agree to change).
 */
export const Theme = {
  // — Backdrop bands —
  /** Page + canvas background, and the Zone B band. */
  paper: 0xf2e7d5,
  /** Zone A band — slightly lighter, the "workbench top" where the action is. */
  paperZoneA: 0xf7efe0,
  /** Zone C band — slightly deeper, so the trap-door band reads as a slot. */
  paperZoneC: 0xe9dcc4,

  // — Structure (wood + brass) —
  /** Light pine: walls, funnel, rails, gate signs. */
  pine: 0xd9b07c,
  /** Darker pine: grain lines, structural edges. */
  pineDark: 0xa87e4f,
  /** Deepest wood tone: outlines, inner shadow lines, dividers. */
  pineShadow: 0x7d5a33,
  /** Brass accents: HUD rule, door hinges, dim markers. */
  brass: 0xc9973f,
  /** Polished brass: the lit marker, highlights, button strokes. */
  brassBright: 0xf0c060,

  // — Ink & panels —
  /** Primary warm-brown text on light surfaces. */
  ink: 0x3f3428,
  /** Muted labels ("NEXT", units). */
  inkSoft: 0x8a7a64,
  /** Cream panel fill (HUD band, queue row chrome). */
  cream: 0xfdf6ea,

  // — Feedback —
  /** Death-line / warning red (chosen to read on the light paper). */
  danger: 0xd64545,
  /** Warm dark game-over scrim (with ~0.85 alpha). */
  scrim: 0x2b2115,
} as const;
