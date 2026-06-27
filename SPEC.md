2D Arcade — Mobile Browser Game

## Overview

A portrait-mode mobile game split into two gameplay zones. The top zone plays like a merge-puzzle (fruit-merge style); the bottom zone plays like pinball. The player's skill lies in deciding which merged ball to send down and when — the bottom zone then amplifies its value automatically.

---

## Zone A — Merge-Two (top)

The upper portion of the screen. The player drops balls one at a time from the top. When two balls of equal value collide they merge into a single ball of the next tier. Balls accumulate and stack; the zone fills up over time.

- **Player input:** drag horizontally to aim, release to drop.
- **Merge rule:** same-value + same-value → next-tier ball (e.g. 2+2→4, 4+4→8).
- **Physics:** gravity pulls balls down; they rest on each other and on the floor.
- **Failure condition:** TBD (e.g. balls overflow past the top boundary).

---

## Zone C — Trap-Door Transition (boundary)

A narrow tunnel/vacuum at the boundary between Zone A and Zone B. The player activates it manually. When activated, the closest ball to the tunnel entrance is pulled through — one ball per activation. This is the only moment the player bridges the two zones.

- **Player input:** tap the trap-door to activate.
- **Behaviour:** the nearest ball is sucked in and dropped into Zone B.
- **Constraint:** one ball passes at a time; the tunnel has a short cooldown before it can be activated again.
- **Strategy:** the player cherry-picks which merged ball to send down, trading a valuable ball from Zone A for a scoring run in Zone B.

---

## Zone B — Ball-Drop Multiplier (bottom)

The lower portion of the screen. Once a ball falls through the trap-door it enters a physics-driven arena with no player control. Moving elements (gates, bumpers) multiply the ball's value as it passes through them. The ball eventually drains into a funnel at the bottom, cashing out its final value as score.

- **Player input:** none — fully automatic once the ball enters.
- **Elements:** moving gates with multiplier values (×2, ×3, etc.), bumpers that redirect the ball.
- **Scoring:** ball value × all multipliers encountered = points added to the total score.
- **Feel:** exciting and dynamic like pinball; outcomes should feel skill-influenced (via Zone C timing), not purely random like a slot machine.

---

## Scoring

- Endless high-score mode — no win condition, play until failure.
- Score = cumulative sum of all ball-drop payouts from Zone B.
- Zone A merging itself does not award points directly; value is realised only when a ball passes through Zone C into Zone B.

---

## Open Questions

- Exact merge tiers and ball value progression.
- Failure condition for Zone A overflow.
- Gate movement patterns in Zone B (scripted vs. procedural).
- Aesthetics and visual theme (not yet decided).
- Whether there is a timer or move limit per session.

---

## Tech Requirements

- Runs in a mobile browser (no native install).
- Deployable to a server and served online.
- Full-screen mode on mobile browser (portrait, ~390×844).
- Framework: Phaser 4 + TypeScript + Vite (current direction, not locked in).
- No external asset dependencies — textures generated procedurally where possible.
