import Phaser from 'phaser';

/** A thing that can be flown: positioned + scaled each tick, destroyed on arrival. */
type Flyable = Phaser.GameObjects.GameObject &
  Phaser.GameObjects.Components.Transform &
  Phaser.GameObjects.Components.Visible;

export interface OverlayFlyerOptions {
  /** Scene that OWNS the drive tween — pass GameScene so a `scene.restart()` kills it. */
  gameScene: Phaser.Scene;
  /** Top-most overlay scene the flyer + trail motes draw into (scroll 0, screen-space). */
  overlay: Phaser.Scene;
  /** The object to fly (already added to `overlay`). Positioned/scaled each tick, destroyed on arrival. */
  object: Flyable;
  start: { x: number; y: number };
  end: { x: number; y: number };
  durationMs?: number;
  ease?: string;
  /** Horizontal jitter on the bezier control point so a burst of flyers fan out. */
  bowJitter?: number;
  scaleFrom?: number;
  scaleTo?: number;
  trailColor?: number;
  trailEveryNFrames?: number;
  trailFadeMs?: number;
  /** Called once when the flyer reaches `end`, just before the object is destroyed. */
  onArrive?: () => void;
}

export interface OverlayFlyer {
  tween: Phaser.Tweens.Tween;
  object: Flyable;
  /** Kill the tween + destroy the object immediately WITHOUT calling onArrive (teardown). */
  discard(): void;
}

/**
 * Fly an overlay object from `start` to `end` along a jittered quadratic-bezier arc, shedding a
 * fading trail, then destroy it and call `onArrive`. Mirrors Zone A's buffer-particle fly-up:
 * the DRIVE tween lives on `gameScene` (so a scene restart kills it), while the object + trail
 * live on the top-most `overlay` scene — full-screen at scroll 0, coords 1:1 with the screen
 * chrome the flight targets, and immune to the phase pan.
 *
 * The overlay object OUTLIVES a GameScene restart (the overlay scene is separate + always-on), so
 * a caller that can be torn down mid-flight must hold the returned handle and `discard()` it in
 * its own `destroy()` — otherwise the object leaks onto the overlay.
 */
export function launchOverlayFlyer(opts: OverlayFlyerOptions): OverlayFlyer {
  const {
    gameScene,
    overlay,
    object,
    start,
    end,
    durationMs = 500,
    ease = 'Sine.easeInOut',
    bowJitter = 60,
    scaleFrom = 1,
    scaleTo = 1,
    trailColor = 0xffffff,
    trailEveryNFrames = 3,
    trailFadeMs = 220,
    onArrive,
  } = opts;

  const control = new Phaser.Math.Vector2(
    (start.x + end.x) / 2 + Phaser.Math.FloatBetween(-bowJitter, bowJitter),
    Phaser.Math.Linear(start.y, end.y, 0.45),
  );
  const curve = new Phaser.Curves.QuadraticBezier(
    new Phaser.Math.Vector2(start.x, start.y),
    control,
    new Phaser.Math.Vector2(end.x, end.y),
  );

  object.setPosition(start.x, start.y);
  object.setScale(scaleFrom);

  const proxy = { t: 0 };
  let frame = 0;
  const flyer: OverlayFlyer = {
    object,
    tween: undefined as unknown as Phaser.Tweens.Tween,
    discard: () => {
      flyer.tween.remove();
      object.destroy();
    },
  };
  flyer.tween = gameScene.tweens.add({
    targets: proxy,
    t: 1,
    duration: durationMs,
    ease,
    onUpdate: () => {
      const p = curve.getPoint(proxy.t);
      object.setPosition(p.x, p.y);
      object.setScale(Phaser.Math.Linear(scaleFrom, scaleTo, proxy.t));
      if (frame++ % trailEveryNFrames === 0) shedTrailMote(overlay, p.x, p.y, trailColor, trailFadeMs);
    },
    onComplete: () => {
      onArrive?.();
      object.destroy();
    },
  });
  return flyer;
}

/** A tiny fading mote left behind by an in-flight flyer. Self-destroys on the overlay scene. */
function shedTrailMote(
  overlay: Phaser.Scene,
  x: number,
  y: number,
  color: number,
  fadeMs: number,
): void {
  const mote = overlay.add.circle(x, y, 2, color, 0.55).setDepth(949);
  overlay.tweens.add({
    targets: mote,
    alpha: 0,
    scale: 0.3,
    duration: fadeMs,
    onComplete: () => mote.destroy(),
  });
}
