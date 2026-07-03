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

**"Bright Workshop" — industrial materials in a warm toy workshop.** Aimed between
hyper-casual and casual for UA-driven mobile: bright, readable, friendly.

**Balls are materials.** Each tier IS a physical material — the higher the tier, the more
valuable the stuff. The ladder is 20 materials in 5 families of 4, each family aligned
with one 4-tier draw window (so a 50-level window shift reads as advancing a material age):

1. **Primitives** [1–4]: Wood, Stone, Turquoise, Clay
2. **Metals** [5–8]: Copper, Iron, Steel, Silver
3. **Precious** [9–12]: Gold, Rose gold, Obsidian, Glass
4. **Gems** [13–16]: Sapphire, Emerald, Ruby, Diamond
5. **Exotic** [17–20]: Plasma, Magma, Void, Antimatter

Tiers past 20 wrap around the ladder with one gold ring per completed cycle. Textures are
procedural (no asset files): a soft top-lit sphere base plus a per-material detail pass
(wood grain, speckle, brushed sheen, gem facets, emissive glow…). Zone B's 10px balls use
a simplified recipe — there, colour is the identity, so any 4 consecutive tiers keep
strong mutual hue contrast. Materials also carry a **subtle physics feel** (narrow
restitution/friction/density multipliers: wood bounces, metal thuds and slides, gems
slip) — flavour, not balance.

**Environment**: warm-paper backdrop (`#f2e7d5`), light-pine structure (Zone A tray,
guide rails, gate signs), brass accents (HUD rule, trap-door hinges and markers, score
bar fill), warm-brown ink text. Danger stays red. The single source for ball look/feel is
`src/core/Materials.ts` (+ `MaterialPainter.ts`); the environment palette is
`src/core/Theme.ts`. A dev proof sheet of the full ladder renders at
`/material-preview.html`.

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

## Progression

The game has no explicit levels. Internally, each time the score bar fills and resets counts as one level tick. Three parameters change as the internal level advances — the available ball tiers, the buffer capacity, and the score bar target. These shifts happen silently: no level-up screen, no announcement. The game simply becomes slightly different from run to run as the player improves.

The exact values for each stage are defined in **`src/core/progression.json`**. The system reads the highest stage whose `fromLevel` is ≤ the current internal level and applies its parameters. Stages that are not listed are held at the previous stage's values.

---

### Ball Window

Balls queued in Zone A are drawn randomly from a sliding window of consecutive tiers. The window is always four tiers wide. As the internal level rises, the window shifts upward — the lowest available tier increases by one and a higher tier is added at the top. The player never sees tier labels; they experience this as "the balls are getting bigger."

- Early game: tiers 1–4 (smallest balls, easy to merge, low Zone B value).
- Mid game: tiers 3–6 (medium balls; merging is harder but each ball sent to Zone B is worth more).
- Late game: tiers 6–9 (large balls; the board fills quickly, every drop matters).

The window never exposes tiers beyond 10 (the game's maximum). Once the window reaches its final position it stays there.

---

### Buffer Capacity

The number of ball slots in the buffer starts at 4 and grows with the internal level. A larger buffer gives the player more room to breathe between score bar fills, partially offsetting the rising score bar target. Capacity grows slowly — roughly one extra slot every twenty-five levels — and is capped at 10.

---

### Score Bar Target

The number of points required to fill the score bar increases with each stage. A rising target means the player must send higher-value balls through Zone B to keep up, which in turn requires more merging in Zone A. This is the primary difficulty lever: early targets are reachable with low-tier balls; later targets demand merged, high-tier balls to fill the bar in a reasonable number of Zone C activations.

---

### Open Question — Merge Incentive

Scoring is currently linear: four balls of value 1 sent to Zone B yield the same payout as one merged ball of value 4. The progression system partially addresses this by shifting the ball window (making low-tier balls unavailable in later stages), but a direct superlinear merge bonus — where a higher-tier ball scores more than the sum of its source balls — remains an open design question. The window shift is the current answer; a value curve may be layered on top later.

### Losing Condition — Ball Buffer + Score Bar

A second losing condition runs in parallel with Zone A overflow. The player has a finite **ball buffer** — a supply of balls available to drop into Zone A. The buffer is replenished by progress in Zone B.

---

## Systems

### Ball Buffer

The ball buffer is the player's fuel supply. It holds a fixed number of balls (currently 4) that are queued and ready to drop into Zone A. Each slot is occupied by a randomly generated ball drawn from the low tiers, identical in distribution to Zone A's normal queue.

**Mechanics:**

- Dropping a ball into Zone A consumes one slot from the buffer. The next ball in the queue immediately becomes the active ball.
- The buffer is always full at the start of the run. Slots are not replaced as they are consumed — the count decreases until a refill event occurs.
- When the buffer reaches 0, no new balls can be dropped into Zone A. Any balls already on the board in Zone A or in flight through Zone B continue to play out normally.
- If the buffer is 0 and Zone B is completely empty (no balls in flight, no balls waiting to drain), the run ends — game over.
- A last-chance window exists: with buffer at 0, if a ball still in Zone B triggers a score refill before Zone B fully empties, the buffer is restored and the run continues.

**Capacity:**

The buffer currently holds 4 balls. Future progression may increase this capacity as the game advances, but the starting value is fixed at 4 for the initial version.

**Visual:**

The buffer is displayed in the HUD as a row of small ball icons, one per remaining slot. Each icon shows the ball's color, which corresponds to its tier — giving the player a visual read of what is coming. Empty slots are shown as dim outlines. The buffer sits at the top of the screen, in the Zone A HUD area.

---

### Score Bar

The score bar tracks cumulative Zone B output toward the next buffer refill. It is a horizontal fill bar displayed at the bottom of Zone B.

**Mechanics:**

- Every ball that drains into a Zone B collector adds its scored value (`value × collector multiplier`) to the bar.
- When the bar reaches its target, it resets to zero and the ball buffer is immediately refilled to its full capacity.
- The bar target is currently fixed. It is designed to escalate over time as the run progresses, but for the initial version the target stays constant throughout the run.
- The bar and the ball buffer are independent systems; they communicate through a single event (bar full → refill buffer). Neither system has direct knowledge of the other's internal state.

**Overall score:**

In addition to the bar, a running total of all points scored in the session is shown in the HUD. This cumulative number never resets and represents the player's final score at game over. The score bar and the overall score both draw from the same Zone B drain events but serve different purposes: the bar drives the refill loop, the total measures overall performance.

**Visual:**

The score bar is a horizontal progress bar spanning the bottom edge of Zone B. It fills left to right as balls drain. A small label shows the current total score above or beside the bar. When the bar fills, a brief flash or animation signals the refill before it resets.

---

**Feel:** The buffer and the bar create a visible cause-and-effect loop: drop balls into Zone A → merge and send them to Zone B → Zone B fills the bar → bar refills the buffer → repeat. Running low on buffer adds pressure without being an immediate death sentence; the player still has Zone B to bail them out. Last-ball moments where a single ball in Zone B just barely tops the bar and saves the run are intentionally dramatic.
