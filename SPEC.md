2D Arcade — Mobile Browser Game

## Overview

A portrait-mode mobile game split into two gameplay zones. The top zone is a merge puzzle (fruit-merge style); the bottom zone is a ball-splitting physics arena. The player's skill lies in building high-value balls in Zone A and deciding when to drop them into Zone B — where gates split each ball into more balls, all draining into a funnel to cash out as score.

---

## Zone A — Merge-Two (top)

The upper portion of the screen. The player drops balls from the top. Same-value balls that collide merge into a single ball of the next tier. The board fills over time; the game ends when balls overflow the top boundary.

- **Player input:** drag horizontally to aim, release to drop.
- **Ball queue:** there is always a ball ready to drop at the top. As soon as one is released, the next appears immediately — the player is never waiting.
- **Next ball preview:** the upcoming ball's tier is shown so the player can plan their drop.
- **Merge tiers:** powers of 2 — 1 → 2 → 4 → 8 → 16 … up to ~10 distinct tiers.
- **Ball generation:** newly queued balls are drawn from the lower tiers (e.g. tiers 1–4) so the board doesn't immediately fill with large balls.
- **Physics:** gravity-driven; balls rest on each other and on the floor.
- **Failure condition:** any ball crosses the top boundary of Zone A — game over.

---

## Zone C — Trap-Door Transition (boundary)

A vacuum tunnel at the boundary between Zone A and Zone B. The player activates it manually. The nearest ball is pulled through and dropped into Zone B.

- **Player input:** tap the trap-door to activate.
- **Behaviour:** the closest ball to the tunnel entrance is sucked through — one ball per activation.
- **Cooldown:** the tunnel cannot be activated again until Zone B is empty (no balls in flight).
- **Strategy:** the player chooses the best moment to sacrifice a merged ball from Zone A for a Zone B payout.

---

## Zone B — Ball-Split Multiplier (bottom)

The lower portion of the screen. Once a ball falls through the trap-door it enters a physics arena with no player control. Moving gates split the ball into multiple copies of the same value. All copies eventually drain into a funnel at the bottom, each contributing its value to the score.

- **Player input:** none — fully automatic once the ball enters.
- **Gate mechanic:** a ball hitting a ×N gate is replaced by N balls of the same value. Example: a ball of value 8 hitting a ×2 gate becomes 2 balls of value 8.
- **Cascading splits:** split balls can hit further gates and split again. A single ball can cascade into many copies.
- **Multiple balls in flight:** the player can drop another ball into Zone B before the previous ones have drained — all coexist simultaneously. The trap-door cannot activate again while any ball is in Zone B.
- **Miss:** a ball that reaches the funnel without hitting any gate scores its raw value unchanged.
- **Scoring:** all balls that drain into the funnel add their value to the running total.
- **Feel:** dynamic and physical like pinball. Outcomes should feel layout-driven and readable, not random like a slot machine.

---

## Scoring

- **Mode:** endless single run — no win condition, no levels, no meta-progression.
- **Score formula:** sum of values of all balls that drain from Zone B's funnel.
- **Example:** tier-4 ball (value 8) enters → hits ×2 gate → 2 balls of 8 → one hits ×3 gate → 3 balls of 8 + 1 ball of 8 = 4 balls of 8 → all drain → score += 32.
- Zone A merging does not award points. Value is realised only when balls exit Zone B.

---

## Visual Theme

Abstract / geometric. Bold shapes, flat colors, no figurative metaphors. Dark background with high-contrast elements. Exact palette TBD.

---

## Open Questions

- Gate movement patterns in Zone B (scripted oscillation vs. procedural / random).
- Exact layout and number of gates per Zone B session.
- Whether Zone B layout is fixed or changes between runs.
- Whether there is a visual or audio escalation as score grows (juice / feedback).
- Ball drop indicator in Zone A (ghost line showing where ball will land).

---

## Tech Requirements

- Runs in a mobile browser — no native install required.
- Deployable to a server and served online.
- Full-screen portrait mode on mobile browser (~390×844).
- Framework: Phaser 4 + TypeScript + Vite (current direction, not locked in).
- No external asset dependencies — textures generated procedurally where possible.
