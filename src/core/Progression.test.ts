import { describe, expect, it } from 'vitest';
import {
  bufferForLevel,
  MILESTONE_EVERY,
  milestoneProgress,
  paletteNameForLevel,
  scoreBarTargetForLevel,
  TAIL_TARGET_GROWTH,
  windowForLevel,
} from './Progression';

describe('milestoneProgress', () => {
  it('starts near empty on level 1', () => {
    expect(milestoneProgress(1)).toBeCloseTo(1 / MILESTONE_EVERY);
  });

  it('is nearly full one level before a milestone', () => {
    expect(milestoneProgress(MILESTONE_EVERY - 1)).toBeCloseTo(
      (MILESTONE_EVERY - 1) / MILESTONE_EVERY,
    );
  });

  it('resets to empty the level a milestone lands', () => {
    expect(milestoneProgress(MILESTONE_EVERY)).toBe(0);
    expect(milestoneProgress(MILESTONE_EVERY * 3)).toBe(0);
  });

  it('tracks progress within later milestone windows', () => {
    expect(milestoneProgress(MILESTONE_EVERY * 2 + 10)).toBeCloseTo(
      10 / MILESTONE_EVERY,
    );
  });
});

describe('paletteNameForLevel', () => {
  it('starts on the workshop palette', () => {
    expect(paletteNameForLevel(1)).toBe('workshop');
    expect(paletteNameForLevel(19)).toBe('workshop');
  });

  it('swaps at each authored milestone stage', () => {
    expect(paletteNameForLevel(20)).toBe('dusk');
    expect(paletteNameForLevel(39)).toBe('dusk');
    expect(paletteNameForLevel(40)).toBe('night');
    expect(paletteNameForLevel(60)).toBe('dawn');
    expect(paletteNameForLevel(80)).toBe('gilded');
  });

  it('holds the last authored palette forever (author-then-hold)', () => {
    expect(paletteNameForLevel(81)).toBe('gilded');
    expect(paletteNameForLevel(500)).toBe('gilded');
  });
});

describe('scoreBarTargetForLevel', () => {
  it('returns the authored target exactly at each anchor', () => {
    expect(scoreBarTargetForLevel(1)).toBe(20);
    expect(scoreBarTargetForLevel(4)).toBe(200);
    expect(scoreBarTargetForLevel(20)).toBe(5_000);
    expect(scoreBarTargetForLevel(40)).toBe(400_000);
    expect(scoreBarTargetForLevel(80)).toBe(2_700_000_000);
  });

  it('interpolates geometrically between anchors — no plateaus, strictly increasing', () => {
    // Anchors are NOT plateaus: every level's bar is bigger than the last, so a
    // multi-level roll-through burst self-limits instead of wrapping a flat target.
    for (let level = 2; level <= 80; level++) {
      expect(
        scoreBarTargetForLevel(level),
        `target must grow at level ${level}`,
      ).toBeGreaterThan(scoreBarTargetForLevel(level - 1));
    }
    // Spot-check the constant per-level ratio inside the L4→L20 span.
    const ratio = (5_000 / 200) ** (1 / 16);
    expect(scoreBarTargetForLevel(5)).toBe(Math.round(200 * ratio));
  });

  it('keeps growing geometrically past the last authored stage (the endless tail)', () => {
    const last = scoreBarTargetForLevel(80);
    expect(scoreBarTargetForLevel(81)).toBeCloseTo(last * TAIL_TARGET_GROWTH, -2);
    expect(scoreBarTargetForLevel(81)).toBeGreaterThan(last);
    // Strictly increasing far into the tail — the flat-forever bug.
    expect(scoreBarTargetForLevel(500)).toBeGreaterThan(scoreBarTargetForLevel(499));
  });

  it('tail rate continues the authored curve: one draw-window of value per milestone span', () => {
    // Ball values grow 3^4 per window shift every MILESTONE_EVERY levels, and the authored
    // anchors track that (5K@20 -> 2.7B@80 ~ x3^12 over 60 levels). The tail keeps the
    // same per-level rate so one good drain stays worth ~one level far into the tail.
    expect(TAIL_TARGET_GROWTH ** MILESTONE_EVERY).toBeCloseTo(3 ** 4, 8);
    expect(
      scoreBarTargetForLevel(80 + MILESTONE_EVERY) / scoreBarTargetForLevel(80),
    ).toBeCloseTo(3 ** 4, 2);
  });
});

