import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import type { StubZoneAC } from './stubZoneAC';

const MAX_LOG_LINES = 12;
const DROP_MIN_TIER = 1;
const DROP_MAX_TIER = 6;

/**
 * Dev harness for `?zone=b`: an on-screen DROP button (also the SPACE key) that
 * fires a ball through StubZoneAC, plus a live log of every bus event in/out — so
 * Dev 2 sees the contract traffic while building the real Zone B.
 */
export class Harness implements GameSystem {
  private readonly log: string[] = [];
  private logText?: Phaser.GameObjects.Text;

  constructor(
    private readonly bus: EventBus,
    private readonly driver: StubZoneAC,
  ) {}

  create(scene: Phaser.Scene): void {
    this.buildLog(scene);
    this.buildButton(scene);

    // Mirror everything crossing the bus into the on-screen log.
    this.bus.on(GameEvent.BallDropped, (b) =>
      this.push(`-> BALL_DROPPED v${b.value} t${b.tier} x${b.x}`),
    );
    this.bus.on(GameEvent.ZoneBBusy, () => this.push('<- ZONE_B_BUSY'));
    this.bus.on(GameEvent.ZoneBEmpty, () => this.push('<- ZONE_B_EMPTY'));
    this.bus.on(GameEvent.ScoreChanged, ({ total }) => this.push(`<- SCORE_CHANGED ${total}`));

    scene.input.keyboard?.on('keydown-SPACE', () => this.drop());
  }

  update(_time: number, _delta: number): void {}

  private drop(): void {
    const tier = Phaser.Math.Between(DROP_MIN_TIER, DROP_MAX_TIER);
    if (!this.driver.dropBall(tier)) this.push(`.. drop blocked (locked) t${tier}`);
  }

  private buildButton(scene: Phaser.Scene): void {
    const y = Layout.zoneB.y + Layout.zoneB.height - 28;
    const btn = scene.add
      .rectangle(Layout.WIDTH / 2, y, 160, 40, 0x2d7d46)
      .setStrokeStyle(2, 0x6ee7a0)
      .setInteractive({ useHandCursor: true })
      .setDepth(2000);
    btn.on(Phaser.Input.Events.POINTER_DOWN, () => this.drop());

    scene.add
      .text(Layout.WIDTH / 2, y, 'DROP  (space)', {
        fontFamily: 'monospace',
        fontSize: '16px',
        color: '#ffffff',
      })
      .setOrigin(0.5)
      .setDepth(2001);
  }

  private buildLog(scene: Phaser.Scene): void {
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
    this.log.push(line);
    if (this.log.length > MAX_LOG_LINES) this.log.shift();
    this.logText?.setText(this.log.join('\n'));
  }
}
