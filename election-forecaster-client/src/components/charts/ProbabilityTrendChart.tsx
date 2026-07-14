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
}

const DEM = '#123f8f';
const REP = '#9c150b';
const INK = '#1f2937';
const INK_MUTED = '#9aa0a6';
const GRID = '#eef0f2';

// Builds a smooth SVG path through the points with monotone-cubic interpolation
// (no overshoot, so a probability series never bulges past its data).
const smoothPath = (pts: { x: number; y: number }[]): string => {
  const n = pts.length;
  if (n < 3) return pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');

  const dx = Array.from({ length: n - 1 }, (_, i) => pts[i + 1].x - pts[i].x);
  const slope = Array.from({ length: n - 1 }, (_, i) => (pts[i + 1].y - pts[i].y) / (dx[i] || 1));
  const m: number[] = [slope[0]];
  for (let i = 1; i < n - 1; i++) {
    m.push(slope[i - 1] * slope[i] <= 0 ? 0 : (slope[i - 1] + slope[i]) / 2);
  }
  m.push(slope[n - 2]);
  // Fritsch–Carlson limiter keeps the curve monotone between points.
  for (let i = 0; i < n - 1; i++) {
    if (slope[i] === 0) { m[i] = 0; m[i + 1] = 0; continue; }
    const a = m[i] / slope[i], b = m[i + 1] / slope[i];
    const s = a * a + b * b;
    if (s > 9) { const t = 3 / Math.sqrt(s); m[i] = t * a * slope[i]; m[i + 1] = t * b * slope[i]; }
  }
  let d = `M ${pts[0].x.toFixed(1)} ${pts[0].y.toFixed(1)}`;
  for (let i = 0; i < n - 1; i++) {
    const h = dx[i];
    d += ` C ${(pts[i].x + h / 3).toFixed(1)} ${(pts[i].y + (m[i] * h) / 3).toFixed(1)},`
      + ` ${(pts[i + 1].x - h / 3).toFixed(1)} ${(pts[i + 1].y - (m[i + 1] * h) / 3).toFixed(1)},`
      + ` ${pts[i + 1].x.toFixed(1)} ${pts[i + 1].y.toFixed(1)}`;
  }
  return d;
};

/**
 * Two-line probability-over-time chart (Dem vs Rep, which sum to 100%). Shared by the home-page
 * chamber "Race Timeline" and each race page's timeline so the two always look identical.
 * Smoothed lines with soft area fills, round-number gridlines, an emphasized 50% majority line,
 * named end-of-line callouts, and a unified crosshair tooltip.
 */
