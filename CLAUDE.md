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
- **Zone B (bottom)** — no-control physics arena, PROCEDURALLY GENERATED and reshuffled after
  every drop; gates split a ball into copies; drained value fills a score bar. Filling it levels
  up and refills Zone A's finite ball buffer. Its barrier row is solid but for one narrow golden
  mouth on a door column: thread it and a brass chute drops the ball onto a gilded ×6–×8 gate.
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
  `ScoreBar`, `ZoneBGenerator` + `ZoneBGenParams` (the seeded arena grammar and its `Validate`
  contract) over `ZoneBLayouts` (the data model + the fixed funnel/collector), `DoorMath`
  (nearest-ball / sweep / split fan), and the typed static **`GameEvents`** seam. `RichCoast.Game` (`Assets/Game/Gameplay`) is the MonoBehaviour/plain-C#
  gameplay layer; `RichCoast.UI` (`Assets/Game/UI`) the uGUI shell; `RichCoast.App`
  (`Assets/Game/App/GameBootstrap.cs`) the composition root — the ONLY component the scene
  holds. `RichCoast.EditorTools` (`Assets/Editor`) = project setup + scene/asset builder.
  Tests: `Assets/Tests/EditMode` (Core math, mirrors master's vitest suites incl. the
  progression reachability guard) and `Assets/Tests/PlayMode` (scene smoke + screenshot).
- **Zones never call each other** — they talk only through `GameEvents` (typed C# events,
  same names/meanings as the Phaser bus: `BallDropped`, `ZoneBBusy/Empty`, `ScoreChanged`,
  `ScoreBarFilled/CashedIn/Changed`, `ScoreHarvested`, `BallBufferChanged`,
  `BufferSlotLaunched`, `ProgressionChanged`, `ArenaZoom`, `PhaseChanged`, `ZoneADepleted`,
  `GoldenGateHit`, plus `GameOver`).
  `GameEvents.Reset()` runs on every scene load and in test setup.
