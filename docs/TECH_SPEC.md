# Technical Spec — Unity Architecture

Companion to [SPEC.md](SPEC.md). The gameplay design lives there and is **unchanged** by the
migration; this document defines how that design is structured in Unity.

The game was originally built in Phaser 4 + TypeScript (see git history before `10baa58`).
The Unity rebuild is a faithful port of the design, not a redesign. Where this document names
an old TS module, that module's logic and its unit tests were ported directly.

## Platform targets

- **Android first** (min API 24, ARM64, IL2CPP, Vulkan + GLES3), iOS to follow.
- **Perf budget: ~2019 budget phone** (Snapdragon 4xx class, 2–3 GB RAM) at **60 fps**.
  Consequences that are binding on all code below: no post-processing, no realtime shadows,
  every runtime object pooled, zero per-frame managed allocation in the gameplay loop.
- Portrait only.

## Simulation and rendering

**Gameplay is 2D.** All bodies are `Rigidbody2D` + 2D colliders on the XY plane at z = 0,
simulated by Unity's Box2D at a fixed 1/60 step. This matches the original Matter.js design
exactly and is the cheapest reliable option on low-end hardware.

**Rendering is free to become 3D.** Because gameplay never depends on the camera projection,
views are URP-lit meshes and the camera can move from orthographic to perspective — with real
lighting and depth — as part of a later art pass, without touching a line of gameplay code.

## Assembly layout

```
Assets/Game/
  Core/       RichCoast.Core.asmdef       pure C#: contracts, event bus, layout, math, state machines
  Data/       RichCoast.Data.asmdef       ScriptableObjects -> plain structs for Core
  Gameplay/   RichCoast.Gameplay.asmdef   MonoBehaviours, Physics2D, Input System, analytics
  View/       RichCoast.View.asmdef       view components, palette, effects (swappable)
  Scenes/Game.unity
Assets/Tests/EditMode/  RichCoast.Tests.EditMode.asmdef
Assets/Tests/PlayMode/  RichCoast.Tests.PlayMode.asmdef
Tools/run-tests.sh
```

Dependencies flow one way: `View`, `Gameplay` → `Data` → `Core`. **Core does not reference
`UnityEngine` types beyond `Vector2` / `Mathf`**, which is what makes every ported algorithm
testable in a headless batchmode run with no scene, no prefabs and no play loop.

## The seam

`Core/Contracts.cs` is the port of the old `core/contracts.ts` and carries the same rule: it is
the *only* coupling between the zones. Zones never import each other; they exchange typed
events over `Core/EventBus.cs` (no string event names — a generic bus keyed by payload type).

Events, unchanged from the original design:

| Event | Direction | Payload |
|---|---|---|
| `BallDropped` | Zone C → Zone B | `value, tier, x` (player-chosen entry column) |
| `ZoneBBusy` / `ZoneBEmpty` | Zone B → Zone C | — (trap-door cooldown) |
| `ScoreChanged` | Zone B → HUD | `total` |
| `ScoreBarChanged` | Zone B → HUD | `filled, target` |
| `ScoreBarFilled` | Zone B → all | — (once **per level** in a roll-through) |
| `ScoreBarCashedIn` | Zone B → PhaseDirector | — (whole roll finished; pan-up trigger) |
| `ScoreHarvested` | Zone B → HUD | `amount, x, y` (flying score token) |
| `BallBufferChanged` | Zone A → HUD | `count` |
| `ProgressionChanged` | Zone A → all | `level, minTier, maxTier, bufferCapacity, scoreBarTarget` |
| `ArenaZoom` | Zone A → Zone C | `active` |
| `PhaseChanged` | PhaseDirector → all | `phase` |
| `ZoneADepleted` | Zone A → PhaseDirector | — |
| `ThemeChanged` | ThemeDirector → all | — |

