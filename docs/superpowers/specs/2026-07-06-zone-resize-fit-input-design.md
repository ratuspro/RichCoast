# Zone resize, viewport fit, and whole-screen input — design

Date: 2026-07-06. Approved in conversation before implementation.

## Goals

1. Zone A's band 10% shorter; Zone B's band grows to absorb the freed space.
2. The canvas fits the browser window (keep aspect, neither dimension overflows) —
   in particular fix the bottom of Zone B being cut off on mobile.
3. Zone A aiming and Zone C's trap-door tap accept input anywhere on the screen,
   not just inside their own bands.

## Decisions (user-confirmed)

- "10%" applies to the **whole Zone A band** (HUD row + board): 563 → **507**.
- Zone B **keeps its authored gate layouts**; the extra 56px becomes free-fall
  headroom at the top of the band (no coordinate remapping).
- Scaling was already `Phaser.Scale.FIT`; the observed cutoff is the mobile
  `100vh`-includes-browser-chrome problem — fixed in CSS, not Phaser.

## Changes

### 1. Zone geometry (world stays 390×1238)

- `src/core/Layout.ts`: `ZONE_A_HEIGHT` = 42 + 465 = **507**; `ZONE_B_HEIGHT` =
  **687** (band y=551..1238). Zone C unchanged at 44px, now at y=507.
- `src/core/phaseGeometry.ts`: `PAN_DISTANCE` is redefined as the Zone B world
  overhang (`zoneB bottom − HEIGHT` = 394, unchanged in value), and
  `ARENA_VIEW_H_B` derives from it (`ARENA_VIEW_H_A − PAN_DISTANCE` = **71**)
  instead of the old `round(HEIGHT/5) − HUD_H`. Net framing: A-phase gives
  Zone A ~60% of the screen (was 2/3); B-phase crops Zone A to a 113px sliver
  (was 169). Pan tween, seam lock, and `?zone=b` static framing all follow.
- `phaseGeometry.test.ts` fraction assertions updated; `Layout.test.ts` is
  fully derived and passes unchanged. `tuning.ts` numbers are top-anchored
  absolutes and survive; comments updated.

### 2. Viewport fit

- `index.html`: size `#app` with `100dvh` (fallback `100vh`). No `main.ts` change.
- Found during verification: `#app`'s flex centering double-centered the canvas
  (Phaser's `CENTER_BOTH` already margins it), pushing it off-center in wide
  windows. Removed the flex centering; Phaser centers alone.

### 3. Whole-screen input

- `src/zoneA/AimController.ts`: drop the `pointer.y > zoneA.height` guard in
  `onPointerDown` — the phase freeze already gates it.
- `src/zoneC/ZoneCSystem.ts`: the door rectangle stops being interactive; a
  scene-level `POINTER_DOWN` listener calls `onTap()`, which already no-ops
  when locked or when Zone A has no ball (stalemate/RESTART overlap is safe).

Accepted side effects: debug-overlay taps also move the aim (A phase) and
trigger the door (B phase).
