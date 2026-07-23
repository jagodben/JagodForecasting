import { useId, useState } from 'react';

export interface TrendPoint {
  date: string;
  demValue: number; // Democratic probability in 0..1 (win prob, or chamber-control prob)
}

interface Props {
  data: TrendPoint[];
  demLabel: string;
  repLabel: string;
  width?: number;
  height?: number;
  // Color of the "dem"/challenger series. Defaults to blue; pass gold for a viable independent.
  demColor?: string;
  // Multiplier for the hover value pills. Pills are sized in viewBox units, so a chart whose
  // viewBox is much wider than its on-screen width should pass (viewBoxWidth / 320) to render
  // pills at the same physical size as the default 320-wide chart.
  pillScale?: number;
}

const DEM = '#123f8f';
const REP = '#9c150b';
const INK = '#1f2937';
const INK_MUTED = '#9aa0a6';

// Measures pill text at the site font so name/value spacing is exact (character-count
// estimates left uneven gaps — long names overestimate, short ones underestimate).
let measureCtx: CanvasRenderingContext2D | null = null;
const textWidth = (text: string, weight: number, fontPx: number): number => {
  measureCtx ??= document.createElement('canvas').getContext('2d');
  if (!measureCtx) return text.length * 6.2;
  measureCtx.font = `${weight} ${fontPx}px 'Libre Franklin', -apple-system, BlinkMacSystemFont, sans-serif`;
  return measureCtx.measureText(text).width;
};

// Step-after path: each day's value holds flat until the next day's jump — the market-style
// "rigid" look (daily snapshots are genuinely discrete, so steps are also the honest shape).
const stepPath = (pts: { x: number; y: number }[]): string => {
  let d = `M ${pts[0].x.toFixed(1)} ${pts[0].y.toFixed(1)}`;
  for (let i = 1; i < pts.length; i++) {
    d += ` L ${pts[i].x.toFixed(1)} ${pts[i - 1].y.toFixed(1)} L ${pts[i].x.toFixed(1)} ${pts[i].y.toFixed(1)}`;
  }
  return d;
};

/**
 * Two-line probability-over-time chart (Dem vs Rep, which sum to 100%). Shared by the home-page
 * chamber "Race Timeline" and each race page's timeline so the two always look identical.
 * Market-style presentation: tall plot, dotted gridlines labeled on the right, stepped lines,
 * and white value pills (colored tick + name + value) that sit at the line ends and track the
 * crosshair on hover, with the hovered date shown at the top.
 */