describe('bufferForLevel', () => {
  it('starts lean (tutorial) then oscillates harvest/pressure by parity', () => {
    expect(bufferForLevel(1)).toBe(8);
    expect(bufferForLevel(2)).toBe(17); // harvest
    expect(bufferForLevel(3)).toBe(13); // pressure
    expect(bufferForLevel(4)).toBe(17);
    expect(bufferForLevel(5)).toBe(13);
    expect(bufferForLevel(10)).toBe(18);
    expect(bufferForLevel(11)).toBe(14);
  });

  it('always pays the harvest amount on milestone levels (breather with the new window)', () => {
    for (const level of [20, 40, 80]) {
      // The level before a milestone is a lean pressure refill; the milestone pays full.
      expect(bufferForLevel(level), `milestone level ${level}`).toBeGreaterThan(
        bufferForLevel(level - 1),
      );
    }
    expect(bufferForLevel(20)).toBe(19);
  });

  it('caps forever at 20/16 — drop count never becomes a chore', () => {
    expect(bufferForLevel(30)).toBe(20);
    expect(bufferForLevel(31)).toBe(16);
    for (let level = 2; level <= 500; level++) {
      expect(bufferForLevel(level)).toBeLessThanOrEqual(20);
      expect(bufferForLevel(level)).toBeGreaterThanOrEqual(8);
    }
  });
});

describe('windowForLevel', () => {
  it('serves the authored window through the last stage', () => {
    expect(windowForLevel(1)).toEqual([1, 1]);
    expect(windowForLevel(4)).toEqual([1, 4]);
    expect(windowForLevel(20)).toEqual([5, 8]);
    expect(windowForLevel(80)).toEqual([17, 20]);
    expect(windowForLevel(99)).toEqual([17, 20]);
  });

  it('steps +TAIL_WINDOW_STEP per milestone in the tail, holding between them', () => {
    expect(windowForLevel(100)).toEqual([19, 22]);
    expect(windowForLevel(119)).toEqual([19, 22]);
    expect(windowForLevel(120)).toEqual([21, 24]);
    expect(windowForLevel(200)).toEqual([17 + 2 * 6, 20 + 2 * 6]);
  });
});

describe('target reachability (anti-soft-lock guard)', () => {
  it('every level 1–100 is crossable with that level alone: refill, perfectly merged, worst gates', () => {
    // Conservative model of one A-phase → B-phase cycle with NO carried-over board balls:
    // N = bufferForLevel(level) balls of the window's MAX tier; perfect pair-merging
    // multiplies total board value ×1.5 per full pairing round (floor(log2 N) rounds,
    // since a merge turns 2V into 3V); every ball then drains through a deliberately
    // low ×4 gate cascade (the player aims the trap-door column, so ×4 is pessimistic —
    // typical cascades run ×8–14). If this ever dips below the target, the curve has
    // authored a level that can soft-lock a run into the stalemate game-over. (Deeper in
    // the TAIL the target eventually outruns the +2-per-milestone supply by design —
    // that wall IS the endgame — so the guard stops at the first tail window.)
    for (let level = 1; level <= 100; level++) {
      const n = bufferForLevel(level);
      const maxTierValue = 3 ** (windowForLevel(level)[1] - 1);
      const mergedBoardValue = n * maxTierValue * 1.5 ** Math.floor(Math.log2(n));
      const drained = mergedBoardValue * 4;
      expect(drained, `level ${level} unreachable`).toBeGreaterThanOrEqual(
        scoreBarTargetForLevel(level),
      );
    }
  });
});
