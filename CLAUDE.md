# CLAUDE.md

RichCoast — a portrait-mode mobile arcade game, being **reimplemented natively in Unity**
(Android first, iOS-compatible) on this `unity` branch. The finished Phaser web game lives on
`master` and is the **design truth**; the older `unity-migration` branch is an abandoned port
kept only as reference (its two-camera pixel-locked rig is deliberately NOT carried over).

## What the game is, in one screen

- **Zone A (top)** — fruit-merge puzzle. Drag to aim along the top, release to drop; equal-tier
  balls merge into the next tier (value ×3, `3^(tier-1)`), with a neighbour-shoving blast.
  A ball resting above the death line ends the run.
- **Zone C (boundary)** — a manually-tapped trap-door that sucks the nearest Zone A ball into B.
- **Zone B (bottom)** — no-control physics arena; gates split a ball into copies; drained value
  fills a score bar. Filling it levels up and refills Zone A's finite ball buffer.
- Two phases (A: drop; B: trap-door) joined by a camera pan. Endless; merging scores nothing —
  value is realised only when balls exit B.

Detail lives in **`docs/SPEC.md`** (gameplay design: mechanics, scoring, progression, theme)
and **`docs/TECH_SPEC.md`** (the original Phaser architecture — still the reference for the
event seam and zone ownership). Read the relevant section on demand, not wholesale.

## Architecture (Unity-native)

