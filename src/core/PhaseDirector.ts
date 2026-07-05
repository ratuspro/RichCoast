import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import * as Layout from './Layout';
import { HUD_H, PAN_DISTANCE, arenaCenterY, framingForPan } from './phaseGeometry';
import { initialPhaseState, stepPhase, type PhaseInput, type PhaseState } from './phaseMachine';

/** Camera pan duration between the two phase framings, in ms. Snappier than the 1200ms
 *  milestone zoom — the pan is a scene change, not a reward beat. */
const PAN_MS = 650;

/**
 * Scene-level choreographer of the two-phase flow (shared shell, like the HUD).
 *
 * Listens for the two triggers — ZONE_A_DEPLETED (A → pan down) and SCORE_BAR_FILLED
 * (B → pan up) — runs the camera pan, and broadcasts PHASE_CHANGED so each zone applies
 * its own input lock. It never calls into a zone: the state lives in the pure
 * `phaseMachine`, and the cameras are driven by name (`cameras.getCamera('arena')` — the
 * same decoupling trick Zone C's `toApparent` uses), so Zone A's ArenaView stays the
 * camera's owner and this class only animates its viewport during the pan.
 *
 * One tween proxy drives BOTH cameras (main scrollY + arena viewport height) through
 * `framingForPan`, so the arena-bottom / Zone-C seam stays pixel-locked mid-pan.
 *
 * Must be created LAST by GameScene so its initial PHASE_CHANGED('A') reaches every
 * system that subscribed during its own create().
 */
export class PhaseDirector implements GameSystem {
  private scene?: Phaser.Scene;
  private state: PhaseState = initialPhaseState();
  private tween?: Phaser.Tweens.Tween;
  /** Current pan position, retained across tweens so a turnaround starts from the truth. */
  private readonly pan = { value: 0 };

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.state = initialPhaseState();
    this.pan.value = 0;
    this.applyPan(0);

    this.bus.on(GameEvent.ZoneADepleted, () => this.step('depleted'));
    this.bus.on(GameEvent.ScoreBarFilled, () => this.step('barFilled'));

    this.bus.emit(GameEvent.PhaseChanged, { phase: this.state.phase });
  }

  update(_time: number, _delta: number): void {}

  destroy(): void {
    this.tween?.remove();
    this.tween = undefined;
  }

  private step(input: PhaseInput): void {
    const result = stepPhase(this.state, input);
    this.state = result.state;
    if (result.startPan) this.startPan(result.startPan === 'B' ? PAN_DISTANCE : 0);
    if (result.changed) this.bus.emit(GameEvent.PhaseChanged, { phase: this.state.phase });
  }

  private startPan(target: number): void {
    // The FSM never overlaps pans (a turnaround waits for panDone), but a stale tween
    // would corrupt the shared proxy — kill defensively.
    this.tween?.remove();
    this.tween = this.scene?.tweens.add({
      targets: this.pan,
      value: target,
      duration: PAN_MS,
      ease: 'Sine.easeInOut',
      onUpdate: () => this.applyPan(this.pan.value),
      onComplete: () => {
        this.tween = undefined;
        this.applyPan(target);
        this.step('panDone');
      },
    });
    // Headless/edge case: no scene tweens available — snap so the flow can't wedge.
    if (!this.tween) {
      this.pan.value = target;
      this.applyPan(target);
      this.step('panDone');
    }
  }

  /** Drive both cameras to the framing for `pan` (rounded inside framingForPan, so the
   *  arena bottom and Zone C's top edge can't drift apart by a sub-pixel). */
  private applyPan(pan: number): void {
    const scene = this.scene;
    if (!scene) return;
    const framing = framingForPan(pan);

    scene.cameras.main.setScroll(0, framing.scrollY);

    // The arena camera exists in ac/full modes (created by Zone A). Shrinking its viewport
    // while keeping its floor-anchored centring (see ArenaView.applyCameraZoom) crops Zone A
    // from the TOP at unchanged zoom — the "pure pan" look.
    const arena = scene.cameras.getCamera('arena');
    if (arena) {
      arena.setViewport(0, HUD_H, Layout.WIDTH, framing.arenaViewportH);
      const s = 1 / arena.zoom;
      arena.centerOn(Layout.WIDTH / 2, arenaCenterY(framing.arenaViewportH, s));
    }
  }
}
