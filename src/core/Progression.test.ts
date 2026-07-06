import { describe, expect, it } from 'vitest';
import { MILESTONE_EVERY, milestoneProgress } from './Progression';

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