export const ProbabilityTrendChart = ({ data, demLabel, repLabel, width = 320, height = 150, demColor = DEM }: Props) => {
  const uid = useId().replace(/:/g, '');
  const [hover, setHover] = useState<number | null>(null);
  if (data.length < 2) return null;

  const pad = { top: 14, right: 74, bottom: 24, left: 36 };
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

  const demPts = dem.map((v, i) => ({ x: x(i), y: y(v) }));
  const repPts = rep.map((v, i) => ({ x: x(i), y: y(v) }));
  const demLine = smoothPath(demPts);
  const repLine = smoothPath(repPts);
  const floorY = pad.top + chh;

  const lastDem = dem[dem.length - 1], lastRep = rep[rep.length - 1];
  const stepW = cw / (data.length - 1);
  // Parse the YYYY-MM-DD portion as a LOCAL date so a UTC-midnight timestamp doesn't render as
  // the previous day (which made a July 1 point read "Jun 30").
  const fmtDate = (iso: string) => {
    const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  };

  const nTicks = Math.min(4, data.length);
  const dateTicks = Array.from({ length: nTicks }, (_, k) => Math.round((k / (nTicks - 1 || 1)) * (data.length - 1)));

  // End-of-line callouts: colored marker + name + value in ink. Nudged apart when the
  // two lines converge so the labels never collide.
  let demLabelY = y(lastDem), repLabelY = y(lastRep);
  const minGap = 15;
  if (Math.abs(demLabelY - repLabelY) < minGap) {
    const mid = (demLabelY + repLabelY) / 2;
    const dir = demLabelY <= repLabelY ? -1 : 1;
    demLabelY = mid + dir * (minGap / 2);
    repLabelY = mid - dir * (minGap / 2);
  }
  const clampY = (v: number) => Math.min(Math.max(v, pad.top + 5), floorY - 5);
  demLabelY = clampY(demLabelY); repLabelY = clampY(repLabelY);

  // End labels use the surname ("Jon Husted" -> "Husted") to fit beside the plot; the hover
  // card keeps full names. When surnames collide (e.g. two "Nominee" placeholders), fall back
  // to truncating the full labels so the two ends stay distinguishable.
  const lastWord = (s: string) => s.trim().split(/\s+/).pop() ?? s;
  let demShort = lastWord(demLabel), repShort = lastWord(repLabel);
  if (demShort === repShort) {
    const trunc = (s: string) => (s.length > 9 ? `${s.slice(0, 8)}…` : s);
    demShort = trunc(demLabel); repShort = trunc(repLabel);
  }

  const renderEndLabel = (labelY: number, color: string, name: string, value: number) => (
    <text x={(width - pad.right + 6).toFixed(1)} y={labelY.toFixed(1)} alignmentBaseline="middle"
          fontSize="10.5" fontWeight="700" fill={color}>
      {name} {(value * 100).toFixed(0)}%
    </text>
  );

  // Unified hover card: date header + one row per series, flipped left past mid-chart.
  const renderHoverCard = (i: number) => {
    const rows = [
      { color: demColor, name: demLabel, v: dem[i] },
      { color: REP, name: repLabel, v: rep[i] },
    ].sort((a, b) => b.v - a.v);
    const w = 14 + Math.max(...rows.map(r => r.name.length), 8) * 6.2 + 42;
    const h = 52;
    const cx = x(i);
    const px = cx > pad.left + cw * 0.55 ? cx - w - 12 : cx + 12;
    const py = Math.min(Math.max(Math.min(y(dem[i]), y(rep[i])) - 8, pad.top - 4), floorY - h);
    return (
      <g transform={`translate(${px.toFixed(1)}, ${py.toFixed(1)})`} pointerEvents="none">
        <rect width={w} height={h} rx="7" fill="#ffffff" stroke="#e5e8eb" strokeWidth="1" filter={`url(#shadow-${uid})`} />
        <text x="10" y="14" fontSize="9.5" fontWeight="700" fill={INK_MUTED} letterSpacing="0.06em">
          {fmtDate(data[i].date).toUpperCase()}
        </text>
        {rows.map((r, k) => (
          <g key={k} transform={`translate(10, ${25 + k * 14})`}>
            <text y="0.5" alignmentBaseline="middle" fontSize="10.5" fontWeight="600" fill={r.color}>{r.name}</text>
            <text x={w - 20} y="0.5" textAnchor="end" alignmentBaseline="middle" fontSize="10.5" fontWeight="700" fill={INK}>
              {(r.v * 100).toFixed(1)}%
            </text>
          </g>
        ))}
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
        <clipPath id={`plot-${uid}`}>
          <rect x={pad.left} y={pad.top} width={cw} height={chh} />
        </clipPath>
      </defs>

      {/* Recessive gridlines + y-axis labels; the 50% majority line is emphasized */}
      {ticks.map((t, i) => {
        const isHalf = Math.abs(t - 0.5) < 1e-9;
        return (
          <g key={i}>
            <line x1={pad.left} y1={y(t)} x2={width - pad.right} y2={y(t)}
                  stroke={isHalf ? '#c9ced4' : GRID} strokeWidth="1"
                  strokeDasharray={isHalf ? '4,3' : undefined} />
            <text x={pad.left - 6} y={y(t)} textAnchor="end" alignmentBaseline="middle"
                  fontSize="10" fontWeight={isHalf ? 700 : 400} fill={isHalf ? '#6b7280' : INK_MUTED}>
              {(t * 100).toFixed(0)}%
            </text>
          </g>
        );
      })}

      {/* Soft area fills + smoothed lines */}
      <g clipPath={`url(#plot-${uid})`}>
        <path d={repLine} fill="none" stroke={REP} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
        <path d={demLine} fill="none" stroke={demColor} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
      </g>

      {/* Current values: dot on the line + named callout */}
      <circle cx={x(data.length - 1)} cy={y(lastRep)} r="3.5" fill={REP} stroke="#fff" strokeWidth="1.5" />
      <circle cx={x(data.length - 1)} cy={y(lastDem)} r="3.5" fill={demColor} stroke="#fff" strokeWidth="1.5" />
      {renderEndLabel(repLabelY, REP, repShort, lastRep)}
      {renderEndLabel(demLabelY, demColor, demShort, lastDem)}

      {/* Date axis */}
      {dateTicks.map((idx, k) => (
        <text key={k} x={x(idx)} y={height - 6} fontSize="10" fill={INK_MUTED}
              textAnchor={k === 0 ? 'start' : k === dateTicks.length - 1 ? 'end' : 'middle'}>
          {fmtDate(data[idx].date)}
        </text>
      ))}

      {/* Hover crosshair + unified tooltip card */}
      {hover != null && (
        <g>
          <line x1={x(hover)} y1={pad.top} x2={x(hover)} y2={floorY} stroke="#d3d8dd" strokeWidth="1" strokeDasharray="3,3" />
          <circle cx={x(hover)} cy={y(dem[hover])} r="4" fill={demColor} stroke="#fff" strokeWidth="2" />
          <circle cx={x(hover)} cy={y(rep[hover])} r="4" fill={REP} stroke="#fff" strokeWidth="2" />
          {renderHoverCard(hover)}
        </g>
      )}

      {/* Invisible hover targets */}
      {data.map((_, i) => (
        <rect key={i} x={x(i) - stepW / 2} y={pad.top} width={Math.max(1, stepW)} height={chh}
              fill="transparent" onMouseEnter={() => setHover(i)} />
      ))}
    </svg>
  );
};
