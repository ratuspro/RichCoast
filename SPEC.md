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
- **Interface:** on activation, Zone C hands the ball to Zone B and gates the trap-door on Zone B's flight state (busy/empty). See [TECH_SPEC.md](TECH_SPEC.md).

---

## Zone B — Ball-Split Multiplier (bottom)

The lower portion of the screen. Once a ball falls through the trap-door it enters a physics arena with no player control. Gates split the ball into multiple copies of the same value; walls guide trajectories; collectors capture balls and cash them out as score.

- **Player input:** none — fully automatic once the ball enters.
- **Gate mechanic:** a ball hitting a ×N gate is replaced by N balls of the same value. Example: a ball of value 8 hitting a ×2 gate becomes 2 balls of value 8.
- **Cascading splits:** split balls can hit further gates and split again. A single ball can cascade into many copies.
- **Multiple balls in flight:** the player can drop another ball into Zone B before the previous ones have drained — all coexist simultaneously. The trap-door cannot activate again while any ball is in Zone B.
- **Miss:** a ball that reaches a collector without hitting any gate scores its raw value unchanged.
- **Scoring:** all balls that enter a collector add their value to the running total.
- **Feel:** dynamic and physical like pinball. Outcomes should feel layout-driven and readable, not random like a slot machine.
- **Interface:** Zone B receives the dropped ball at the shared entry point, owns scoring (it accumulates the running total for the HUD), and reports its flight state back to Zone C. See [TECH_SPEC.md](TECH_SPEC.md).

### Gate types

Three kinds of gate can appear in a Zone B layout. Each is a rigid line segment (with physical thickness) that splits any ball touching it.

- **Static gate** — defined by a center point and an angle. Most static gates are near-horizontal but can be tilted to redirect balls. Does not move.
- **Translating gate** — defined by two endpoints A and B. The gate slides back and forth between A and B on a fixed period. Center moves; angle stays constant.
- **Rotating gate** — defined by a pivot point C and a length. The gate spins continuously (or oscillates) around C.

### Collectors

A collector is a sensor area — any shape, any position — that captures balls and turns them into score. Balls can be collected at the bottom of the arena (the most common case, since gravity pulls them down), at the sides (for balls that ricochet off walls), or anywhere else the layout designer places one. There is no single mandatory funnel; instead, the layout defines one or more collectors whose combined capture area covers the reachable exits.

- Each collector is labelled with a multiplier (default ×1). A ball entering a ×M collector scores `value × M`.
- A ball that enters any collector is removed from play and its score is committed.

### Walls

A wall is a static line segment — no gate behavior, no score — used purely as a physical barrier. Walls let the layout route balls between collectors, prevent them from reaching dead zones, and create predictable rebound paths. A wall can be placed anywhere inside Zone B.

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

---

## Architecture

The code is split so two people can build it in parallel: **Dev 1 owns Zone A + Zone C +
the shared shell/HUD; Dev 2 owns Zone B (including scoring)**, coupled only through a
single agreed contract module. See [TECH_SPEC.md](TECH_SPEC.md) for the module layout,
ownership, interface contract, and isolated-development workflow.

---

## Open Questions

### Progression (Incentivize Merge)

**The problem:** Scoring is currently linear, so there is no incentive to merge. Four balls of value 1 sent to Zone B yield the same payout as one merged ball of value 4. This makes Zone A feel pointless as a puzzle — the optimal play is just to send balls as fast as possible.
**The goal:** Merging should produce superlinear value, so a single high-tier ball sent to Zone B is worth meaningfully more than the equivalent number of low-tier balls. This creates a real tension: wait and merge for a bigger payout, but risk Zone A filling up and triggering game over sooner.
**A rejected extreme:** One approach is to make merge results grow much faster than powers of 2 — e.g. 1+1→4, 4+4→64, 64+64→4096. This does make high-tier balls clearly superior, but the growth is too steep and makes low-tier play feel worthless rather than just suboptimal.
**Open question:** Find a curve between pure linear and steeply exponential that feels fair and readable. The player should be able to intuit that merging is rewarding without needing to do maths. It is also unclear whether a player who never merges — dropping one ball at a time directly to Zone B — can still score competitively; ideally they should fall behind but not be immediately punished.

### Losing Condition — Ball Buffer + Score Milestones

A second losing condition runs in parallel with Zone A overflow. The player has a finite **ball buffer** — a count of balls remaining to drop into Zone A, shown as a simple number in the HUD (e.g. ×12).

**Mechanics:**
- Every ball dropped into Zone A costs 1 from the buffer.
- Zone B tracks a **score milestone** — an escalating target that increases each time it is reached.
- When the milestone is reached, the buffer is immediately refilled by a fixed amount (tunable; e.g. +10 balls).
- If the buffer reaches 0, no new balls can be dropped. Balls already in Zone B continue to resolve — if one of them pushes the score over the current milestone before Zone B fully drains, the buffer refills and the run continues.
- If Zone B empties with the buffer still at 0 and the milestone not yet reached, the run ends (game over).

**Feel:** The player always has a visible next target, creating a clear "save yourself" goal when running low. Escalating milestones tighten pressure naturally over a long run without breaking the endless-run structure. Last-ball moments where Zone B just barely hits the milestone are intentionally dramatic.
