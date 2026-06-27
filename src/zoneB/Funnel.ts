import type Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';

/**
 * Zone B funnel (Dev 2). Skeleton: where balls drain out and cash in. This is the
 * only place a ball's value becomes score.
 */
export class Funnel implements GameSystem {
  create(_scene: Phaser.Scene): void {
    // TODO(zoneB): build the funnel geometry at the bottom of Layout.zoneB.
  }

  update(_time: number, _delta: number): void {
    // TODO(zoneB): detect each ball entering the funnel; for each, call
    //   ZoneBSystem.addScore(value) then ZoneBSystem.onBallDrained().
  }
}
