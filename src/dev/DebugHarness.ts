import Phaser from 'phaser';
import { GameEvent, tierToValue, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { isDebug } from '../core/DebugMode';

const MAX_LOG_LINES = 12;
const DROP_MIN_TIER = 1;
const DROP_MAX_TIER = 6;

/**
 * Debug overlay shown when debug mode is active (?debug=2 or D key).
 * Provides a DROP button (also SPACE) that fires BALL_DROPPED directly on the
 * bus, plus a live log of every relevant bus event.
 */
export class DebugHarness implements GameSystem {
  private readonly log: string[] = [];
  private logText?: Phaser.GameObjects.Text;
  private btn?: Phaser.GameObjects.Rectangle;
  private btnLabel?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.buildUI(scene);
    this.setVisible(isDebug());

    this.bus.on(GameEvent.BallDropped, (b) =>
      this.push(`-> BALL_DROPPED v${b.value} t${b.tier} x${b.x}`),
    );
    this.bus.on(GameEvent.ZoneBBusy, () => this.push('<- ZONE_B_BUSY'));
    this.bus.on(GameEvent.ZoneBEmpty, () => this.push('<- ZONE_B_EMPTY'));
    this.bus.on(GameEvent.ScoreChanged, ({ total }) => this.push(`<- SCORE_CHANGED ${total}`));

    scene.input.keyboard?.on('keydown-SPACE', () => {
      if (isDebug()) this.drop();
    });
  }

  update(_time: number, _delta: number): void {}

  setVisible(on: boolean): void {
    this.logText?.setVisible(on);
    this.btn?.setVisible(on);
    this.btnLabel?.setVisible(on);
    if (!on) {
      this.log.length = 0;
      this.logText?.setText('');
    }
  }

  private drop(): void {
    const tier = Phaser.Math.Between(DROP_MIN_TIER, DROP_MAX_TIER);
    this.bus.emit(GameEvent.BallDropped, { value: tierToValue(tier), tier, x: Layout.zoneBEntry.x });
  }

  private buildUI(scene: Phaser.Scene): void {
    const btnY = Layout.zoneB.y + Layout.zoneB.height - 28;

    this.btn = scene.add
      .rectangle(Layout.WIDTH / 2, btnY, 160, 40, 0x2d7d46)
      .setStrokeStyle(2, 0x6ee7a0)
      .setInteractive({ useHandCursor: true })
      .setDepth(2000);
    this.btn.on(Phaser.Input.Events.POINTER_DOWN, () => {
      if (isDebug()) this.drop();
    });

    this.btnLabel = scene.add
      .text(Layout.WIDTH / 2, btnY, 'DROP  (space)', {
        fontFamily: 'monospace',
        fontSize: '16px',
        color: '#ffffff',
      })
      .setOrigin(0.5)
      .setDepth(2001);

    this.logText = scene.add
      .text(8, Layout.zoneB.y + 8, '', {
        fontFamily: 'monospace',
        fontSize: '12px',
        color: '#8fd0ff',
        lineSpacing: 2,
      })
      .setDepth(2000);
  }

  private push(line: string): void {
    if (!isDebug()) return;
    this.log.push(line);
    if (this.log.length > MAX_LOG_LINES) this.log.shift();
    this.logText?.setText(this.log.join('\n'));
  }
}
