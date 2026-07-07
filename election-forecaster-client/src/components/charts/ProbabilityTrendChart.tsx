import { useState } from 'react';

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
}

const DEM = '#0033AA';
const REP = '#AA0000';

/**
 * Two-line probability-over-time chart (Dem vs Rep, which sum to 100%). Shared by the home-page
 * chamber-control chart and each race page. Auto-scales the y-axis to the data range so small
 * day-to-day moves are visible, with a legend, current-value callouts, and a hover crosshair.
 */
export const ProbabilityTrendChart = ({ data, demLabel, repLabel, width = 320, height = 150 }: Props) => {
  const [hover, setHover] = useState<number | null>(null);
  if (data.length < 2) return null;

  const pad = { top: 16, right: 48, bottom: 24, left: 40 };
  const cw = width - pad.left - pad.right;
  const chh = height - pad.top - pad.bottom;

  const dem = data.map(d => d.demValue);
  const rep = data.map(d => 1 - d.demValue);

  // Auto-scaled domain fitting both series, with >=6pt span and 15% headroom, clamped to [0,1].
  const all = [...dem, ...rep];
  const dataLo = Math.min(...all), dataHi = Math.max(...all);
  const span = Math.max(dataHi - dataLo, 0.06);
  const lo = Math.max(0, dataLo - span * 0.15);
  const hi = Math.min(1, dataHi + span * 0.15);

  const x = (i: number) => pad.left + (i / (data.length - 1)) * cw;
  const y = (v: number) => pad.top + chh - ((v - lo) / (hi - lo || 1)) * chh;
  const path = (vals: number[]) => vals.map((v, i) => `${i === 0 ? 'M' : 'L'} ${x(i).toFixed(1)} ${y(v).toFixed(1)}`).join(' ');

  const ticks = [lo, (lo + hi) / 2, hi];
  const lastDem = dem[dem.length - 1], lastRep = rep[rep.length - 1];
  const step = cw / (data.length - 1);
  const fmtDate = (iso: string) => new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });

  const nTicks = Math.min(4, data.length);
  const dateTicks = Array.from({ length: nTicks }, (_, k) => Math.round((k / (nTicks - 1 || 1)) * (data.length - 1)));

  // Floating pill label: colored marker + series name + value, placed beside the point.
  const renderPill = (cx: number, cy: number, color: string, label: string, value: string) => {
    const text = `${label} ${value}`;
    const h = 22;
    const w = 30 + text.length * 6.0;
    const onLeft = cx > pad.left + cw * 0.45;
    const px = onLeft ? cx - w - 12 : cx + 12;
    const py = Math.min(Math.max(cy - h / 2, pad.top - 6), height - pad.bottom - h + 6);
    return (
      <g transform={`translate(${px.toFixed(1)}, ${py.toFixed(1)})`}>
        <rect width={w} height={h} rx="6" fill="#ffffff" stroke="#eceff2" strokeWidth="1" filter="url(#pillShadow)" />
        <rect x="8" y={h / 2 - 4} width="8" height="8" rx="2" fill={color} />
        <text x="20" y={h / 2 + 0.5} alignmentBaseline="middle" fontSize="10.5" fontWeight="700" fill="#1f2937">{label} {value}</text>
      </g>
    );
  };

  // Emphasized date marker at the bottom of the crosshair, so the hovered day is unmistakable.
  const renderDateLabel = (cx: number, dateStr: string) => {
    const dw = dateStr.length * 5.8 + 14;
    const dx = Math.min(Math.max(cx, pad.left + dw / 2), width - pad.right - dw / 2);
    return (
      <g transform={`translate(${dx.toFixed(1)}, ${(pad.top + chh + 6).toFixed(1)})`}>
        <rect x={(-dw / 2).toFixed(1)} y="0" width={dw.toFixed(1)} height="15" rx="3" fill="#1f2937" />
        <text x="0" y="10.5" textAnchor="middle" fontSize="9.5" fontWeight="600" fill="#fff">{dateStr}</text>
      </g>
    );
  };

  return (
    <svg width="100%" viewBox={`0 0 ${width} ${height}`} style={{ display: 'block', overflow: 'visible' }}
         onMouseLeave={() => setHover(null)}>
      <defs>
        <filter id="pillShadow" x="-20%" y="-40%" width="140%" height="180%">
          <feDropShadow dx="0" dy="1" stdDeviation="1.5" floodColor="#0b1220" floodOpacity="0.18" />
        </filter>
      </defs>

      {/* Recessive gridlines + y-axis labels */}
      {ticks.map((t, i) => (
        <g key={i}>
          <line x1={pad.left} y1={y(t)} x2={width - pad.right} y2={y(t)} stroke="#eef0f2" strokeWidth="1" />
          <text x={pad.left - 6} y={y(t)} textAnchor="end" alignmentBaseline="middle" fontSize="10" fill="#9aa0a6">
            {(t * 100).toFixed(0)}%
          </text>
        </g>
      ))}

      <path d={path(rep)} fill="none" stroke={REP} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
      <path d={path(dem)} fill="none" stroke={DEM} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />

      {/* Current values */}
      <circle cx={x(data.length - 1)} cy={y(lastRep)} r="3.5" fill={REP} stroke="#fff" strokeWidth="1.5" />
      <text x={width - pad.right + 5} y={y(lastRep)} alignmentBaseline="middle" fontSize="12" fontWeight="700" fill={REP}>{(lastRep * 100).toFixed(0)}%</text>
      <circle cx={x(data.length - 1)} cy={y(lastDem)} r="3.5" fill={DEM} stroke="#fff" strokeWidth="1.5" />
      <text x={width - pad.right + 5} y={y(lastDem)} alignmentBaseline="middle" fontSize="12" fontWeight="700" fill={DEM}>{(lastDem * 100).toFixed(0)}%</text>

      {/* Date axis (hidden while hovering — the crosshair shows the exact date instead) */}
      {hover == null && dateTicks.map((idx, k) => (
        <text key={k} x={x(idx)} y={height - 6} fontSize="10" fill="#9aa0a6"
              textAnchor={k === 0 ? 'start' : k === dateTicks.length - 1 ? 'end' : 'middle'}>
          {fmtDate(data[idx].date)}
        </text>
      ))}

      {/* Hover crosshair + floating pill labels */}
      {hover != null && (
        <g>
          <line x1={x(hover)} y1={pad.top} x2={x(hover)} y2={pad.top + chh} stroke="#dfe3e7" strokeWidth="1" strokeDasharray="3,3" />
          <circle cx={x(hover)} cy={y(dem[hover])} r="4" fill={DEM} stroke="#fff" strokeWidth="1.5" />
          <circle cx={x(hover)} cy={y(rep[hover])} r="4" fill={REP} stroke="#fff" strokeWidth="1.5" />
          {renderPill(x(hover), y(dem[hover]), DEM, demLabel, `${(dem[hover] * 100).toFixed(1)}%`)}
          {renderPill(x(hover), y(rep[hover]), REP, repLabel, `${(rep[hover] * 100).toFixed(1)}%`)}
          {renderDateLabel(x(hover), fmtDate(data[hover].date))}
        </g>
      )}

      {/* Invisible hover targets */}
      {data.map((_, i) => (
        <rect key={i} x={x(i) - step / 2} y={pad.top} width={Math.max(1, step)} height={chh}
              fill="transparent" onMouseEnter={() => setHover(i)} />
      ))}
    </svg>
  );
};
