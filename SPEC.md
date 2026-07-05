# 2D Arcade — Mobile Browser Game

## Overview

A portrait-mode mobile game split into two gameplay zones. The top zone is a merge puzzle
(fruit-merge style); the bottom zone is a ball-splitting physics arena. The player's skill
lies in building high-value balls in Zone A and deciding when to drop them into Zone B —
where gates split each ball into more balls, all draining into a collector to cash out as
score.

---

## Zone A — Merge-Two (top)

The upper portion of the screen. The player drops balls from the top. Same-**value** balls
that collide merge into a single ball of the next tier. The board fills over time; the game
ends on overflow or on a full stalemate (see Failure conditions).

- **Player input:** drag horizontally to aim, release to drop.
- **Ball queue:** there is always a ball ready to drop at the top. As soon as one is
  released, the next appears immediately — the player is never waiting. A top-right queue
  row shows the next-ball preview alongside the balls-left-to-drop count (driven by the
  ball buffer, below).
- **Merge tiers:** uncapped — merging never stops. Two equal-value balls merge and the
  result **triples** the value (`tierToValue(tier) = 3^(tier-1)`, since merging two equal
  balls yields `1.5×(V+V) = 3V`). The base radius/colour table covers tiers 1–10; tiers
  climb past that with balls growing by formula (`RADIUS_GROWTH` in `tuning.ts`) — there is
  no gameplay ceiling.
- **Ball generation:** newly queued balls are drawn from a sliding 4-tier **draw window**
  (see Progression) so the board never fills with balls far above what the player can merge.
- **Physics:** gravity-driven; balls rest on each other and on the floor. Physics feel is
  normalized across arena scale changes (see Arena Growth) so drops, shoves, and settling
  look identical at every milestone.
- **Failure conditions** (either ends the run):
  - **Overflow** — a ball rests above the death line for about a second. A red warning line
    appears once a slow ball is within a band just below the boundary, before it's actually
    game-over.
  - **Stalemate** — the ball buffer is empty, Zone A is empty, and Zone B is empty (nothing
    left to play).
  - On game over: the physics world pauses and a full-screen overlay shows the final score
    and a RESTART button.

### Arena Growth (milestones)

Because balls grow without bound, the arena itself grows to make room, on a schedule tied to
the draw-window shifts below.

- Every 50 levels, Zone A input freezes and the arena — ceiling, walls, floor/funnel width —
  expands outward (never into Zone B). A dedicated camera zooms out to match, so relative
  ball sizes and positions on screen hold steady.
- The growth factor per milestone is the *neutral match* for the window's new max tier (the
  radius ratio that keeps that ball's on-screen size constant) multiplied by an authored
  **tightness** value per stage (`progression.json`): `<1` = a tighter, harder squeeze; `>1`
  = a roomier breather. The current authored rhythm alternates squeeze → breathe with
  deepening squeezes over time.
- Any ball still on the board whose tier just got blacklisted (see below) drains into Zone B
  automatically in one synchronized slide, so the board never gets stuck holding
  now-forbidden tiers.
- Zone C is locked for the duration of the zoom (and the drain), then re-arms.
- Past the last authored window shift, milestones self-heal into plain level-ups with no
  growth.

---

## Zone C — Trap-Door Transition (boundary)

A vacuum tunnel at the boundary between Zone A and Zone B. The player activates it manually,
timing a moving marker to choose where the ball lands in Zone B.

- **Player input:** while armed, a lit marker steps back and forth across nine evenly-spaced
  positions along the boundary (ping-pong sweep). Tapping freezes the marker on its current
  position — that column becomes the Zone B entry point.
- **Behaviour:** on tap, the ball nearest the trap-door (by edge distance, not centre — so a
  large ball whose surface reaches closer still wins) is pulled through. The trap-door locks
  immediately (before the transit finishes) so Zone A's stalemate check can't misfire
  mid-transit. A brief suck-then-pop animation carries the ball from its on-screen Zone A
  position/size to the frozen column at Zone B scale.