- **Tuning is ScriptableObjects**, editable live in play mode (`Assets/Game/Data`, created by
  the scene builder if missing): `TierLadder.asset`, `Progression.asset` (port of
  `progression.json`), **`ZoneBArena.asset`** (the Zone B generator's grammar knobs), and
  **`GameFeel.asset`** — THE feel file (physics, blast, squash/stretch, burst, audio, haptics,
  HUD timings).
- **World & screen:** Core tables are authored in the Phaser 390×844 **design px**;
  `DesignSpace`/`BoardGeometry` convert to world units (board = 10 units wide, funnel apex at
  the origin, y-up). ONE orthographic camera (`CameraRig`) fits board width to screen width
  (extra height on tall phones is headroom, never letterboxing) with the top edge pinned above
  the tray + HUD, inset by the safe area. uGUI canvases are ScreenSpaceCamera on that camera
  (so render-texture captures include them), CanvasScaler 1080×2340 match 0.5, HUD under a
  `SafeArea` container. `CameraRig.Pan` blends the A framing (top pinned) with the B framing
  (Zone B's bottom pinned) for the phase pan. The camera never zooms and the tray never
  grows: milestone arena growth shrinks Zone A's balls in place (`BallFactory.ArenaScale`),
  so every zone keeps its screen size for the whole run. Physics2D (Box2D): Zone A balls on layer 8,
  walls 9 (shared by both zones), Zone B balls 10, fresh split copies 11, gates 12 — layers are
  physics only, never render routing.
- **Juice:** `BallView` (child of the body: landing squash along the contact normal, merge-birth
  pop, blast punch, idle wobble), `MergeFx` (rim sparks + flash ring), `Sfx` (procedural
  synthesised clips, combo pitch-climb via `ComboPitch`), `Haptics` (Android
  `VibrationEffect` via JNI, no-op elsewhere), `ScoreFlyer` (the overlay bezier fly-up: the
  "+N" harvest token into the HUD total, and one brass dot per refilled buffer slot into the
  balls-left count). All driven by PrimeTween + `GameFeel.asset`.

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

**Milestones 1–4 plus the Zone B procedural revamp are implemented and green headlessly**
(EditMode 95 tests · PlayMode 19, incl. a Zone B drain test, a full depletion → pan → door-tap →
Zone B handoff test, three milestone tests (plain level-ups never zoom, the level-20 milestone
grows/drains/recolours, a milestone cash-in that arrives in phase B defers until the pan lands
in A) and three M4 polish tests (a refill launches one particle per slot and lands each after
its flight, the round's cash-in fires only after the bar holds full then drains out, the phase
ribbon reads "TAP THE DOOR" once the pan lands in B), plus five Zone B arena tests (eight seeds
each drain a ball, a drop down the golden column strikes the gilded gate on all eight, the
golden path outscores an ordinary drop on the same arena, the arena reshuffles exactly once and
only with the playfield clear, a reshuffle preserves the score bar and total); screenshots of
the A framing, the B framing and the first milestone in `Logs/game-scene*.png`). M1 ran on a
Pixel 7 (2026-09-11); **M2, M3, M4 and the Zone B revamp have not yet been built to the device**
— the user's hands-on feel sign-off (touch, haptics, audio, perf, door timing, milestone
shrink/drain pacing, refill particles, cash-in beat, ribbon, the golden fanfare and whether the
mouth reads as reachable-but-tight) and any resulting `GameFeel.asset` / `ZoneBArena.asset`
tuning are pending.

The full loop runs from one `GameBootstrap`:
- **Zone A** — tray, pooled material balls, drag-to-aim, finite buffer, merges with juice, death
  line, game over + RESTART. Aiming is frozen outside phase A and during a milestone zoom
  (one `ApplyFreeze` sink). A cash-in that arrives in phase B (or mid-zoom) defers its reward
  beat until the pan lands back in A; a roll-through burst composes its zoom factors by product
  into that deferred slot. A brass `DropHighlight` breathes under the ball the door would grab
  while the buffer is spent.
- **Milestones (M3)** — every 20 levels the draw window shifts (`Core.ProgressionCurve`) and
  `ZoneASystem` runs master's cash-in sequence: `TierLadder.MilestoneZoomFactor` (neutral
  growth × stage tightness; flat ×1.2 tail) → `ArenaGrowth.Grow`. Master grew the walls and
  zoomed a second camera out; here the tray, camera, death line and Zone C/B are FIXED and the
  board's contents recede instead: `BallFactory.ArenaScale` multiplies by the factor (radius =
  ladder radius ÷ scale for every future spawn/merge), and each live ball is frozen
  (`simulated = false`), tweened toward the funnel apex and down to 1/factor of its size over
  `GameFeel.milestoneZoomMs` (the funnel V is linear through the apex, so scaling about it maps
  the floor onto itself), then re-seated with physics resumed. Mass is compensated —
  `BallFactory.ApplyMass` writes density × scale² — so a shrunken ball keeps its ladder weight
  and the tier mass hierarchy never drifts. Live tiers therefore stay in the same world-size
  band forever (Box2D's comfort zone); no gravity/blast/speed normalisation is needed. When the
  balls land, `DrainBlacklisted` takes every ball below the new window floor
  (`Board.TakeBallsBelow`), raises `ZoneBBusy` up front, slides throwaway sprites to the Zone B
  entry (column clamped 12 design px inside the walls) and raises `BallDropped` per landing;
  `ArenaZoom(false)` only after the last one. The queue re-rolls off blacklisted tiers first.
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
- **Zone B** (`Gameplay/ZoneB/`) — a `Core.ZoneBGenerator` arena built in world space below Zone
  C and **re-rolled every time the zone drains empty**, so no two drops play the same board.
  Capsule-collider pine rails, static/kinematic gate slabs (`ZoneBGate`, painted sign +
  world-space TMP `X N`), trigger collectors, invisible containment. The playfield hangs off an
  `Arena` child; the backdrop, containment and score bar hang off the root and outlive every
  reshuffle. `ReleaseArena` rebuilds BEFORE raising `ZoneBEmpty` (so the door re-arms onto the
  board the player will actually play) and only with `inFlight == 0 && balls.Count == 0` plus a
  250 ms guard since the last arrival — Zone A's milestone drain raises several `BallDropped` in
  one tween batch. Pieces stagger in over `arenaPopInMs`. Small pooled `ZoneBBall`s (10 design px,
  layer 10; fresh split copies on layer 11 ignore gates for `splitGraceMs`); contacts are reported
  and resolved once per frame. Owns scoring via `Core.ScoreBar`: live per-level `ScoreBarFilled`
  wraps, the world-space bar + `+N` haul label, then `ScoreHarvested` → `ZoneBEmpty` →
  `ScoreBarCashedIn` after the wraps + dwell. Safety: `maxBallsInFlight` cap and a stuck-ball nudge.
- **The golden path (Zone B)** — the generated grammar is three rows: a BARRIER row whose cracks
  are narrower than a ball (so an ordinary drop always splits), then two spread rows with passable
  gaps, plus 2–3 guide diagonals, feeding the fixed funnel + collector. Exactly one barrier gap
  admits a ball: a 32 px golden mouth centred on one of the trap-door's interior sweep columns,
  flanked by two brass chute rails running down to a single gilded ×6–×8 gate (everything else
  stays ≤ ×4). The aperture is the narrower of the mouth and the rails' inner faces, and the
  mouth's jitter is capped at `aperture − ballRadius − clearance`, so a ball dropped down the right
  column ALWAYS reaches the gilded gate — the difficulty is the tap, not luck. A hit fans 6–8
  copies over `goldenSplitSpread` on two alternating radii (one arc would interpenetrate), plays
  `Sfx.Golden`, a heavy haptic and a gilded spark burst, and raises `GoldenGateHit`.
  `ZoneBGenerator.Validate` is the contract (band bounds, gate overlap, one passable barrier gap
  and it being the mouth, a clear mouth→gate channel, the mouth on an entry column, funnel region
  clear) and every guide diagonal must keep a full ball's width from every other rail — a sloped
  rail passing under a divider makes a wedge no ball can escape. 500 seeds are swept in EditMode.
- **Phase pan** — `PhaseDirector` runs `Core.PhaseMachine` (A ⇄ B with the queued-refill
  bounce) and tweens `CameraRig.Pan` 0→1: A pins the top edge above the tray + HUD, B pins Zone
  B's bottom (score bar) to the screen bottom, both safe-area inset; on the 390×844 design
  screen the two differ by exactly `DesignSpace.PanDistance` (394 px).
- **UI polish (M4)** — three beats, all knobs under `GameFeel.asset`'s "HUD" / "Score bar" /
  "Phase pan" headers. *Buffer particles:* `ZoneASystem` launches refill slots on the tick
  cadence (`BufferSlotLaunched(index)`) and LANDS each one `bufferFlightMs` later (count +1,
  blip, unlock — `cashInPending` clears with the last landing; a refill re-triggered mid-flight
  settles the in-flight slots at once). `HudView` answers each launch with `ScoreFlyer.LaunchDot`
  from a random point along the safe-area bottom to the balls-left count over the same time, so
  the dot arrives as the count pops by shared timing — Zone A never waits on a visual.
  *Cash-in drain-out:* Zone B's bar never snaps from full any more (`BarMode`): a roll-through
  still snaps between wraps, a mid-round last wrap drains down over `wrapDrainMs`, and the
  round's final wrap (arena drained) holds full through `settleDwellMs` then drains over
  `barDrainMs` (ease-in) — `ScoreBarCashedIn` fires only once the bar is empty, the hold
  replacing `UpdateResolve`'s dwell. The HUD chrome flashes brass + a light haptic when the
  harvest lands; the milestone fill pops per level. *Phase cues:* a themed brass ribbon under the
  HUD bar ("DROP" / "TAP THE DOOR") lifts out on `AToB`/`BToA` and drops in on `A`/`B`
  (`ribbonMs`); the aim ghost + guide fade over `aimFadeMs` instead of toggling; each pan start
  plays `Sfx.Pan(up)` (triangle glide, down into B / up back to A) + `panHaptic`.
- Physics layers: Zone A balls 8, walls 9 (shared), Zone B balls 10, grace 11, gates 12 —
  ignores set in `GameBootstrap.ConfigurePhysicsLayers`. Sfx cues: drop, merge (combo-pitched),
  buffer tick (climbing), goal, transition (door suck), multiply (combo-pitched), collect, pan
  down/up, game over.

Next: **on-device M2 + M3 + M4 + Zone B revamp feel session** (`Tools/build-android.sh`; judge
the milestone shrink beat, drain pacing, refill particle cadence, cash-in hold/drain, ribbon
timing, the golden fanfare/haptic, whether the golden mouth reads as reachable-but-tight, and the
reshuffle pop-in length) · **M5** analytics, perf, store prep.

> **Keep this section current.** As phases finish, **rewrite** it to describe the project's
> state *now* — a single snapshot, not a changelog.
