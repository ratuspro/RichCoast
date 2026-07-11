import { describe, expect, it } from 'vitest';
import {
  bufferForLevel,
  getStage,
  MILESTONE_EVERY,
  milestoneProgress,
  paletteNameForLevel,
  scoreBarTargetForLevel,
  TAIL_TARGET_GROWTH,
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
    expect(paletteNameForLevel(24)).toBe('workshop');
  });

  it('swaps at each authored milestone stage', () => {
    expect(paletteNameForLevel(25)).toBe('dusk');
    expect(paletteNameForLevel(49)).toBe('dusk');
    expect(paletteNameForLevel(50)).toBe('night');
    expect(paletteNameForLevel(75)).toBe('dawn');
    expect(paletteNameForLevel(100)).toBe('gilded');
  });

  it('holds the last authored palette forever (author-then-hold)', () => {
    expect(paletteNameForLevel(101)).toBe('gilded');
    expect(paletteNameForLevel(500)).toBe('gilded');
  });
});

describe('scoreBarTargetForLevel', () => {
  it('returns the authored target at and below the last authored stage', () => {
    expect(scoreBarTargetForLevel(1)).toBe(getStage(1).scoreBarTarget);
    expect(scoreBarTargetForLevel(60)).toBe(320_000);
    expect(scoreBarTargetForLevel(97)).toBe(21_000_000); // holds the level-90 stage
    expect(scoreBarTargetForLevel(100)).toBe(670_000_000);
  });

  it('keeps growing geometrically past the last authored stage (the endless tail)', () => {
    const last = scoreBarTargetForLevel(100);
    expect(scoreBarTargetForLevel(101)).toBeCloseTo(last * TAIL_TARGET_GROWTH, -2);
    expect(scoreBarTargetForLevel(101)).toBeGreaterThan(last);
    // Strictly increasing far into the tail — the flat-forever bug.
    expect(scoreBarTargetForLevel(500)).toBeGreaterThan(scoreBarTargetForLevel(499));
  });

  it('tail rate continues the authored curve: one draw-window of value per milestone span', () => {
    // Ball values grow 3^4 per window shift every MILESTONE_EVERY levels, and the authored
    // targets track that (105K@50 -> 670M@100 ~ x3^8 over 50 levels). The tail keeps the
    // same per-level rate so one good drain stays worth ~one level forever.
    expect(TAIL_TARGET_GROWTH ** MILESTONE_EVERY).toBeCloseTo(3 ** 4, 8);
    expect(
      scoreBarTargetForLevel(100 + MILESTONE_EVERY) / scoreBarTargetForLevel(100),
    ).toBeCloseTo(3 ** 4, 2);
  });
});

describe('bufferForLevel', () => {
  it('starts at 5 and holds through the first fill', () => {
    expect(bufferForLevel(1)).toBe(5);
    expect(bufferForLevel(2)).toBe(5);
  });

  it('adds 2 per fill for the next three fills', () => {
    expect(bufferForLevel(3)).toBe(7);
    expect(bufferForLevel(4)).toBe(9);
    expect(bufferForLevel(5)).toBe(11);
  });

  it('adds 1 per fill thereafter', () => {
    expect(bufferForLevel(6)).toBe(12);
    expect(bufferForLevel(7)).toBe(13);
    expect(bufferForLevel(20)).toBe(26);
  });
});