- **Cooldown:** the trap-door cannot activate again until Zone B reports empty (no balls in
  flight).
- **Strategy:** the player chooses both *when* to sacrifice a merged ball from Zone A, and
  *where* it lands in Zone B — landing column is a timing skill, not fixed.
- **Interface:** on activation, Zone C hands the ball's value/tier and chosen column to Zone
  B, and gates the trap-door on Zone B's flight state (busy/empty). See
  [TECH_SPEC.md](TECH_SPEC.md).

---

## Zone B — Ball-Split Multiplier (bottom)

The lower portion of the screen. Once a ball falls through the trap-door it enters a physics
arena with no player control. Gates split the ball into multiple copies of the same value;
walls guide trajectories; a collector captures balls and cashes them out as score.

- **Player input:** none — fully automatic once the ball enters.
- **Gate mechanic:** a ball hitting a ×N gate is replaced by N balls of the same value.
  Example: a ball of value 8 hitting a ×2 gate becomes 2 balls of value 8.
- **Cascading splits:** split balls can hit further gates and split again. A single ball can
  cascade into many copies.
- **Multiple balls in flight:** the player can drop another ball into Zone B before the
  previous ones have drained — all coexist simultaneously. Balls are small (10px radius) and
  collide with each other, so they pile and nudge in the cascade. The trap-door cannot
  activate again while any ball is in Zone B.
- **Miss:** a ball that reaches the collector without hitting any gate scores its raw value
  unchanged.
- **Scoring:** every ball that enters the collector adds its value (× the collector's
  multiplier) to the running total, and to the score bar (see Score Bar).
- **Feel:** dynamic and physical like pinball. Outcomes should feel layout-driven and
  readable, not random like a slot machine.
- **Interface:** Zone B receives the dropped ball at the player-chosen entry point, owns
  scoring (accumulates the running total and score-bar progress for the HUD), and reports
  its flight state back to Zone C. See [TECH_SPEC.md](TECH_SPEC.md).

### Layout

The playfield is one of two hand-built "shelf cascade" layouts (`LAYOUT_1`, `LAYOUT_2`),
picked at random each run (including every restart). Both are stacked horizontal gate rows
split by vertical/diagonal guide rails, funnelling down into a single bottom collector via
two funnel-ramp walls. Gate multipliers are tuned (≤4) so cascades stay balanced; higher
multipliers are painted as green signs, lower as brass, each with a stencilled `×N` label.

### Gate types

Three kinds of gate can appear in a Zone B layout. Each is a rigid line segment (with
physical thickness) that splits any ball touching it.

- **Static gate** — defined by a center point and an angle. Most static gates are
  near-horizontal but can be tilted to redirect balls. Does not move.
- **Translating gate** — defined by two endpoints A and B. The gate slides back and forth
  between A and B on a fixed period. Center moves; angle stays constant.
- **Rotating gate** — defined by a pivot point and a length, spinning continuously at a
  fixed angular speed around that pivot.

### Collectors

A collector is a sensor area — any shape, any position — that captures balls and turns them
into score. The current layouts both use a single bottom collector (fed by two funnel
ramps), but the mechanism supports any number of collectors at any position (bottom, sides,
etc.) with an independent multiplier each.

- Each collector is labelled with a score multiplier (default ×1). A ball entering a ×M
  collector scores `value × M`.
- A ball that enters a collector is removed from play and its score is committed.

### Walls

A wall is a static line segment — no gate behavior, no score — used purely as a physical
barrier. Walls route balls between collectors, prevent them from reaching dead zones, and
create predictable rebound paths.

---

## Scoring

- **Mode:** endless single run — no win condition, no meta-progression, but an internal
  level counter drives progression (see below).
- **Score formula:** sum of values of all balls that drain into Zone B's collector, ×
  collector multiplier.