- **Assemblies:** `RichCoast.Core` (`Assets/Game/Core`, pure C#, `noEngineReferences`) holds
  all design math — `TierMath`/`MergeLogic`, `TierLadder` (radii/friction/density tables +
  neutral-growth/milestone-zoom math), `BallMath`, `Materials` (the 20-material ladder),
  `ProgressionCurve` (anchor-interpolated targets, buffer oscillation, tail growth, window
  stepping), `ComboPitch`, `NumberFormat.Compact`, `BallQueue`, `PhaseMachine` (A ⇄ B flow),
  `ScoreBar`, `ZoneBLayouts` (the two authored arenas), `DoorMath` (nearest-ball / sweep /
  split fan), and the typed static **`GameEvents`** seam. `RichCoast.Game` (`Assets/Game/Gameplay`) is the MonoBehaviour/plain-C#
  gameplay layer; `RichCoast.UI` (`Assets/Game/UI`) the uGUI shell; `RichCoast.App`
  (`Assets/Game/App/GameBootstrap.cs`) the composition root — the ONLY component the scene
  holds. `RichCoast.EditorTools` (`Assets/Editor`) = project setup + scene/asset builder.
  Tests: `Assets/Tests/EditMode` (Core math, mirrors master's vitest suites incl. the
  progression reachability guard) and `Assets/Tests/PlayMode` (scene smoke + screenshot).
- **Zones never call each other** — they talk only through `GameEvents` (typed C# events,
  same names/meanings as the Phaser bus: `BallDropped`, `ZoneBBusy/Empty`, `ScoreChanged`,
  `ScoreBarFilled/CashedIn/Changed`, `ScoreHarvested`, `BallBufferChanged`,
  `ProgressionChanged`, `ArenaZoom`, `PhaseChanged`, `ZoneADepleted`, plus `GameOver`).
  `GameEvents.Reset()` runs on every scene load and in test setup.
- **Tuning is ScriptableObjects**, editable live in play mode (`Assets/Game/Data`, created by
  the scene builder if missing): `TierLadder.asset`, `Progression.asset` (port of
  `progression.json`), and **`GameFeel.asset`** — THE feel file (physics, blast, squash/stretch,
  burst, audio, haptics, HUD timings).
- **World & screen:** Core tables are authored in the Phaser 390×844 **design px**;
  `DesignSpace`/`BoardGeometry` convert to world units (board = 10 units wide, funnel apex at
  the origin, y-up). ONE orthographic camera (`CameraRig`) fits board width to screen width
  (extra height on tall phones is headroom, never letterboxing) with the top edge pinned above
  the tray + HUD, inset by the safe area. uGUI canvases are ScreenSpaceCamera on that camera
  (so render-texture captures include them), CanvasScaler 1080×2340 match 0.5, HUD under a
  `SafeArea` container. `CameraRig.Pan` blends the A framing (top pinned) with the B framing
  (Zone B's bottom pinned) for the phase pan. Physics2D (Box2D): Zone A balls on layer 8,
  walls 9 (shared by both zones), Zone B balls 10, fresh split copies 11, gates 12 — layers are
  physics only, never render routing.
- **Juice:** `BallView` (child of the body: landing squash along the contact normal, merge-birth
  pop, blast punch, idle wobble), `MergeFx` (rim sparks + flash ring), `Sfx` (procedural
  synthesised clips, combo pitch-climb via `ComboPitch`), `Haptics` (Android
  `VibrationEffect` via JNI, no-op elsewhere), `ScoreFlyer` (the overlay "+N" bezier fly-up
  into the HUD total). All driven by PrimeTween + `GameFeel.asset`.

## Tech stack

Unity **6000.5.3f1** · URP 17.6 with the **2D Renderer** (`Assets/Settings/UniversalRP.asset`,
`Renderer2D.asset`) · Physics2D · New Input System (`Pointer.current` — touch on device, mouse
in editor) · uGUI + TextMeshPro · **PrimeTween** (OpenUPM) · Coplay editor plugin (git URL in
`Packages/manifest.json`) for the MCP lane. Portrait-only, linear colour, Android IL2CPP/ARM64
minSdk 26, bundle id `com.richcoast.game`. No image/audio assets — everything procedural.

## Workflow & tooling

Two lanes; **they are mutually exclusive** (the editor holds a project lock):

1. **Headless CLI** (`Tools/`, bash — Git Bash on Windows; `Tools/unity-path.sh` resolves the
   Hub editor from `ProjectSettings/ProjectVersion.txt`, override with `UNITY_PATH`):
   - `Tools/setup-project.sh` — idempotent: URP-2D wiring, player/Android settings, TMP
     essentials, data assets + `Assets/Scenes/Main.unity` (`ProjectSetup.ApplyAndBuild`).
     Fails loudly on compile errors.
   - `Tools/run-tests.sh [--platform EditMode|PlayMode] [--filter X]` — NUnit results parsed,
     non-zero on any failure. PlayMode keeps graphics (the screenshot test needs them).
   - `Tools/screenshot.sh` — renders a populated board to `Logs/game-scene.png` (A framing) and
     `Logs/game-scene-b.png` (B framing, Zone B mid-cascade), 1080×2340.
   - `Tools/build-android.sh [--no-install]` — development APK to `Builds/Android/RichCoast.apk`
     (`ProjectSetup.BuildAndroid`, `-buildTarget Android`), then `adb install` + launch + a
     `logcat -s Unity` tail using the editor module's bundled SDK. `Assets/Editor/
     AndroidManifestPatcher.cs` injects `VIBRATE` into the generated manifest (the JNI haptics
     path doesn't trigger Unity's auto-permission). The Play-Core `AssetPackManager`
     ClassNotFoundException at boot is benign Unity noise.
2. **Editor open + Coplay MCP** (`check_compile_errors`, `play_game`, `capture_*`,
   `get_unity_logs`, `execute_script`) for live iteration and visual checks. Menu items:
   `RichCoast/Apply Project Setup`, `RichCoast/Build Data Assets + Main Scene`,
   `RichCoast/Switch Active Target To Android`.

Feel verification tiers: EditMode tests (math) → PlayMode + screenshot (behaviour/layout) →
**on-device Android Build & Run** (the only honest judge of touch feel, haptics, perf).

## Status

**Milestones 1, 2 + 3 are implemented and green headlessly** (EditMode 88 tests · PlayMode 11,
incl. a Zone B drain test, a full depletion → pan → door-tap → Zone B handoff test, and three
milestone tests: plain level-ups never zoom, the level-20 milestone grows/drains/recolours, a
milestone cash-in that arrives in phase B defers until the pan lands in A; screenshots of the A
framing, the B framing and the first milestone in `Logs/game-scene*.png`). M1 ran on a Pixel 7
(2026-09-11); **M2 and M3 have not yet been built to the device** — the user's hands-on feel
sign-off (touch, haptics, audio, perf, door timing, zoom/drain pacing, the single-camera
framing consequence below) and any resulting `GameFeel.asset` tuning are pending.

The full loop runs from one `GameBootstrap`:
- **Zone A** — tray, pooled material balls, drag-to-aim, finite buffer, merges with juice, death
  line, game over + RESTART. Aiming is frozen outside phase A and during a milestone zoom
  (one `ApplyFreeze` sink). A cash-in that arrives in phase B (or mid-zoom) defers its reward
  beat until the pan lands back in A; a roll-through burst composes its zoom factors by product
  into that deferred slot. A brass `DropHighlight` breathes under the ball the door would grab
  while the buffer is spent.
- **Milestones (M3)** — every 20 levels the draw window shifts (`Core.ProgressionCurve`) and
  `ZoneASystem` runs master's cash-in sequence: `TierLadder.MilestoneZoomFactor` (neutral
  growth × stage tightness; flat ×1.2 tail) → `ArenaGrowth.Grow` snaps `BoardGeometry.Scale`,
  rebuilds the tray outward (`ArenaBuilder`; wall bodies keep a CONSTANT thickness — Zone A
  balls use continuous collision, and a ×s floor would reach through the shared wall layer
  into Zone B), re-normalises ball gravity (`BallFactory.GravityScale` = feel × s, master's
  supplemental gravity; blast/rest/speed-cap/impact-squash are all ÷/× s too) and tweens
  `CameraRig.ViewScale` over `GameFeel.milestoneZoomMs`. When the camera lands,
  `DrainBlacklisted` takes every ball below the new window floor (`Board.TakeBallsBelow`),
  raises `ZoneBBusy` up front, slides throwaway sprites to the Zone B entry (column clamped 12
  design px inside the walls) and raises `BallDropped` per landing; `ArenaZoom(false)` only
  after the last one. The queue re-rolls off blacklisted tiers before the zoom.
- **Single camera zoom** — `CameraRig` frames `10 × ViewScale` units in the A framing and Zone
  B's native 10 units in the B framing; `Pan` blends both position AND width. Consequence (vs
  master's second Zone-A camera): in the A framing Zone C and Zone B appear at 1/s once the
  arena has grown, and the pan to B doubles as a zoom-in on Zone B. Tray paint below the apex
  clamps to Zone C's divider so the grown tray never paints into Zone B.
- **Palettes** — `Core.Palettes` (workshop → dusk → night → dawn → gilded, verbatim from
  master's `Theme.ts`, + `ColorMath`/`Palette.Lerp`). `Game.Theme` is the ACTIVE palette
  (`Theme.Apply`); baked colours are bound with `Themed.Bind(renderer, ThemeKey)` (or the
  `WorldArt`/`UiKit` ThemeKey overloads) and one `GameEvents.ThemeChanged` restyles them all,
  preserving each target's live alpha. `ThemeDirector` targets `curve.PaletteNameForLevel` on
  `ProgressionChanged` and cross-fades on `ArenaZoom(true)` over the zoom duration. Every run
  boots in workshop (`GameBootstrap.Awake` applies it and `Tween.StopAll()`s stale fades).
- **Zone C** (`Gameplay/ZoneC/ZoneCSystem.cs`) — pine door band under the funnel apex; nine
  brass markers, the lit one ping-pongs at `GameFeel.sweepMs`; armed only in phase B and while
  Zone B is empty (+ `ArenaZoom` lock, game-over lock). A tap anywhere grabs the nearest ball by
  edge distance (`DoorTarget` → `Core.DoorMath`, shared with the highlight), raises `ZoneBBusy`
  up front, `Board.Extract`s it, plays suck→pop (PrimeTween) and only then raises `BallDropped`
  with the frozen column (design-px x).
- **Zone B** (`Gameplay/ZoneB/`) — one of `Core.ZoneBLayouts` (LAYOUT_1/2, a 1:1 port, design
  px rebased to the band) built in world space below Zone C: capsule-collider pine rails,
  static/kinematic gate slabs (`ZoneBGate`, painted sign + world-space TMP `X N`), trigger
  collectors, invisible containment. Small pooled `ZoneBBall`s (10 design px, layer 10; fresh
  split copies on layer 11 ignore gates for `splitGraceMs`); contacts are reported and resolved
  once per frame. Owns scoring via `Core.ScoreBar`: live per-level `ScoreBarFilled` wraps, the
  world-space bar + `+N` haul label, then `ScoreHarvested` → `ZoneBEmpty` → `ScoreBarCashedIn`
  after the wraps + dwell. Safety: `maxBallsInFlight` cap and a stuck-ball nudge.
- **Phase pan** — `PhaseDirector` runs `Core.PhaseMachine` (A ⇄ B with the queued-refill
  bounce) and tweens `CameraRig.Pan` 0→1: A pins the top edge above the tray + HUD, B pins Zone
  B's bottom (score bar) to the screen bottom, both safe-area inset; on the 390×844 design
  screen the two differ by exactly `DesignSpace.PanDistance` (394 px) at scale 1.
- Physics layers: Zone A balls 8, walls 9 (shared), Zone B balls 10, grace 11, gates 12 —
  ignores set in `GameBootstrap.ConfigurePhysicsLayers`. Sfx gained transition / multiply
  (combo-pitched) / collect cues.

Next: **on-device M2 + M3 feel session** (`Tools/build-android.sh`; judge the single-camera
framing at the first milestone) · **M4** UI polish (buffer particles, cash-in sequence, phase
transitions) · **M5** analytics, perf, store prep.

> **Keep this section current.** As phases finish, **rewrite** it to describe the project's
> state *now* — a single snapshot, not a changelog.
