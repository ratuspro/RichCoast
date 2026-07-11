import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';
import { Theme } from '../core/Theme';
import { findNearestDoorBall } from '../zoneA/doorTarget';

/** How long the suck tween runs before the ball pops into Zone B, in ms. */
const SUCK_MS = 150;
/** The quick scale-up "spawn pop" at the entry column after the suck, in ms. */
const POP_MS = 110;
/** Duration of one edge→edge sweep leg (the return leg doubles it). Difficulty knob. */
const SWEEP_MS = 880;
/** Inset of the sweep from each Zone B edge (~one ball radius + wall slack), in px. */
const SWEEP_MARGIN = 18;
/** Number of evenly-spaced positions the lit marker steps between. */
const SWEEP_POSITIONS = 9;
/** On-screen size the suck snapshot lands at — matches Zone B's fixed ball (radius 10). */
const ZONE_B_BALL_PX = 20;
/** Per-position dwell so one leg (8 steps) == SWEEP_MS, keeping the old cadence. */
const STEP_MS = SWEEP_MS / (SWEEP_POSITIONS - 1);

/**
 * Zone C — the trap-door (Dev 1).
 *
 * Owns the cooldown lock (driven by Zone B's busy/empty events) and a row of nine
 * evenly-spaced position markers across the band; while armed, the lit one steps
 * edge→edge and back. A tap freezes on the lit position; its column becomes the Zone B
 * entry. ZoneBBusy fires up front (so Zone A's stalemate check stays blocked while the
 * ball is mid-transit), then a suck→pop cosmetic runs and BALL_DROPPED is emitted at the
 * frozen column when it lands — so WHERE a ball enters Zone B is a timing skill, picked
 * from nine discrete columns rather than a fixed one.
 */
export class ZoneCSystem implements GameSystem {
  private locked = false;
  /** Separate lock raised while Zone A's milestone zoom-out animates (ArenaZoom event). */
  private zoomLocked = false;
  /** Phase lock: the trap-door only arms in the 'B' phase (PhaseChanged event). Defaults
   *  to locked — the run boots in the 'A' phase — so it's robust to create() ordering. */
  private phaseLocked = true;
  private scene?: Phaser.Scene;
  /** The nine dim brass position markers; the active one glows polished-bright. */
  private dots: Phaser.GameObjects.Arc[] = [];
  /** Precomputed x of each position, indexed the same as `dots`. */
  private positionsX: number[] = [];
  /** Which position is currently lit (read by onTap to freeze the column). */
  private activeIndex = 0;
  /** Last styled index, so update() restyles only when the lit position changes. */
  private styledIndex = -1;
  private sweepMinX = 0;
  private sweepMaxX = 0;
  /** Elapsed sweep time (ms); advanced only while armed, reset to 0 on re-arm. */
  private sweepT = 0;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;

    // Cooldown: locked while Zone B has balls in flight, re-armed when it's empty.
    this.bus.on(GameEvent.ZoneBBusy, () => this.setLocked(true));
    this.bus.on(GameEvent.ZoneBEmpty, () => this.setLocked(false));

    // Freeze the door for the duration of a Zone A milestone zoom-out (composes with the
    // Zone-B-driven lock, so neither source clobbers the other).
    this.bus.on(GameEvent.ArenaZoom, ({ active }) => {
      this.zoomLocked = active;
      if (!active) this.sweepT = 0; // restart the sweep from the edge when the zoom lands
    });

    // Phase lock: armed only while the game is in the Zone-B phase (composes with the
    // other two locks). Reset the sweep on arming so it always restarts from the edge.
    this.bus.on(GameEvent.PhaseChanged, ({ phase }) => {
      this.phaseLocked = phase !== 'B';
      if (!this.phaseLocked) this.sweepT = 0;
    });

    // The door band reads as a seamless continuation of Zone A's wooden ramp: same pine
    // fill, no top border, so the A→C seam disappears. Only the bottom edge carries a
    // divider, marking the separation from Zone B below. The band itself is purely visual —
    // the tap target is the WHOLE screen (scene-level pointer listener): onTap() no-ops
    // unless the door is armed, and the door is only armed in the B phase, when no other
    // gameplay input competes for taps. The fill never changes with lock state.
    const r = Layout.zoneC;
    const band = scene.add.rectangle(
      r.x + r.width / 2, r.y + r.height / 2, r.width, r.height, Theme.pine,
    );
    scene.input.on(Phaser.Input.Events.POINTER_DOWN, () => this.onTap());
    // Separation from Zone B only: a thin shadow line along the band's bottom edge.
    const divider = scene.add
      .rectangle(r.x + r.width / 2, r.y + r.height, r.width, 2, Theme.pineShadow)
      .setDepth(40);

