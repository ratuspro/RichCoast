import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from './contracts';
import {
  MATERIAL_COUNT,
  MATERIALS,
  colorForTier,
  compactValue,
  hexColor,
  materialForTier,
} from './Materials';

describe('the material ladder', () => {
  it('has exactly MATERIAL_COUNT (20) entries, one per authored tier', () => {
    expect(MATERIALS).toHaveLength(MATERIAL_COUNT);
    expect(MATERIAL_COUNT).toBe(20);
    expect(MATERIAL_COUNT).toBeGreaterThanOrEqual(TIER_COUNT);
  });

  it('groups into 5 families of 4, aligned with the 4-tier draw windows', () => {
    const families = MATERIALS.map((m) => m.family);
    for (let start = 0; start < MATERIAL_COUNT; start += 4) {
      const window = families.slice(start, start + 4);
      expect(new Set(window).size, `window starting at tier ${start + 1}`).toBe(1);
    }
    expect(new Set(families).size).toBe(5);
  });

  it('gives every material a unique name and base colour', () => {
    expect(new Set(MATERIALS.map((m) => m.name)).size).toBe(MATERIAL_COUNT);
    expect(new Set(MATERIALS.map((m) => m.baseColor)).size).toBe(MATERIAL_COUNT);
  });

  it('keeps physics multipliers inside the subtle-feel band', () => {
    for (const m of MATERIALS) {
      expect(m.physics.restitutionMult, m.name).toBeGreaterThanOrEqual(0.7);
      expect(m.physics.restitutionMult, m.name).toBeLessThanOrEqual(1.5);
      expect(m.physics.frictionMult, m.name).toBeGreaterThanOrEqual(0.4);
      expect(m.physics.frictionMult, m.name).toBeLessThanOrEqual(1.2);
      expect(m.physics.densityMult, m.name).toBeGreaterThanOrEqual(0.8);
      expect(m.physics.densityMult, m.name).toBeLessThanOrEqual(1.3);
    }
  });
});

describe('materialForTier', () => {
  it('maps authored tiers straight onto the ladder', () => {
    expect(materialForTier(1).def.name).toBe('Wood');
    expect(materialForTier(20).def.name).toBe('Antimatter');
    expect(materialForTier(1).cycle).toBe(0);
    expect(materialForTier(20).cycle).toBe(0);
  });

  it('wraps past the ladder end and reports the completed cycle count', () => {
    expect(materialForTier(21).def.name).toBe('Wood');
    expect(materialForTier(21).cycle).toBe(1);
    expect(materialForTier(40).cycle).toBe(1);
    expect(materialForTier(41).cycle).toBe(2);
  });

  it('clamps nonsense tiers below 1 to the first material', () => {
    expect(materialForTier(0).def.name).toBe('Wood');
    expect(materialForTier(0).cycle).toBe(0);
  });
});

describe('colorForTier (compatibility shim)', () => {
  it('returns the material base colour, wrapping like materialForTier', () => {
    for (let t = 1; t <= MATERIAL_COUNT * 2; t++) {
      expect(colorForTier(t)).toBe(materialForTier(t).def.baseColor);
    }
  });
});

describe('hexColor', () => {
  it('formats an 0xRRGGBB number as a CSS hex string, zero-padded', () => {
    expect(hexColor(0xa9713f)).toBe('#a9713f');
    expect(hexColor(0x00000f)).toBe('#00000f');
  });
});

describe('compactValue', () => {
  it('leaves sub-thousand values untouched', () => {
    expect(compactValue(1)).toBe('1');
    expect(compactValue(729)).toBe('729');
  });

  it('compacts thousands and millions', () => {
    expect(compactValue(19683)).toBe('20K'); // 3^9
    expect(compactValue(531441)).toBe('531K'); // 3^12
    expect(compactValue(1594323)).toBe('1.6M'); // 3^13
  });

  it('stays within a 4-character face budget for every authored tier value', () => {
    for (let tier = 1; tier <= 24; tier++) {
      expect(compactValue(3 ** (tier - 1)).length, `tier ${tier}`).toBeLessThanOrEqual(4);
    }
  });

  it('never exceeds 5 characters, even deep into wrapped-cycle tiers', () => {
    for (let tier = 25; tier <= 100; tier++) {
      expect(compactValue(3 ** (tier - 1)).length, `tier ${tier}`).toBeLessThanOrEqual(5);
    }
  });
});