Every zone implements `IGameSystem { Create(); Tick(float dt); Dispose(); }`. `GameRoot` (the
single scene bootstrap) constructs the systems, wires the bus and ticks them, and exposes a
`ZoneMode` enum that swaps a real zone for a stub — the Unity replacement for the old `?zone=`
URL flag, and the reason Zone A could be built and played before Zones B and C existed.

Systems announce their opening state on their first **tick**, not in `Create()`: construction runs
system by system, so anything emitted during it is missed by every system built later.

## View seam

Gameplay components never touch renderers. Each entity holds an interface handle
(`IBallView`, `IArenaView`, `IHudView`) and pushes data at it — tier, radius, position, state.
The migration ships flat placeholder implementations (`FlatBallView`, `HudView`,
`PlaceholderArt` — all generated at runtime, no art assets); the "Bright Workshop" material
ladder (20 materials, 5 families, per-tier physics feel) is described in SPEC.md and lands in a
later art pass as an alternate implementation of the same interfaces.

**A view never shares a transform with a body.** A view sizes itself by scaling its own
transform, and a Unity collider inherits its transform's scale — so a view parented to a ball
would silently scale the physics with the sprite. Ball views are siblings of ball bodies,
positioned each frame through the seam. `BallPhysicsTests` pins this: a resting ball must sit
exactly one radius above the floor, which fails the moment a collider's size drifts from the
radius the rules use.

## Cameras

`Gameplay/CameraRig.cs` owns the two cameras and derives both from one pan value:

- The **main camera** shows the world 1:1 and scrolls between the phases; Zone B and Zone C ride it.
- The **arena camera** draws Zone A into a viewport below the HUD at zoom `1/arenaScale`, so a
  milestone's growth gives the board room without changing how big the balls look. Its viewport
  shrinks with the pan, top-cropping Zone A to a sliver in the B phase.

The split is by layer (`PhysicsLayers`), which is also why every Zone A *view* object must be put
on the Zone A layer — a view left on the default layer is simply never drawn. Aiming reads through
the arena camera, since that is the projection the board is drawn under.

## Coordinate spaces

The design is authored y-DOWN from the world's top-left, as the original was; Unity is y-up.
`Core/DesignSpace.cs` is the only place the two meet (`worldY = -designY`; one design unit is
one world unit, so radii and speeds carry over untouched). Gameplay reasons in design space,
which is what lets every tuned constant be compared directly against the original build.

`Core/ArenaGeometry.cs` derives Zone A's boundary from an arena scale, anchored to the funnel
floor: the arena grows upward and outward at milestones, never into Zone B, and gravity and the
speed thresholds scale with it so the drop feel is identical at every milestone.

## Layout

`Core/Layout.cs` ports the original world geometry: a **390 × 1238** design-space world, taller
than the screen, with Zone A (42 HUD + 465 board), Zone C (44) and Zone B (687) bands. Nothing in
the world moves between phases — the cameras do, through a single `pan ∈ [0, PanDistance]` proxy
(`Core/PhaseGeometry.cs`, ported from `phaseGeometry.ts`), which is what keeps the
arena-bottom / Zone-C seam locked mid-pan.

Unlike the original, the design height is **not** assumed to equal the device screen: layout
resolves the design world against the real aspect ratio (clamped, so an odd editor aspect cannot
frame the board out of existence) and against `Screen.safeArea`, so notches and tall or short
devices are handled rather than letterboxed.

## Ported pure logic

These modules are direct C# ports and keep their original unit tests:

