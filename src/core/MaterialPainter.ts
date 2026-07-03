/**
 * Procedural canvas recipes for the material ball faces — the one place both zones'
 * texture generation draws from, so a ball transferred A→B keeps its exact look.
 *
 * `paintBall` renders one material ball into a 2·radius square canvas whose centre is
 * (radius, radius). Two levels of detail:
 *  - `'full'`  (Zone A): shaded base + the material's detail pass (grain/sheen/facets/…)
 *    — the caller draws the value number on top afterwards.
 *  - `'small'` (Zone B, 10px): shaded base + highlight only; at that size detail reads
 *    as noise, colour is the identity.
 * Both LODs draw the per-cycle gold ring for tiers wrapped past the ladder end, so a
 * tier-21 ball never mimics tier 1.
 *
 * Everything is deterministic (seeded per tier, no Math.random), so regenerated
 * textures are pixel-stable across scenes and zones.
 */
import { hexColor, materialForTier, type MaterialDef } from './Materials';

export type BallLod = 'full' | 'small';

// --- tiny colour + rng helpers ---------------------------------------------

/** Mix `c` toward `target` by t∈[0,1], per channel. */
function mix(c: number, target: number, t: number): number {
  const ch = (shift: number) => {
    const a = (c >> shift) & 0xff;
    const b = (target >> shift) & 0xff;
    return Math.round(a + (b - a) * t) << shift;
  };
  return ch(16) | ch(8) | ch(0);
}

const lighten = (c: number, t: number) => mix(c, 0xffffff, t);
const darken = (c: number, t: number) => mix(c, 0x000000, t);

/** `#rrggbb` with alpha, for Canvas 2D fill/stroke styles. */
function rgba(c: number, alpha: number): string {
  return `rgba(${(c >> 16) & 0xff}, ${(c >> 8) & 0xff}, ${c & 0xff}, ${alpha})`;
}

