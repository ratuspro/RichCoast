import type Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import * as Layout from '../core/Layout';
import { BallQueue } from './BallQueue';

/**
 * Zone A — the merge board (Dev 1).
 *
 * Skeleton: renders the zone label + a next-ball preview, and owns the drop queue
 * and merge rules. The gameplay — aim/drop input, spawning physics balls,
 * merge-on-collision, the overflow→game-over check — is left as the seams below.
 *
 * Note: Zone A is self-contained (no event bus). It emits nothing on the cross-zone
 * contract — Zone C reads its ball bodies straight from the shared world. Wire the
 * bus in only if a future internal signal (e.g. game-over) actually needs it.
 */
export class ZoneASystem implements GameSystem {
  private readonly queue = new BallQueue();

  create(scene: Phaser.Scene): void {
    this.drawLabel(scene);

    // The board lives entirely above Layout.zoneC.y. Implementation seams:
    // TODO(zoneA): drag across the top to aim; release to drop this.queue.pop().
    // TODO(zoneA): spawn a Matter circle per drop and tag its body so Zone C can
    //   read it on a world query:  body.ballData = { value: tierToValue(tier), tier }
    //   (see BallBodyData in core/contracts). Keep balls above the divider.
    // TODO(zoneA): on same-tier contact use canMerge()/mergedTier() from MergeLogic
    //   to replace the pair with one next-tier ball. Merging scores nothing.
    // TODO(zoneA): grow the preview below into the real next-ball indicator.
    // TODO(zoneA): game over when any resting ball crosses Layout.zoneA.y.
  }

  update(_time: number, _delta: number): void {
    // TODO(zoneA): aim indicator / overflow checks.
  }

  private drawLabel(scene: Phaser.Scene): void {
    const r = Layout.zoneA;
    scene.add
      .text(
        r.x + r.width / 2,
        r.y + r.height / 2,
        `ZONE A\nmerge\n\nnext: t${this.queue.peek()} -> t${this.queue.peekNext()}`,
        {
          fontFamily: 'monospace',
          fontSize: '20px',
          color: '#566080',
          align: 'center',
        },
      )
      .setOrigin(0.5)
      .setAlpha(0.5);
  }
}