| Unity | Original | What it owns |
|---|---|---|
| `Core/Progression.cs` | `core/Progression.ts` | draw window (incl. +2 tail milestones), buffer oscillation, geometric score-bar targets |
| `Core/BallMath.cs` | `zoneA/ballMath.ts` | radius growth, friction/density per tier, arena growth factors, death-line/overflow rules, blast impulse, nearest-door pick |
| `Core/MergeLogic.cs` | `zoneA/MergeLogic.ts` | merge eligibility and resulting tier |
| `Core/SettleGate.cs` | `zoneA/settleGate.ts` | "board settled" accumulator with hold + hard timeout |
| `Core/ScoreBar.cs` | `zoneB/ScoreBar.ts` | fill, multi-level roll-through, overflow forfeit |
| `Core/PhaseMachine.cs` | `core/phaseMachine.ts` | A / A_TO_B / B / B_TO_A transitions and the queued-refill bounce |
| `Core/PhaseGeometry.cs` | `core/phaseGeometry.ts` | per-phase camera framing |
| `Core/ComboPitch.cs` | `core/comboPitch.ts` | combo pitch-rise shared by merges and Zone B multiplies |
| `Core/BallQueue.cs` | `zoneA/BallQueue.ts` | draw-window sampling, next-ball preview, blacklist re-roll |
| `Core/BallBuffer.cs` | (redesigned) | fuel supply: spend, drip-fed refills, burst bonus, last-chance window |
| `Core/DoorSweep.cs` | `zoneC/ZoneCSystem.ts` | the trap-door's nine-column ping-pong marker |
| `Core/ZoneBLayout.cs` | `zoneB/zoneLayout.ts` | gate/wall/collector defs and gate motion |

## Tuning data

Authored values live in ScriptableObjects (`Assets/Game/Data`), each exposing a `ToTable()`
that returns a plain struct for Core:

- `ProgressionConfigSO` — the stage table ported verbatim from `core/progression.json`
  (`fromLevel`, `ballWindow`, `scoreBarTarget`, `tightness`, `palette`).
- `BallTierTableSO` — base radii and the 20-material ladder with per-material physics feel.
- `DefaultZoneBLayouts` — the two authored playfields, ported verbatim; one is picked per run.
  `ZoneBSystem` takes an optional layout override so a cascade can be reproduced in a test instead
  of depending on which playfield the run drew.

Values are **not** re-tuned during the migration. The ported tests encode the tuned behaviour.

## Migration status

| Area | State |
|---|---|
| Foundations, seam, data layer, ported rules | done, tested |
| Zone A: aim, drop, merge, death line, buffer, HUD, game over | done (grey-box) |
| Zone C: sweeping marker, tap-to-freeze, nearest-by-edge grab, transit | done (grey-box) |
| Zone B: authored layouts, gates, cascading splits, walls, collectors, scoring | done (grey-box) |
| Milestone arena growth + camera zoom + blacklist drain | done |
| Two-camera A/B framing (`CameraRig`) | done |
| Audio, art pass, Android build scripting, iOS | not started |

`ZoneMode.ZoneAC` on `GameRoot` still swaps the real Zone B for `StubZoneB`, which is the fastest
way to tell which side of the seam a bug is on.

## Tools

Generated rather than hand-authored, so a clean checkout rebuilds byte-identically:

- `Rich Coast/Set Up Project` (menu, or `-executeMethod RichCoast.EditorTools.ProjectSetup.SetUpAll`)
  applies the low-end player settings, names the zone collision layers, creates the tuning assets
  and rebuilds the scene.
- `Tools/run-tests.sh [--platform EditMode|PlayMode] [--filter …]` — headless test run, exits
  non-zero with the failing test names.
- `Tools/screenshot.sh` — renders the running game to `Logs/game-scene.png`. It uses its own
  design-space camera covering the whole 390×1238 world: a batchmode run has no backbuffer to
  capture and reports a landscape screen, so the live rig would frame almost nothing.

## Verification

The EditMode suite covers the rules. The PlayMode suite covers the wiring and the physics, which
is where this migration's real bugs have been:

- the scene boots and the physics world is configured;
- a dropped ball rests on the floor **at its own radius** (catches a collider whose size has
  drifted from the radius the rules use);
- a busy, merging board leaks nothing through the walls;
- two balls merge into one of the next tier;
- a Zone B round splits, drains, scores, and always returns to empty — a round that never closes
  would lock the trap-door for the rest of the run;
- a cascade never exceeds the ball cap;
- a milestone grows the arena by the authored factor, locks input while it does, and drains the
  tiers that just left the draw window.

Android build scripting and on-device profiling are a later milestone.
