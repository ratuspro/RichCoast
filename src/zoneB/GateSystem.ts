import type Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';

/** A moving ×N gate: a ball that hits it is replaced by N balls of the same value. */
export interface GateDef {
  x: number;
  y: number;
  /** N — how many copies the ball becomes. */
  multiplier: number;
}

/**
 * Zone B gates (Dev 2). Skeleton: defines what a gate is and marks where motion
 * and splitting will live. No bodies yet.
 */
export class GateSystem implements GameSystem {
  create(_scene: Phaser.Scene): void {
    // TODO(zoneB): build gate bodies from a GateDef[] layout (static sensors or
    //   kinematic movers). Scripted oscillation vs procedural motion is a SPEC open Q.
  }

  update(_time: number, _delta: number): void {
    // TODO(zoneB): advance gate motion.
    // TODO(zoneB): on ball↔gate contact, replace the ball with `multiplier` copies of
    //   the same value — each copy is a ZoneBSystem.onBallSpawned() — cascading into
    //   further gates.
  }
}
