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

// Initial layout. All y values are absolute (zone B starts at y=358).
export const INITIAL_LAYOUT: ZoneBLayout = {
  gates: [
    // Static, horizontal, centre of arena
    { type: 'static', cx: 195, cy: 580, angle: 0, length: 90, multiplier: 2 },
    // Translating gate — slides left↔right across the left half
    { type: 'translating', ax: 70, ay: 660, bx: 180, by: 660, angle: 0, length: 70, multiplier: 2, periodMs: 2200 },
    // Rotating gate — spins around a fixed pivot on the right side
    { type: 'rotating', cx: 310, cy: 720, length: 65, multiplier: 3, speedRadPerMs: 0.0025 },
  ],
  collectors: [
    // Single wide collector at the bottom, between where the two ramps converge
    { x: 110, y: 820, width: 170, height: 24, scoreMultiplier: 1 },
  ],
  walls: [
    // Steeper diagonal ramps (~36°) funnelling balls into the single collector
    { x1: 0,   y1: 760, x2: 110, y2: 840 },
    { x1: 390, y1: 760, x2: 280, y2: 840 },
  ],
};
