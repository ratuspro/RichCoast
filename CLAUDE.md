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
  stepping), `ComboPitch`, `NumberFormat.Compact`, `BallQueue`, and the typed static
  **`GameEvents`** seam. `RichCoast.Game` (`Assets/Game/Gameplay`) is the MonoBehaviour/plain-C#
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
  `SafeArea` container. Physics2D (Box2D): balls on layer 8, walls on layer 9 — layers are
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
   - `Tools/screenshot.sh` — renders a populated board to `Logs/game-scene.png` (1080×2340).
2. **Editor open + Coplay MCP** (`check_compile_errors`, `play_game`, `capture_*`,
   `get_unity_logs`, `execute_script`) for live iteration and visual checks. Menu items:
   `RichCoast/Apply Project Setup`, `RichCoast/Build Data Assets + Main Scene`,
   `RichCoast/Switch Active Target To Android`.

Feel verification tiers: EditMode tests (math) → PlayMode + screenshot (behaviour/layout) →
**on-device Android Build & Run** (the only honest judge of touch feel, haptics, perf).

## Status

**Milestone 1 (Zone A vertical slice + mobile UI shell) is implemented and green headlessly;
it has not yet been played on a device.** The scene boots from one `GameBootstrap`: tray
(walls + V funnel, pine rails on paper), pooled procedurally-painted material balls, drag-to-aim
ghost with a dashed drop guide, finite ball buffer (`ProgressionCurve.BufferForLevel`), merges
with blast/pop/sparks/flash/SFX/haptics, death line with proximity warning, overflow + stalemate
game-over with a full-screen RESTART overlay, and the HUD (milestone bar · compact score ·
"N left" + next-ball preview). **Zones B and C do not exist yet:** `Assets/Game/Gameplay/Dev/
ZoneBStub.cs` stands in for both — on `ZoneADepleted` it banks the board's total value ×4,
rolls the score bar through any levels crossed (real `ScoreBarFilled` level-ups → ticked
buffer refill), and fires `ScoreHarvested` so the fly-up plays; balls stay on the board, so
the tray fills and the death line ends the run. Milestone arena growth / window-shift drains /
theme moods are M3. EditMode: 57 tests. PlayMode: boot, merge, no-merge stacking, screenshot.

Next: **M2** Zone C door + Zone B arena + phase pan (delete the stub) · **M3** progression
milestones (arena growth as ortho-size tween, blacklist drains, palette cross-fades) · **M4**
UI polish pass · **M5** analytics, perf, store prep. First real step: an Android Build & Run
feel session, tuning `GameFeel.asset` on the device.

> **Keep this section current.** As phases finish, **rewrite** it to describe the project's
> state *now* — a single snapshot, not a changelog.
