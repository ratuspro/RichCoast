# Progression Redesign — Tension from Board Pressure, not Drop Count

**Date:** 2026-07-12 · **Status:** implemented

## Problem

Playtesting found two interest-killers:

1. **Levels 1–24 had no tension** — tier 1–4 balls (radii 13–26) were too small to ever
   crowd the board, so the overflow line never threatened the player.
2. **Late game was tedious** — difficulty ramped by growing the buffer +1/level forever,
   reaching ~90 drops per A-phase. The buffer is double-edged (fuel *and* overflow risk),
   but past ~20 balls it read as a chore, not danger.

## Design principles (agreed in design Q&A)

- Tension comes from **ball size × count vs. arena room**, never from raw drop count.
- The buffer **oscillates** per level (pressure/harvest) instead of ramping forever.
- **Bigger low-tier balls** fix early flatness (chosen over faster milestones alone).
- Pacing target: ~15–20 min typical run, first milestone ~4–5 min in.
- One Zone B send can cross **multiple levels** (roll-through burst) — targets must grow
  per level so bursts self-limit, and the refill received is `bufferForLevel(landing level)`.
- `tierToValue = 3^(tier-1)` in `contracts.ts` stays untouched (frozen seam).

## Decisions

| Axis | Change |
|------|--------|
| Ball scales | `RADII` [13…78] → **[17, 22, 28, 34, 41, 50, 60, 71, 84, 99]** (~30% chunkier low end); `SPAWN_Y` 68→78, `DEATH_LINE_Y` 96→108 for tier-4 HUD clearance |
| Milestone cadence | `MILESTONE_EVERY` 25 → **15** (shifts at 15/30/45/60) |
| Buffer refills | Oscillation: base `min(9 + ⌊level/3⌋, 15)`, even levels +3 (harvest), odd −3 (pressure), milestones always +3. L1 = 6. **Cap = 18 drops forever** |
| Score targets | Anchors **20 @ L1, 60/85/120 @ L2–4, 5K @ L15, 400K @ L30, 33M @ L45, 2.7B @ L60**, geometric interpolation between anchors (no plateaus), existing `TAIL_TARGET_GROWTH` tail (now 3^(4/15)) past L60 |
| Draw windows | Soft ramp-in `[1,1]→[1,2]→[1,3]→[1,4]` over L1–4 (ceiling-only, no blacklist), then +4-tier jumps per milestone as before |
| Tightness | Unchanged values (0.92 / 1.05 / 0.85 / 1.05), now at 15/30/45/60 |
| Ball values | Unchanged (3^(tier-1)) |

## Why interpolated targets

One drain can roll the bar through several levels. Flat plateaus between sparse stages let a
monster drain wrap many times; geometric interpolation (~×1.34/level) makes each crossed
level immediately raise the next bar, so bursts self-limit to a celebratory few. Within each
window the ratio of typical-send to target traces an arc: cheap bars right after a milestone
(multi-level burst, power fantasy), tightening to ~1–2 full sends per level before the next
milestone (the squeeze the old curve lacked).

## Guard

A reachability test in `Progression.test.ts` asserts every level 1–60 is crossable with that
level's refill alone (window-max balls, perfect pair-merging ≈ ×1.5 per pairing round,
pessimistic ×4 gate cascade) — future retunes can't author a soft-lockable level.

## Files touched

`src/zoneA/tuning.ts`, `src/core/Progression.ts`, `src/core/progression.json`,
`src/core/Progression.test.ts`, `src/zoneA/ballMath.test.ts`, `SPEC.md`, `CLAUDE.md`.
