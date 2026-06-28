import type Phaser from 'phaser';
import type { GameSystem } from '../core/contracts';
import * as Layout from '../core/Layout';
import { AimController } from './AimController';
import { BallFactory } from './BallFactory';
import { Board } from './Board';

/**
 * Zone A — the Suika-style merge board (Dev 1).
 *
 * Composition root: wires the procedural {@link BallFactory}, the physics
 * {@link Board} (merges + blast + overflow) and the drag-to-aim {@link AimController}.
 * Stays self-contained — it emits nothing on the bus; Zone C reads its ball bodies
 * straight from the shared Matter world.
 *
 * Game-over is local: the frozen contract has no GAME_OVER event, so on overflow we
 * just freeze Zone A's input and paint an overlay — Zones B/C keep running.
 */
export class ZoneASystem implements GameSystem {
  private scene?: Phaser.Scene;
  private board?: Board;
  private aim?: AimController;
  private over = false;

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    const factory = new BallFactory(scene);
    const board = new Board(scene, factory, () => this.handleGameOver());
    const aim = new AimController(scene, factory, (x, tier) => board.spawnDropped(x, tier));
    this.board = board;
    this.aim = aim;
  }

  update(_time: number, delta: number): void {
    if (this.over) return;
    this.board?.update(delta);
  }

  destroy(): void {
    this.aim?.destroy();
    this.board?.destroy();
  }

  private handleGameOver(): void {
    if (this.over) return;
    this.over = true;
    this.aim?.disable();
    this.drawGameOverOverlay();
  }

  private drawGameOverOverlay(): void {
    const scene = this.scene;
    if (!scene) return;
    const r = Layout.zoneA;
    const cx = r.x + r.width / 2;
    const cy = r.y + r.height / 2;
    scene.add.rectangle(cx, cy, r.width, r.height, 0x0b0d12, 0.66).setDepth(900);
    scene.add
      .text(cx, cy, 'GAME OVER', {
        fontFamily: 'monospace',
        fontSize: '40px',
        color: '#ff6d6d',
        fontStyle: 'bold',
        align: 'center',
      })
      .setOrigin(0.5)
      .setDepth(901);
  }
}