export const ProbabilityTrendChart = ({ data, demLabel, repLabel, width = 320, height = 210, demColor = DEM, pillScale = 1 }: Props) => {
  const uid = useId().replace(/:/g, '');
  const [hover, setHover] = useState<number | null>(null);
  if (data.length < 2) return null;

  const pad = { top: 24, right: 44, bottom: 22, left: 8 };
  const cw = width - pad.left - pad.right;
  const chh = height - pad.top - pad.bottom;

  const dem = data.map(d => d.demValue);
  const rep = data.map(d => 1 - d.demValue);

  // Domain fits both series with headroom, then snaps to a round tick step so the
  // axis reads 40/50/60 rather than arbitrary values.
  const all = [...dem, ...rep];
  const rawLo = Math.min(...all), rawHi = Math.max(...all);
  const rawSpan = Math.max(rawHi - rawLo, 0.06) * 1.3;
  const step = [0.02, 0.05, 0.1, 0.2, 0.25].find(s => rawSpan / s <= 4) ?? 0.25;
  const lo = Math.max(0, Math.floor((rawLo - rawSpan * 0.12) / step) * step);
  const hi = Math.min(1, Math.ceil((rawHi + rawSpan * 0.12) / step) * step);
  // Ticks grow outward from the 50% majority line (falling back to the domain floor when
  // 50% is out of view), so that line always gets a gridline and the count stays sparse.
  const anchor = lo <= 0.5 && 0.5 <= hi ? 0.5 : lo;
  const tickStep = (hi - lo) / step > 4 ? step * 2 : step;
  const tickSet = new Set<number>();
  for (let t = anchor; t >= lo - 1e-9; t -= tickStep) tickSet.add(Math.round(t * 1000) / 1000);
  for (let t = anchor; t <= hi + 1e-9; t += tickStep) tickSet.add(Math.round(t * 1000) / 1000);
  const ticks = [...tickSet].sort((a, b) => a - b);

  const x = (i: number) => pad.left + (i / (data.length - 1)) * cw;
  const y = (v: number) => pad.top + chh - ((v - lo) / (hi - lo || 1)) * chh;
  const floorY = pad.top + chh;

  const demPts = dem.map((v, i) => ({ x: x(i), y: y(v) }));
  const repPts = rep.map((v, i) => ({ x: x(i), y: y(v) }));
  const demLine = stepPath(demPts);
  const repLine = stepPath(repPts);

  const stepW = cw / (data.length - 1);
  // Parse the YYYY-MM-DD portion as a LOCAL date so a UTC-midnight timestamp doesn't render as
  // the previous day (which made a July 1 point read "Jun 30").
  const fmtDate = (iso: string) => {
    const [yy, mm, dd] = iso.slice(0, 10).split('-').map(Number);
    return new Date(yy, mm - 1, dd).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  };

  const nTicks = Math.min(4, data.length);
  const dateTicks = Array.from({ length: nTicks }, (_, k) => Math.round((k / (nTicks - 1 || 1)) * (data.length - 1)));

  // Pills use the surname ("Jon Husted" -> "Husted") to stay compact. When surnames collide
  // (e.g. two "Nominee" placeholders), fall back to truncating the full labels.
  // A generational suffix isn't a surname on its own: "Nick Begich III" -> "Begich III".
  const lastWord = (s: string) => {
    const parts = s.trim().split(/\s+/);
    const last = parts[parts.length - 1] ?? s;
    if (parts.length >= 3 && /^(Jr\.?|Sr\.?|II|III|IV|V)$/i.test(last))
      return `${parts[parts.length - 2]} ${last}`;
    return last;
  };
  let demShort = lastWord(demLabel), repShort = lastWord(repLabel);
  if (demShort === repShort) {
    const trunc = (s: string) => (s.length > 9 ? `${s.slice(0, 8)}…` : s);
    demShort = trunc(demLabel); repShort = trunc(repLabel);
  }

  // The values/positions the pills reflect while hovering.
  const idx = hover ?? data.length - 1;
  const anchorX = x(idx);

  // Value pills, market-style: white rounded chip with a colored tick, name, and value.
  // Nudged apart when the lines converge; placed on whichever side of the anchor has room.
  const PILL_FONT = 11 * pillScale;
  const PILL_H = 20 * pillScale;
  const PILL_GAP = 6 * pillScale; // exact space between the name and the value
  const padL = 9 * pillScale, padR = 8 * pillScale;
  const pillW = (name: string, value: number) =>
    padL + textWidth(name, 600, PILL_FONT) + PILL_GAP + textWidth(`${(value * 100).toFixed(1)}%`, 700, PILL_FONT) + padR;
  let demPillY = y(dem[idx]), repPillY = y(rep[idx]);
  const minGap = PILL_H + 2;
  if (Math.abs(demPillY - repPillY) < minGap) {
    const mid = (demPillY + repPillY) / 2;
    const dir = demPillY <= repPillY ? -1 : 1;
    demPillY = mid + dir * (minGap / 2);
    repPillY = mid - dir * (minGap / 2);
  }
  const clampY = (v: number) => Math.min(Math.max(v, pad.top + PILL_H / 2), floorY - PILL_H / 2);
  demPillY = clampY(demPillY); repPillY = clampY(repPillY);

  const renderPill = (pillY: number, color: string, name: string, value: number) => {
    const w = pillW(name, value);
    const offset = 10 * pillScale;
    const px = anchorX + offset + w > width - 2 ? anchorX - w - offset : anchorX + offset;
    return (
      <g transform={`translate(${px.toFixed(1)}, ${(pillY - PILL_H / 2).toFixed(1)})`} pointerEvents="none">
        <rect width={w} height={PILL_H} rx={6 * pillScale} fill="#ffffff" stroke="#e5e8eb" strokeWidth="1" filter={`url(#shadow-${uid})`} />
        <text x={padL} y={PILL_H / 2 + 1} alignmentBaseline="middle" fontSize={PILL_FONT} fontWeight="600" fill={color}>{name}</text>
        <text x={w - padR} y={PILL_H / 2 + 1} textAnchor="end" alignmentBaseline="middle" fontSize={PILL_FONT} fontWeight="700" fill={INK}>
          {(value * 100).toFixed(1)}%
        </text>
      </g>
    );
  };

  return (
    <svg width="100%" viewBox={`0 0 ${width} ${height}`} style={{ display: 'block', overflow: 'visible' }}
         role="img"
         aria-label={`Win probability over time. Latest: ${demLabel} ${(dem[dem.length - 1] * 100).toFixed(0)}%, ${repLabel} ${((1 - dem[dem.length - 1]) * 100).toFixed(0)}%.`}
         onMouseLeave={() => setHover(null)}>
      <defs>
        <filter id={`shadow-${uid}`} x="-20%" y="-40%" width="140%" height="180%">
          <feDropShadow dx="0" dy="1" stdDeviation="2" floodColor="#0b1220" floodOpacity="0.16" />
        </filter>
        {/* 2px of slack so line strokes centered on the plot edge aren't shaved off */}
        <clipPath id={`plot-${uid}`}>
          <rect x={pad.left - 2} y={pad.top - 2} width={cw + 4} height={chh + 4} />
        </clipPath>
      </defs>

      {/* Dotted gridlines, labeled on the right; the 50% majority line is slightly emphasized */}
      {ticks.map((t, i) => {
        const isHalf = Math.abs(t - 0.5) < 1e-9;
        return (
          <g key={i}>
            <line x1={pad.left} y1={y(t)} x2={width - pad.right} y2={y(t)}
                  stroke={isHalf ? '#c2c8cf' : '#e3e6ea'} strokeWidth="1"
                  strokeDasharray="1.5,3.5" strokeLinecap="round" />
            <text x={width - pad.right + 8} y={y(t)} alignmentBaseline="middle"
                  fontSize="10.5" fontWeight={isHalf ? 700 : 400} fill={isHalf ? '#6b7280' : INK_MUTED}>
              {(t * 100).toFixed(0)}%
            </text>
          </g>
        );
      })}

      <g clipPath={`url(#plot-${uid})`}>
        <path d={repLine} fill="none" stroke={REP} strokeWidth="2.25" strokeLinejoin="round" strokeLinecap="round" />
        <path d={demLine} fill="none" stroke={demColor} strokeWidth="2.25" strokeLinejoin="round" strokeLinecap="round" />
      </g>

      {/* Date axis */}
      {dateTicks.map((di, k) => (
        <text key={k} x={x(di)} y={height - 4} fontSize="10.5" fill={INK_MUTED}
              textAnchor={k === 0 ? 'start' : k === dateTicks.length - 1 ? 'end' : 'middle'}>
          {fmtDate(data[di].date)}
        </text>
      ))}

      {/* Crosshair (hover): vertical line + date readout at the top */}
      {hover != null && (
        <g pointerEvents="none">
          <line x1={anchorX} y1={pad.top - 4} x2={anchorX} y2={floorY} stroke="#d3d8dd" strokeWidth="1" />
          <text x={Math.min(Math.max(anchorX, pad.left + 26), width - pad.right - 26)} y={pad.top - 10}
                textAnchor="middle" fontSize="10.5" fontWeight="600" fill={INK_MUTED}>
            {fmtDate(data[hover].date)}
          </text>
        </g>
      )}

      {/* Markers + value pills follow the crosshair; nothing shows at rest */}
      {hover != null && (
        <>
          <g pointerEvents="none">
            <circle cx={anchorX} cy={y(rep[idx])} r="4" fill={REP} stroke="#fff" strokeWidth="1.75" />
            <circle cx={anchorX} cy={y(dem[idx])} r="4" fill={demColor} stroke="#fff" strokeWidth="1.75" />
          </g>
          {renderPill(repPillY, REP, repShort, rep[idx])}
          {renderPill(demPillY, demColor, demShort, dem[idx])}
        </>
      )}

      {/* Invisible hover targets */}
      {data.map((_, i) => (
        <rect key={i} x={x(i) - stepW / 2} y={pad.top} width={Math.max(1, stepW)} height={chh}
              fill="transparent" onMouseEnter={() => setHover(i)} />
      ))}
    </svg>
  );
};
