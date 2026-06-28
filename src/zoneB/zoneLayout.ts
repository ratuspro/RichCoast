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
}
export interface ZoneBLayout {
  gates: GateDef[];
  collectors: CollectorDef[];
  walls: WallDef[];
}

// Two layouts modelled on the reference screenshots: stacked horizontal multiplier shelves
// (static gates) split by vertical/diagonal guide rails (walls), funnelling into a bottom cup
// (collector). One is chosen at random per run via pickRandomLayout(). All y values are
// absolute (zone B spans y=358..844, x=0..390). Multipliers are tuned for balance (≤4), so
// the geometry matches the images while ball cascades stay sane.

// The two funnel ramps that feed the single bottom collector — shared by both layouts.
const FUNNEL_RAMPS: WallDef[] = [
  { x1: 0,   y1: 760, x2: 110, y2: 840 },
  { x1: 390, y1: 760, x2: 280, y2: 840 },
];
const BOTTOM_COLLECTOR: CollectorDef = { x: 110, y: 810, width: 170, height: 24, scoreMultiplier: 1 };

// LAYOUT_1 — three-segment top row narrowing to a central bottom gate.
// Row-1 gate extents are chosen so a ~16px gap sits between adjacent segments; a vertical
// post is centred in each gap and starts at the bar's centreline (cy=445), so the gates read
// as framed between two walls (matching the reference).
export const LAYOUT_1: ZoneBLayout = {
  gates: [
    // Row 1 — gaps at x∈[100,116] and x∈[248,264].
    { type: 'static', cx: 54,  cy: 445, angle: 0, length: 92,  multiplier: 4 },
    { type: 'static', cx: 182, cy: 445, angle: 0, length: 132, multiplier: 3 },
    { type: 'static', cx: 323, cy: 445, angle: 0, length: 118, multiplier: 2 },
    // Row 2
    { type: 'static', cx: 45,  cy: 585, angle: 0, length: 85,  multiplier: 2 },
    { type: 'static', cx: 195, cy: 585, angle: 0, length: 110, multiplier: 2 },
    { type: 'static', cx: 348, cy: 585, angle: 0, length: 80,  multiplier: 2 },
    // Row 3
    { type: 'static', cx: 195, cy: 700, angle: 0, length: 90,  multiplier: 2 },
  ],
  collectors: [BOTTOM_COLLECTOR],
  walls: [
    // Row-1 gate dividers, centred in the gaps and framing the three top gates.
    { x1: 108, y1: 441, x2: 108, y2: 515 },
    { x1: 256, y1: 441, x2: 256, y2: 515 },
    // Frame posts rising above the right (X2) gate, as in the reference.
    { x1: 264, y1: 412, x2: 264, y2: 445 },
    { x1: 382, y1: 412, x2: 382, y2: 445 },
    // Left outer rail: straight down the left side, then a diagonal converging to centre.
    { x1: 75,  y1: 470, x2: 75,  y2: 600 },
    { x1: 75,  y1: 600, x2: 165, y2: 700 },
    // Right outer rail: straight down the right side, then a diagonal converging to centre.
    { x1: 320, y1: 470, x2: 320, y2: 600 },
    { x1: 320, y1: 600, x2: 230, y2: 700 },
    // Post above the row-2 centre gate.
    { x1: 140, y1: 548, x2: 140, y2: 585 },
    // Small end caps on the row-3 centre gate.
    { x1: 150, y1: 700, x2: 150, y2: 722 },
    { x1: 240, y1: 700, x2: 240, y2: 722 },
    ...FUNNEL_RAMPS,
  ],
};

// LAYOUT_2 — offset rows with zig-zag diagonal rails.
// Row-1 gaps at x∈[195,211] and x∈[293,309] each hold a vertical post; the upper diagonal
// continues straight off the bottom of the right post, so no rail floats free of a junction.
export const LAYOUT_2: ZoneBLayout = {
  gates: [
    // Row 1 — gaps at x∈[195,211] and x∈[293,309].
    { type: 'static', cx: 105, cy: 445, angle: 0, length: 180, multiplier: 4 },
    { type: 'static', cx: 252, cy: 445, angle: 0, length: 82,  multiplier: 3 },
    { type: 'static', cx: 348, cy: 445, angle: 0, length: 78,  multiplier: 4 },
    // Row 2
    { type: 'static', cx: 95,  cy: 590, angle: 0, length: 170, multiplier: 4 },
    { type: 'static', cx: 250, cy: 590, angle: 0, length: 85,  multiplier: 2 },
    { type: 'static', cx: 345, cy: 590, angle: 0, length: 85,  multiplier: 3 },
    // Row 3
    { type: 'static', cx: 100, cy: 700, angle: 0, length: 120, multiplier: 2 },
    { type: 'static', cx: 300, cy: 700, angle: 0, length: 140, multiplier: 3 },
  ],
  collectors: [BOTTOM_COLLECTOR],
  walls: [
    // Row-1 gate dividers, centred in the gaps and rising slightly above the bars.
    { x1: 203, y1: 415, x2: 203, y2: 470 },
    { x1: 301, y1: 415, x2: 301, y2: 470 },
    // Upper diagonal "\" off the left divider, down-left toward the row-2 left gate.
    { x1: 203, y1: 470, x2: 150, y2: 560 },
    // Tall right rail: the right divider runs straight down past row 2 into row 3,
    // forming the right wall of the central channel.
    { x1: 301, y1: 470, x2: 301, y2: 690 },
    // Lower zig-zag diagonal from below the row-2 centre gate down to the row-3 right gate.
    { x1: 207, y1: 600, x2: 250, y2: 690 },
    // Short vertical cap on the far left of row 3.
    { x1: 40,  y1: 645, x2: 40,  y2: 702 },
    ...FUNNEL_RAMPS,
  ],
};

export const ZONE_B_LAYOUTS = [LAYOUT_1, LAYOUT_2] as const;

/** Pick one of the Zone B layouts at random (called once per run, at construction). */
export function pickRandomLayout(): ZoneBLayout {
  return ZONE_B_LAYOUTS[Math.floor(Math.random() * ZONE_B_LAYOUTS.length)];
}
