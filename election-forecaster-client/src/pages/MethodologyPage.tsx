import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { SocialLinks } from '../components/SocialLinks';

const INK = '#333333';
const MUTED = '#555555';
const FAINT = '#6b6b6b';

const body: React.CSSProperties = { color: INK, fontSize: '15px', lineHeight: 1.6, margin: '0 0 12px 0' };
const caption: React.CSSProperties = { color: FAINT, fontSize: '13px', lineHeight: 1.5, margin: '8px 0 0 0' };

const Item = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <li style={{ marginBottom: '12px' }}>
    <strong>{label}</strong> — {children}
  </li>
);

const Section = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <section>
    <h2 style={{ fontSize: '18px', margin: '28px 0 10px 0', paddingBottom: '6px', borderBottom: '1px solid #eeeeee' }}>
      {title}
    </h2>
    {children}
  </section>
);

/* ---------------------------------------------------------------- pipeline */

const stageBox: React.CSSProperties = {
  border: '1px solid #dddddd',
  backgroundColor: '#fafafa',
  borderRadius: '3px',
  padding: '8px 12px',
  textAlign: 'center',
  fontSize: '13px',
  color: INK,
};

const Arrow = () => (
  <div aria-hidden style={{ textAlign: 'center', color: '#999999', fontSize: '15px', lineHeight: 1, margin: '5px 0' }}>
    ↓
  </div>
);