- **Example:** tier-4 ball (value 27) enters → hits ×2 gate → 2 balls of 27 → one hits ×3
  gate → 3 balls of 27 + 1 ball of 27 = 4 balls of 27 → all drain → score += 108.
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

**Audio**: procedural Web Audio (soft synth bells/marimba, no asset files), one hook per
zone event (drop/merge in Zone A, transition in Zone C, multiply/collect/goal in Zone B).
Merge and Zone B multiply chains pitch-climb through a fast combo window. Volumes are tuned
by relevance (goal loudest, collect quietest). Muted with **M**.

---

## Open Questions

- Ball drop indicator in Zone A (ghost line showing where a dropped ball will land) — not
  yet implemented.
- Whether a direct superlinear merge bonus is warranted (see Merge Incentive below) — the
  draw-window shift is the current, indirect answer.
- Whether more than the current two Zone B layouts are worth authoring, and whether layout
  selection should ever be anything other than uniform-random per run.

---

## Tech Requirements

- Runs in a mobile browser — no native install required.
- Deployable to a server and served online.
- Full-screen portrait mode on mobile browser (390×844).
- Framework: Phaser 4 + TypeScript + Vite.
- No external asset dependencies — all textures and audio generated procedurally.

---

## Architecture

The code is split so two people can build it in parallel: **Dev 1 owns Zone A + Zone C +
the shared shell/HUD; Dev 2 owns Zone B (including scoring)**, coupled only through a
single agreed contract module. See [TECH_SPEC.md](TECH_SPEC.md) for the module layout,
ownership, interface contract, and isolated-development workflow.

---

## Progression

The game has no explicit levels in the win/lose sense, but an internal level counter drives
difficulty. Each time the score bar fills and resets counts as one level tick. Parameters
that change as the internal level advances — the ball draw window, the buffer capacity, the
score bar target, and (at window-shift milestones) the arena scale — are read from
**`src/core/progression.json`**. The system applies the highest stage whose `fromLevel` is
≤ the current internal level; stages not listed hold at the previous stage's values. These
shifts happen silently: no level-up screen, no announcement — the game simply becomes
different as the player advances. (The one exception is the arena-growth camera zoom at
window-shift milestones, which is a deliberate, visible beat — see Zone A above.)

### Ball Window

Balls queued in Zone A (both the live queue and the ball buffer) are drawn randomly from a
sliding window of consecutive tiers, always four tiers wide. The window holds at `[1,4]`
through level 49, then jumps `[5,8]` / `[9,12]` / `[13,16]` / `[17,20]` at each 50-level
milestone. At each jump, the lowest 4 tiers become **blacklisted**: the in-hand and
next-preview balls are re-rolled off any blacklisted tier, and any obsolete balls still on
the board are drained into Zone B in one synchronized slide (see Arena Growth). The window
never exposes tiers beyond 20 in the current authored schedule; the underlying tier system
itself has no ceiling.

- Early game: tiers 1–4 (smallest balls, easy to merge, low Zone B value).
- Mid game: tiers 5–8 (copper→silver; merging is harder but each ball sent to Zone B is
  worth more).
- Late game: tiers 9–20 (precious → gems → exotic; the board fills quickly, every drop
  matters).

### Buffer Capacity

The number of ball slots in the buffer starts at 4 and grows with the internal level
(currently up to 10, per the authored stages in `progression.json`). A larger buffer gives
the player more room to breathe between score bar fills, partially offsetting the rising
score bar target.

### Score Bar Target

The number of points required to fill the score bar increases with each authored stage —
from single digits at level 1 up into the billions by level 200, tracking the (powers-of-
three) per-window value magnitudes. A rising target means the player must send higher-value
balls through Zone B to keep up, which in turn requires more merging in Zone A. This is the
primary difficulty lever. The exact target curve is a starting point to keep tuning by
playtest, not a locked design.

### Open Question — Merge Incentive