/** Mulberry32 — deterministic per-tier stream for speckle/star placement. */
function seededRandom(seed: number): () => number {
  let s = seed | 0;
  return () => {
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// --- the painter ------------------------------------------------------------

export function paintBall(
  ctx: CanvasRenderingContext2D,
  radius: number,
  tier: number,
  lod: BallLod,
): void {
  const { def, cycle } = materialForTier(tier);
  const lineW = Math.max(2, radius * 0.08);
  const r = radius - lineW / 2; // painted disc radius, rim stroke centred on its edge

  // Base: soft top-left-lit sphere. The toy look comes from this gradient — detail
  // passes only decorate it.
  const grad = ctx.createRadialGradient(
    radius - r * 0.35,
    radius - r * 0.4,
    r * 0.15,
    radius,
    radius,
    r,
  );
  grad.addColorStop(0, hexColor(lighten(def.baseColor, 0.28)));
  grad.addColorStop(0.7, hexColor(def.baseColor));
  grad.addColorStop(1, hexColor(darken(def.baseColor, 0.22)));
  ctx.beginPath();
  ctx.arc(radius, radius, r, 0, Math.PI * 2);
  ctx.fillStyle = grad;
  ctx.fill();

  if (lod === 'full') {
    ctx.save();
    ctx.beginPath();
    ctx.arc(radius, radius, r, 0, Math.PI * 2);
    ctx.clip();
    paintDetail(ctx, radius, r, tier, def);
    ctx.restore();
  }

  // Glossy highlight: a small bright blob up-left. Kept subtle on 'full' (details
  // already carry the material), stronger on 'small' where it's the only shaping.
  const hlAlpha = lod === 'small' ? 0.5 : 0.3;
  const hl = ctx.createRadialGradient(
    radius - r * 0.4,
    radius - r * 0.45,
    0,
    radius - r * 0.4,
    radius - r * 0.45,
    r * 0.45,
  );
  hl.addColorStop(0, `rgba(255, 255, 255, ${hlAlpha})`);
  hl.addColorStop(1, 'rgba(255, 255, 255, 0)');
  ctx.beginPath();
  ctx.arc(radius, radius, r, 0, Math.PI * 2);
  ctx.fillStyle = hl;
  ctx.fill();

  // Rim: darkened base colour instead of the old universal navy — reads as the
  // material's own edge and survives the light Bright Workshop background.
  ctx.beginPath();
  ctx.arc(radius, radius, r, 0, Math.PI * 2);
  ctx.lineWidth = lineW;
  ctx.strokeStyle = rgba(darken(def.baseColor, 0.5), 0.75);
  ctx.stroke();

  // One gold ring per completed ladder cycle (tier 21+ = wood-with-a-ring, not wood).
  for (let i = 1; i <= cycle; i++) {
    ctx.beginPath();
    ctx.arc(radius, radius, r - lineW * (0.5 + i * 1.6), 0, Math.PI * 2);
    ctx.lineWidth = Math.max(1.5, lineW * 0.6);
    ctx.strokeStyle = rgba(0xf2b024, 0.9);
    ctx.stroke();
  }
}

// --- detail passes (full LOD only, clipped to the disc) ----------------------

function paintDetail(
  ctx: CanvasRenderingContext2D,
  c: number, // centre coordinate (= nominal radius)
  r: number, // painted disc radius
  tier: number,
  def: MaterialDef,
): void {
  const accent = def.accentColor;
  const rand = seededRandom(tier * 7919);

  switch (def.detail) {
    case 'grain': {
      // Concentric-ish arcs offset to one side, like end-grain rings.
      ctx.lineWidth = Math.max(1.5, r * 0.09);
      ctx.strokeStyle = rgba(accent, 0.8);
      for (let i = 0; i < 3; i++) {
        ctx.beginPath();
        ctx.arc(c - r * 0.25, c + r * 0.1, r * (0.3 + i * 0.28), -0.6, Math.PI * 0.75);
        ctx.stroke();
      }
      break;
    }
    case 'speckle': {
      ctx.fillStyle = rgba(accent, 0.85);
      const n = Math.max(10, Math.round(r * 0.6));
      for (let i = 0; i < n; i++) {
        const a = rand() * Math.PI * 2;
        const d = Math.sqrt(rand()) * r * 0.85;
        const dotR = r * (0.05 + rand() * 0.07);
        ctx.beginPath();
        ctx.arc(c + Math.cos(a) * d, c + Math.sin(a) * d, dotR, 0, Math.PI * 2);
        ctx.fill();
      }
      break;
    }
    case 'gloss': {
      // A second, bigger soft light — resin depth.
      const g = ctx.createRadialGradient(c + r * 0.25, c + r * 0.3, 0, c + r * 0.25, c + r * 0.3, r * 0.8);
      g.addColorStop(0, rgba(accent, 0.45));
      g.addColorStop(1, rgba(accent, 0));
      ctx.fillStyle = g;
      ctx.fillRect(c - r, c - r, r * 2, r * 2);
      break;
    }
    case 'matte': {
      // Faint horizontal throw-lines, like a potter's wheel.
      ctx.lineWidth = Math.max(1.5, r * 0.08);
      ctx.strokeStyle = rgba(accent, 0.7);
      for (const dy of [-0.35, 0.05, 0.45]) {
        ctx.beginPath();
        ctx.moveTo(c - r, c + r * dy);
        ctx.quadraticCurveTo(c, c + r * (dy + 0.08), c + r, c + r * dy);
        ctx.stroke();
      }
      break;
    }
    case 'sheen': {
      // Diagonal brushed band catching the light.
      ctx.save();
      ctx.translate(c, c);
      ctx.rotate(-Math.PI / 4);
      const band = ctx.createLinearGradient(0, -r * 0.55, 0, r * 0.1);
      band.addColorStop(0, rgba(accent, 0));
      band.addColorStop(0.5, rgba(accent, 0.55));
      band.addColorStop(1, rgba(accent, 0));
      ctx.fillStyle = band;
      ctx.fillRect(-r, -r * 0.55, r * 2, r * 0.65);
      ctx.restore();
      break;
    }
    case 'rivets': {
      ctx.fillStyle = rgba(accent, 0.85);
      const n = 7;
      for (let i = 0; i < n; i++) {
        const a = (i / n) * Math.PI * 2 - Math.PI / 2;
        ctx.beginPath();
        ctx.arc(c + Math.cos(a) * r * 0.78, c + Math.sin(a) * r * 0.78, r * 0.06, 0, Math.PI * 2);
        ctx.fill();
      }
      break;
    }
    case 'glint': {
      // A thin bright arc near the upper-right edge — light caught on volcanic glass.
      ctx.lineWidth = Math.max(1.5, r * 0.06);
      ctx.strokeStyle = rgba(accent, 0.8);
      ctx.beginPath();
      ctx.arc(c, c, r * 0.8, -Math.PI * 0.45, -Math.PI * 0.1);
      ctx.stroke();
      break;
    }
    case 'crescent': {
      // Glassy white crescent hugging the lower-right inside edge.
      ctx.lineWidth = Math.max(2, r * 0.14);
      ctx.strokeStyle = rgba(accent, 0.6);
      ctx.beginPath();
      ctx.arc(c, c, r * 0.72, Math.PI * 0.05, Math.PI * 0.55);
      ctx.stroke();
      break;
    }
    case 'facets': {
      // Flat wedges from the centre — a stylised table cut, with visible seams.
      const n = 5;
      for (let i = 0; i < n; i++) {
        const a0 = (i / n) * Math.PI * 2 + 0.3;
        const a1 = a0 + (Math.PI * 2) / n;
        ctx.beginPath();
        ctx.moveTo(c, c);
        ctx.arc(c, c, r, a0, a1);
        ctx.closePath();
        ctx.fillStyle =
          i % 2 === 0 ? rgba(accent, 0.5) : rgba(darken(def.baseColor, 0.25), 0.4);
        ctx.fill();
        ctx.beginPath();
        ctx.moveTo(c, c);
        ctx.lineTo(c + Math.cos(a0) * r, c + Math.sin(a0) * r);
        ctx.lineWidth = Math.max(1, r * 0.04);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.stroke();
      }
      // Central table.
      ctx.beginPath();
      ctx.arc(c, c, r * 0.45, 0, Math.PI * 2);
      ctx.fillStyle = rgba(lighten(def.baseColor, 0.2), 0.9);
      ctx.fill();
      ctx.lineWidth = Math.max(1, r * 0.04);
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
      ctx.stroke();
      break;
    }
    case 'glow': {
      const g = ctx.createRadialGradient(c, c, 0, c, c, r);
      g.addColorStop(0, rgba(accent, 0.95));
      g.addColorStop(0.55, rgba(accent, 0.25));
      g.addColorStop(1, rgba(accent, 0));
      ctx.fillStyle = g;
      ctx.fillRect(c - r, c - r, r * 2, r * 2);
      break;
    }
    case 'crust': {
      // Dark cooled plates over the glowing fill: cracked bezier seams + blobs.
      ctx.strokeStyle = rgba(accent, 0.9);
      ctx.lineWidth = Math.max(1.5, r * 0.09);
      for (let i = 0; i < 4; i++) {
        const a = rand() * Math.PI * 2;
        const x0 = c + Math.cos(a) * r * 0.9;
        const y0 = c + Math.sin(a) * r * 0.9;
        ctx.beginPath();
        ctx.moveTo(x0, y0);
        ctx.quadraticCurveTo(
          c + (rand() - 0.5) * r,
          c + (rand() - 0.5) * r,
          c + (rand() - 0.5) * r * 0.6,
          c + (rand() - 0.5) * r * 0.6,
        );
        ctx.stroke();
      }
      break;
    }
    case 'specks': {
      ctx.fillStyle = rgba(accent, 0.95);
      const n = 9;
      for (let i = 0; i < n; i++) {
        const a = rand() * Math.PI * 2;
        const d = Math.sqrt(rand()) * r * 0.85;
        const starR = r * (0.02 + rand() * 0.045);
        ctx.beginPath();
        ctx.arc(c + Math.cos(a) * d, c + Math.sin(a) * d, starR, 0, Math.PI * 2);
        ctx.fill();
      }
      break;
    }
    case 'corona': {
      const g = ctx.createRadialGradient(c, c, 0, c, c, r);
      g.addColorStop(0, rgba(accent, 1));
      g.addColorStop(0.35, rgba(accent, 0.55));
      g.addColorStop(0.6, rgba(accent, 0));
      g.addColorStop(0.85, rgba(lighten(def.baseColor, 0.3), 0.35));
      g.addColorStop(1, rgba(lighten(def.baseColor, 0.3), 0));
      ctx.fillStyle = g;
      ctx.fillRect(c - r, c - r, r * 2, r * 2);
      break;
    }
  }
}