const Pipeline = () => (
  <div style={{ margin: '16px 0 4px 0' }}>
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
      {[
        ['Polls', 'weighted average'],
        ['Fundamentals', 'lean · incumbency · history'],
        ['National environment', 'generic ballot'],
        ['Markets', 'Polymarket odds'],
      ].map(([name, sub]) => (
        <div key={name} style={{ ...stageBox, flex: '1 1 40%', padding: '8px 6px', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
          <strong>{name}</strong>
          <div style={{ color: '#777777', fontSize: '12px' }}>{sub}</div>
        </div>
      ))}
    </div>
    <Arrow />
    <div style={stageBox}>One expected margin per race — e.g. D+2.4</div>
    <Arrow />
    <div style={stageBox}>A fat-tailed probability curve around that margin → win probability</div>
    <Arrow />
    <div style={stageBox}>10,000 simulated elections, sharing national and regional polling misses across races</div>
    <Arrow />
    <div style={stageBox}>Race ratings, seat counts, and chamber odds</div>
  </div>
);

/* ------------------------------------------------------------ weight chart */

const WEIGHT_COLORS = { polls: '#121212', fundamentals: '#6e6e6e', markets: '#c9c9c9' } as const;
const WEIGHT_KEYS = ['polls', 'fundamentals', 'markets'] as const;
const WEIGHT_LABELS = {
  polls: 'Polls',
  fundamentals: 'Fundamentals (incl. national environment)',
  markets: 'Markets',
} as const;

// Normalized shares for a typical fully-polled Senate race in each calendar phase,
// mirroring the backend WeightCalculator (base 45/40/15, shifted by time to election).
const WEIGHT_PHASES = [
  { label: '2–6 months out', minDays: 60, polls: 47, fundamentals: 39, markets: 14 },
  { label: '2 weeks – 2 months out', minDays: 14, polls: 63, fundamentals: 24, markets: 13 },
  { label: 'Final two weeks', minDays: -Infinity, polls: 69, fundamentals: 19, markets: 12 },
];

const WeightChart = () => {
  const daysToElection = (new Date('2026-11-03').getTime() - Date.now()) / 86_400_000;
  const currentPhase = WEIGHT_PHASES.findIndex((p) => daysToElection > p.minDays);

  return (
    <div style={{ margin: '16px 0 4px 0' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 16px', fontSize: '13px', color: MUTED, marginBottom: '10px' }}>
        {WEIGHT_KEYS.map((k) => (
          <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ width: '11px', height: '11px', borderRadius: '2px', backgroundColor: WEIGHT_COLORS[k], display: 'inline-block' }} />
            {WEIGHT_LABELS[k]}
          </span>
        ))}
      </div>
      {WEIGHT_PHASES.map((phase, i) => (
        <div key={phase.label} style={{ marginBottom: '10px' }}>
          <div style={{ fontSize: '13px', color: MUTED, marginBottom: '3px' }}>
            {phase.label}
            {i === currentPhase && <strong style={{ color: '#121212' }}> · now</strong>}
          </div>
          <div style={{ display: 'flex', gap: '2px', height: '26px' }}>
            {WEIGHT_KEYS.map((k) => (
              <div
                key={k}
                style={{
                  width: `${phase[k]}%`,
                  backgroundColor: WEIGHT_COLORS[k],
                  color: k === 'markets' ? INK : 'white',
                  fontSize: '11px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  borderRadius: '2px',
                }}
              >
                {phase[k]}%
              </div>
            ))}
          </div>
        </div>
      ))}
      <p style={caption}>
        Typical shares for a well-polled Senate race. Races with no polls or no market renormalize
        toward what remains — a never-polled House seat runs almost entirely on fundamentals and
        the generic ballot.
      </p>
    </div>
  );
};

/* -------------------------------------------------------------- tail chart */

const TailChart = () => {
  const W = 640, H = 170, PAD = 8, BASE = H - 14, SCALE = 360;
  const toPath = (pdf: (x: number) => number) =>
    Array.from({ length: 201 }, (_, i) => {
      const x = -5 + i * 0.05;
      const px = PAD + ((x + 5) / 10) * (W - 2 * PAD);
      const py = BASE - pdf(x) * SCALE;
      return `${i === 0 ? 'M' : 'L'}${px.toFixed(1)} ${py.toFixed(1)}`;
    }).join(' ');
  const normal = (x: number) => Math.exp((-x * x) / 2) / Math.sqrt(2 * Math.PI);
  const t5 = (x: number) => 0.3796066898 * Math.pow(1 + (x * x) / 5, -3);

  return (
    <div style={{ margin: '16px 0 4px 0' }}>
      <svg
        viewBox={`0 0 ${W} ${H}`}
        style={{ width: '100%', height: 'auto', display: 'block' }}
        role="img"
        aria-label="The model's fat-tailed error curve compared with a normal curve: similar in the middle, but the fat-tailed curve stays meaningfully above zero far from the center."
      >
        <line x1={PAD} y1={BASE} x2={W - PAD} y2={BASE} stroke="#dddddd" strokeWidth="1" />
        <path d={toPath(normal)} fill="none" stroke="#b3b3b3" strokeWidth="2" strokeDasharray="5 4" />
        <path d={toPath(t5)} fill="none" stroke="#121212" strokeWidth="2" />
      </svg>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 16px', fontSize: '13px', color: MUTED, marginTop: '6px' }}>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
          <span style={{ width: '22px', borderTop: '2px solid #121212', display: 'inline-block' }} />
          the model&rsquo;s error curve (Student-t, 5 df)
        </span>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
          <span style={{ width: '22px', borderTop: '2px dashed #b3b3b3', display: 'inline-block' }} />
          a normal curve
        </span>
      </div>
      <p style={caption}>
        Both curves agree on ordinary polling error. The difference is in the tails: the fat-tailed
        curve keeps real probability on very large misses.
      </p>
    </div>
  );
};

/* --------------------------------------------------------------- the page */

export const MethodologyPage = () => {
  useDocumentTitle('About');

  return (
    <div style={{ backgroundColor: 'white', minHeight: '100vh', padding: '20px', maxWidth: '640px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>About</span>
      </nav>

      <header style={{ marginBottom: '20px' }}>
        <h1 style={{ margin: '0 0 8px 0' }}>About</h1>
        <p style={{ ...body, color: MUTED, margin: 0 }}>
          Each 2026 race gets a single forecast by blending four inputs, listed here in rough order
          of weight:
        </p>
      </header>

      <ul style={{ paddingLeft: '20px', color: INK, fontSize: '15px', lineHeight: 1.6 }}>
        <Item label="Polls">recency- and sample-size-weighted average of public polls — including
          district-level House polls where they exist. Partisan-sponsored polls count at half weight,
          and each pollster&rsquo;s measured lean is corrected before averaging.</Item>
        <Item label="Fundamentals">partisan lean, past results, and incumbency.</Item>
        <Item label="National environment">the generic congressional ballot average. House districts
          absorb only part of the national swing, matching how votes have translated into seats in
          recent cycles.</Item>
        <Item label="Prediction markets">Polymarket odds for the race.</Item>
      </ul>

      <Section title="How it fits together">
        <Pipeline />
      </Section>

      <Section title="Blending the inputs">
        <p style={body}>
          The base blend is 45% polls, 40% fundamentals, 15% markets — shifting toward polls as
          Election Day nears, and renormalizing around what each race actually has.
        </p>
        <WeightChart />
      </Section>

      <Section title="From margin to probability">
        <p style={body}>
          Each race&rsquo;s margin carries an error bar — wide a year out, tighter in the final
          weeks, extra for governor&rsquo;s races and the ranked-choice states (Alaska, Maine), and
          floored so no race is ever a certainty. It becomes a win probability through a fat-tailed
          curve, because big polling misses happen more often than a bell curve allows:
        </p>
        <TailChart />
      </Section>

      <Section title="Simulating the chambers">
        <p style={body}>
          Chamber odds come from 10,000 simulated elections. Each simulation shares one national
          polling miss and one regional miss across races before adding race-level noise, so upsets
          cluster the way they do in real elections. Democrats need 51 Senate seats (the Vice
          President breaks ties for Republicans) and 218 House seats.
        </p>
      </Section>

      <Section title="The daily rhythm">
        <p style={body}>
          Everything updates once a day at 8:00 AM ET — polls, markets, and the candidates
          themselves, so primaries, dropouts, and replacements show up automatically. Each
          day&rsquo;s forecast is recorded and never revised in hindsight; on November 3 the model
          takes its final snapshot and freezes.
        </p>
      </Section>

      <p style={{ ...caption, marginTop: '24px' }}>
        A personal modeling project — not affiliated with any campaign, and not professional guidance.
      </p>

      <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid #eee', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px' }}>
        <Link to="/" style={{ color: 'var(--dem-solid)', fontWeight: 500 }}>← Back to the map</Link>
        <SocialLinks color="#555555" size={20} />
      </div>
    </div>
  );
};