Scoring is currently linear: four balls of value 1 sent to Zone B yield the same payout as
one merged ball of value 4 (well, value 3, since a merge triples rather than doubles — see
Zone A). The progression system partially addresses this by shifting the ball window
(making low-tier balls unavailable in later stages), but a direct superlinear merge bonus —
where a higher-tier ball scores more than the sum of its source balls — remains an open
design question.

### Losing Condition — Ball Buffer + Score Bar

A second losing condition runs in parallel with Zone A overflow. The player has a finite
**ball buffer** — a supply of balls available to drop into Zone A. The buffer is replenished
whenever the score bar fills.

---

## Systems

### Ball Buffer

The ball buffer is the player's fuel supply. It holds a fixed number of balls (see Buffer
Capacity) that are queued and ready to drop into Zone A, drawn from the same ball window as
the live queue.

**Mechanics:**

- Dropping a ball into Zone A consumes one slot from the buffer. The next ball in the queue
  immediately becomes the active ball.
- The buffer is always full at the start of the run. Slots are not replaced as they are
  consumed — the count decreases until a refill event occurs.
- When the buffer reaches 0, no new balls can be dropped into Zone A. Any balls already on
  the board in Zone A or in flight through Zone B continue to play out normally.
- If the buffer is 0 and Zone B is completely empty (no balls in flight, no balls waiting to
  drain), the run ends — game over (the stalemate condition, see Zone A).
- A last-chance window exists: with buffer at 0, if a ball still in Zone B triggers a score
  refill before Zone B fully empties, the buffer is restored and the run continues.
- A refill does not land all at once: slots fill in one at a time, each with its own pop
  and sound, so the buffer visibly climbs back to capacity rather than jumping. Dropping
  unlocks again the moment the first slot lands, without waiting for the rest.
- At a window-shift milestone, any buffer slots holding a now-blacklisted tier are re-rolled
  to the new window, same as the live queue.

**Visual:**

The buffer is shown in the HUD's top-right queue row alongside the next-ball preview, as a
count of remaining slots (rather than one icon per ball). It sits in the Zone A HUD area.

---

### Score Bar

The score bar tracks cumulative Zone B output toward the next buffer refill. It fills
horizontally at the bottom of Zone B.

**Mechanics:**

- Every ball that drains into a Zone B collector adds its scored value (`value × collector
  multiplier`) to the bar.
- Reaching the target no longer resets the bar immediately. Instead it triggers a short
  **cash-in sequence**: the bar pins full and holds while Zone B finishes draining any
  balls still in flight (so nothing scored after the target is crossed is lost), then
  dwells briefly at full before draining back down to zero. Only once the drain-out
  finishes does the level advance and the ball buffer begin its refill.
- The buffer refill itself is no longer instant — it fills one slot at a time rather than
  jumping straight to capacity (see Ball Buffer below).
- The bar's target escalates over time, per the authored stages in `progression.json` (see
  Score Bar Target above) — it is not fixed for the whole run.
- The bar and the ball buffer are independent systems; they communicate through a single
  event (bar full → cash-in → refill buffer). Neither system has direct knowledge of the
  other's internal state.

**Overall score:**

In addition to the bar, a running total of all points scored in the session is shown in the
HUD. This cumulative number never resets and represents the player's final score at game
over. The score bar and the overall score both draw from the same Zone B drain events but
serve different purposes: the bar drives the refill loop, the total measures overall
performance.

**Visual:**

The score bar spans the bottom edge of Zone B, filling left to right as balls drain. On
reaching target it holds full for a beat, then visibly tweens back down to empty as the
cash-in resolves, reading as a deliberate payout rather than a snap reset.

---

**Feel:** The buffer and the bar create a visible cause-and-effect loop: drop balls into
Zone A → merge and send them to Zone B → Zone B fills the bar → bar refills the buffer →
repeat. Running low on buffer adds pressure without being an immediate death sentence; the
player still has Zone B to bail them out. Last-ball moments where a single ball in Zone B
just barely tops the bar and saves the run are intentionally dramatic.
