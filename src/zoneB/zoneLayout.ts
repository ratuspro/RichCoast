export interface StaticGate {
  type: 'static';
  cx: number; cy: number;
  angle: number;   // radians; 0 = horizontal
  length: number;
  multiplier: number;
}
export interface TranslatingGate {
  type: 'translating';
  ax: number; ay: number;
  bx: number; by: number;
  angle: number;
  length: number;
  multiplier: number;
  periodMs: number;  // ms for one full A→B→A cycle
}
export interface RotatingGate {
  type: 'rotating';
  cx: number; cy: number;
  length: number;
  multiplier: number;
  speedRadPerMs: number;  // positive = clockwise
}
export type GateDef = StaticGate | TranslatingGate | RotatingGate;

export interface CollectorDef {
  x: number; y: number;
  width: number; height: number;
  scoreMultiplier: number;  // ball.value × scoreMultiplier added to score
}
export interface WallDef {
  x1: number; y1: number;
  x2: number; y2: number;
  thickness?: number;   // default 6
  /** Fill the area between this rail and the Zone B bottom with solid wood (funnel ramps). */
  fillBelow?: boolean;
}
export interface ZoneBLayout {
  gates: GateDef[];
  collectors: CollectorDef[];
  walls: WallDef[];
}

// Two layouts modelled on the reference screenshots: stacked horizontal multiplier shelves
// (static gates) split by vertical/diagonal guide rails (walls), funnelling into a bottom cup
// (collector). One is chosen at random per run via pickRandomLayout(). All y values are
// absolute (zone B now spans y=551..1238, x=0..390; the cascade is still authored for the
// previous 607..1238 band — the extra 56px the band gained from Zone A is deliberate
// free-fall headroom above the first shelf, not a remap). Multipliers are tuned for
// balance (≤4), so the geometry matches the images while ball cascades stay sane.

// The two funnel ramps that feed the single bottom collector — shared by both layouts.
const FUNNEL_RAMPS: WallDef[] = [
  { x1: 0,   y1: 1129, x2: 110, y2: 1233, fillBelow: true },
  { x1: 390, y1: 1129, x2: 280, y2: 1233, fillBelow: true },
];
// The drain sits right at the score bar (bar top ≈ y=1222): its top is one ball-radius above
// the bar so a ball vanishes just as its bottom meets the bar, not floating above it.
const BOTTOM_COLLECTOR: CollectorDef = { x: 110, y: 1212, width: 170, height: 26, scoreMultiplier: 1 };

// LAYOUT_1 — three-segment top row narrowing to a central bottom gate.
// Row-1 gate extents are chosen so a ~16px gap sits between adjacent segments; a vertical
// post is centred in each gap and starts at the bar's centreline (cy=720), so the gates read
// as framed between two walls (matching the reference).
export const LAYOUT_1: ZoneBLayout = {
  gates: [
    // Row 1 — gaps at x∈[100,116] and x∈[248,264].
    { type: 'static', cx: 54,  cy: 720, angle: 0, length: 92,  multiplier: 4 },
    { type: 'static', cx: 182, cy: 720, angle: 0, length: 132, multiplier: 3 },
    { type: 'static', cx: 323, cy: 720, angle: 0, length: 118, multiplier: 2 },
    // Row 2
    { type: 'static', cx: 45,  cy: 901, angle: 0, length: 85,  multiplier: 2 },
    { type: 'static', cx: 195, cy: 901, angle: 0, length: 110, multiplier: 2 },
    { type: 'static', cx: 348, cy: 901, angle: 0, length: 80,  multiplier: 2 },
    // Row 3
    { type: 'static', cx: 195, cy: 1051, angle: 0, length: 90,  multiplier: 2 },
  ],
  collectors: [BOTTOM_COLLECTOR],
  walls: [
    // Row-1 gate dividers, centred in the gaps and framing the three top gates.
    { x1: 108, y1: 715, x2: 108, y2: 810 },
    { x1: 256, y1: 715, x2: 256, y2: 810 },
    // Frame posts rising above the right (X2) gate, as in the reference.
    { x1: 264, y1: 677, x2: 264, y2: 720 },
    { x1: 382, y1: 677, x2: 382, y2: 720 },
    // Left outer rail: straight down the left side, then a diagonal converging to centre.
    { x1: 75,  y1: 752, x2: 75,  y2: 921 },
    { x1: 75,  y1: 921, x2: 165, y2: 1051 },
    // Right outer rail: straight down the right side, then a diagonal converging to centre.
    { x1: 320, y1: 752, x2: 320, y2: 921 },
    { x1: 320, y1: 921, x2: 230, y2: 1051 },
    // Post above the row-2 centre gate.
    { x1: 140, y1: 854, x2: 140, y2: 901 },
    // Small end caps on the row-3 centre gate.
    { x1: 150, y1: 1051, x2: 150, y2: 1079 },
    { x1: 240, y1: 1051, x2: 240, y2: 1079 },
    ...FUNNEL_RAMPS,
  ],
};

// LAYOUT_2 — offset rows with zig-zag diagonal rails.
// Row-1 gaps at x∈[195,211] and x∈[293,309] each hold a vertical post; the upper diagonal
// continues straight off the bottom of the right post, so no rail floats free of a junction.
export const LAYOUT_2: ZoneBLayout = {
  gates: [
    // Row 1 — gaps at x∈[195,211] and x∈[293,309].
    { type: 'static', cx: 105, cy: 720, angle: 0, length: 180, multiplier: 4 },
    { type: 'static', cx: 252, cy: 720, angle: 0, length: 82,  multiplier: 3 },
    { type: 'static', cx: 348, cy: 720, angle: 0, length: 78,  multiplier: 4 },
    // Row 2
    { type: 'static', cx: 95,  cy: 908, angle: 0, length: 170, multiplier: 4 },
    { type: 'static', cx: 250, cy: 908, angle: 0, length: 85,  multiplier: 2 },
    { type: 'static', cx: 345, cy: 908, angle: 0, length: 85,  multiplier: 3 },
    // Row 3
    { type: 'static', cx: 100, cy: 1051, angle: 0, length: 120, multiplier: 2 },
    { type: 'static', cx: 300, cy: 1051, angle: 0, length: 140, multiplier: 3 },
  ],
  collectors: [BOTTOM_COLLECTOR],
  walls: [
    // Row-1 gate dividers, centred in the gaps and rising slightly above the bars.
    { x1: 203, y1: 681, x2: 203, y2: 752 },
    { x1: 301, y1: 681, x2: 301, y2: 752 },
    // Upper diagonal "\" off the left divider, down-left toward the row-2 left gate.
    { x1: 203, y1: 752, x2: 150, y2: 869 },
    // Tall right rail: the right divider runs straight down past row 2 into row 3,
    // forming the right wall of the central channel.
    { x1: 301, y1: 752, x2: 301, y2: 1038 },
    // Lower zig-zag diagonal from below the row-2 centre gate down to the row-3 right gate.
    { x1: 207, y1: 921, x2: 250, y2: 1038 },
    // Short vertical cap on the far left of row 3.
    { x1: 40,  y1: 980, x2: 40,  y2: 1053 },
    ...FUNNEL_RAMPS,
  ],
};

export const ZONE_B_LAYOUTS = [LAYOUT_1, LAYOUT_2] as const;

/** Pick one of the Zone B layouts at random (called once per run, at construction). */
export function pickRandomLayout(): ZoneBLayout {
  return ZONE_B_LAYOUTS[Math.floor(Math.random() * ZONE_B_LAYOUTS.length)];
}