    // Milestone palette swap: the band + divider bake their fill, so re-apply per tick of
    // the cross-fade. The dots self-heal (hidden while zoom-locked, restyled on re-arm).
    this.bus.on(GameEvent.ThemeChanged, () => {
      band.setFillStyle(Theme.pine);
      divider.setFillStyle(Theme.pineShadow);
    });

    // Nine evenly-spaced markers along the door band. The lit one is where a tap drops
    // the ball, so the span is inset by one ball radius from each edge — a ball can never
    // spawn into the side wall.
    const mouthY = r.y + r.height / 2;
    this.sweepMinX = Layout.zoneB.x + SWEEP_MARGIN;
    this.sweepMaxX = Layout.zoneB.x + Layout.zoneB.width - SWEEP_MARGIN;
    for (let i = 0; i < SWEEP_POSITIONS; i++) {
      const x = this.sweepMinX + ((this.sweepMaxX - this.sweepMinX) * i) / (SWEEP_POSITIONS - 1);
      this.positionsX.push(x);
      const dot = scene.add.circle(x, mouthY, 6, Theme.brass).setDepth(50);
      this.dots.push(dot);
      this.styleDot(i, false);
    }
  }

  /** Polished-brass glow when active, dim brass stud when not. One place for both looks. */
  private styleDot(i: number, active: boolean): void {
    const dot = this.dots[i];
    if (!dot) return;
    if (active) {
      dot.setFillStyle(Theme.brassBright, 1).setStrokeStyle(2, Theme.cream).setScale(1.3);
    } else {
      dot.setFillStyle(Theme.brass, 0.45).setStrokeStyle(0).setScale(1);
    }
  }

  /**
   * The lit position is driven straight off the `locked` flag every frame — no tween to
   * get stuck. Armed: the lit index steps edge→edge and back (ping-pong) at STEP_MS each.
   * Locked: all markers are hidden. Because this reads the current state each tick, the
   * sweep always reappears the instant Zone B clears (`locked` → false), self-healing
   * regardless of how the busy/empty events interleave.
   */
  /** True when the door is held — by Zone B activity, an in-progress arena zoom-out,
   *  or the game not being in the Zone-B phase. */
  private isLocked(): boolean {
    return this.locked || this.zoomLocked || this.phaseLocked;
  }

  update(_time: number, delta: number): void {
    if (this.dots.length === 0) return;
    if (this.isLocked()) {
      for (const dot of this.dots) dot.setVisible(false);
      this.styledIndex = -1; // force a restyle on re-arm
      return;
    }
    for (const dot of this.dots) dot.setVisible(true);

    this.sweepT += delta;
    const cycle = 2 * (SWEEP_POSITIONS - 1); // steps in one full ping-pong
    const step = Math.floor(this.sweepT / STEP_MS) % cycle;
    this.activeIndex = step < SWEEP_POSITIONS ? step : cycle - step;

    if (this.activeIndex !== this.styledIndex) {
      for (let i = 0; i < this.dots.length; i++) this.styleDot(i, i === this.activeIndex);
      this.styledIndex = this.activeIndex;
    }
  }

  private onTap(): void {
    if (this.isLocked()) return;

    if (!this.scene) return;
    const ball = findNearestDoorBall(this.scene);
    if (!ball?.ballData) return; // nothing to suck yet (e.g. Zone A still empty)

    // Freeze the sweep the instant the player commits — the lit position's column is
    // where the ball will enter Zone B. Capture it before setLocked() hides the markers.
    const spawnX = this.positionsX[this.activeIndex] ?? Layout.zoneBEntry.x;

    // Signal busy up front so ZoneASystem.checkLoss() can't read a stalemate while the
    // ball is mid-transit: BALL_DROPPED is now deferred to the end of the suck→pop, but
    // Zone A still sees "Zone B busy", so the board emptying here is never a game-over.
    this.setLocked(true);
    this.bus.emit(GameEvent.ZoneBBusy);

    const { value, tier } = ball.ballData;
    const image = ball.gameObject as Phaser.GameObjects.Image | undefined;
    const texKey = image?.texture?.key;

    // Capture how the ball actually appears on screen BEFORE destroying it: Zone A's camera may
    // be zoomed out (post-milestone), so the ball sits smaller and offset from its world coords.
    const worldDiameter = (ball.circleRadius ?? ZONE_B_BALL_PX / 2) * 2;
    const start = this.toApparent(ball.position.x, ball.position.y, worldDiameter);

    // Remove the ball from Zone A by destroying its image — the Board self-prunes its
    // registry off the image's DESTROY event (see Board.register).
    image?.destroy();

    Sfx.transition();
    // Cosmetic suck → spawn pop → hand off to Zone B at the frozen column.
    this.playSuck(start, texKey, value, tier, spawnX);
  }

  /**
   * Map a Zone-A ball's world position + size to how it appears on screen. Zone A's dedicated
   * camera may be zoomed out (after a milestone), so a ball sits smaller and offset from its
   * world coords; the suck snapshot rides the MAIN camera into Zone B, so it must start from
   * the on-screen spot/size. With no arena camera (e.g. ?zone=b) world == screen.
   */
  private toApparent(
    worldX: number,
    worldY: number,
    worldSize: number,
  ): { x: number; y: number; size: number } {
    const cam = this.scene?.cameras.getCamera('arena');
    if (!cam) return { x: worldX, y: worldY, size: worldSize };
    const view = cam.worldView;
    return {
      x: cam.x + (worldX - view.x) * cam.zoom,
      y: cam.y + (worldY - view.y) * cam.zoom,
      size: worldSize * cam.zoom,
    };
  }

  /**
   * Cosmetic suck → spawn pop → handoff. A throwaway snapshot sprite slides from the ball's
   * last Zone-A position to the frozen entry column at the door mouth (suck), then pops up at
   * the top of Zone B (pop), and only when the pop lands do we emit BALL_DROPPED so Zone B's
   * real ball appears exactly there — deferring the emit avoids any double-ball flicker. If
   * there's no sprite to animate we still hand the ball off so Zone B isn't starved.
   */
  private playSuck(
    start: { x: number; y: number; size: number },
    texKey: string | undefined,
    value: number,
    tier: number,
    spawnX: number,
  ): void {
    const scene = this.scene;
    if (!scene || !texKey) {
      this.emitDrop(value, tier, spawnX);
      return;
    }

    const mouthY = Layout.zoneC.y + Layout.zoneC.height / 2;
    // `start` is in SCREEN coords (toApparent), but the sprite lives in world space on the
    // scrolled main camera — sucks happen in the B-phase, where scrollY is parked at
    // PAN_DISTANCE. Offset the spawn point; the tween targets (mouthY, zoneBEntry.y) are
    // already world coords.
    const startWorldY = start.y + scene.cameras.main.scrollY;
    const sprite = scene.add.image(start.x, startWorldY, texKey).setDepth(800);
    sprite.setDisplaySize(start.size, start.size); // start at the ball's true on-screen size
    // The sprite rides the main camera into Zone B; keep the arena camera from double-drawing it.
    scene.cameras.getCamera('arena')?.ignore(sprite);

    scene.tweens.add({
      targets: sprite,
      x: spawnX,
      y: mouthY,
      displayWidth: ZONE_B_BALL_PX * 0.5,
      displayHeight: ZONE_B_BALL_PX * 0.5,
      duration: SUCK_MS,
      ease: 'Cubic.easeIn',
      onComplete: () => {
        sprite.setPosition(spawnX, Layout.zoneBEntry.y);
        scene.tweens.add({
          targets: sprite,
          displayWidth: ZONE_B_BALL_PX,
          displayHeight: ZONE_B_BALL_PX,
          duration: POP_MS,
          ease: 'Back.easeOut',
          onComplete: () => {
            this.emitDrop(value, tier, spawnX);
            sprite.destroy();
          },
        });
      },
    });
  }

  private emitDrop(value: number, tier: number, x: number): void {
    // x is the column the player picked by tapping the Zone C sweep marker at the right moment.
    this.bus.emit(GameEvent.BallDropped, { value, tier, x });
  }

  private setLocked(locked: boolean): void {
    const wasLocked = this.locked;
    this.locked = locked;
    // On re-arm (locked→unlocked) restart the step sequence from the left edge (index 0).
    // update() does the actual showing/hiding each frame, so markers can't get stuck out of sync.
    if (wasLocked && !locked) this.sweepT = 0;
  }
}
