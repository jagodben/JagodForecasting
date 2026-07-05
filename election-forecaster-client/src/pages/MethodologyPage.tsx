import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../utils/useDocumentTitle';

const Section = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <section style={{ marginBottom: '32px' }}>
    <h2 style={{ fontSize: '20px', margin: '0 0 10px 0' }}>{title}</h2>
    <div style={{ color: '#374151', lineHeight: 1.65, fontSize: '15px' }}>{children}</div>
  </section>
);

export const MethodologyPage = () => {
  useDocumentTitle('Methodology');

  return (
    <div style={{ backgroundColor: 'white', minHeight: '100vh', padding: '20px', maxWidth: '760px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>Methodology</span>
      </nav>

      <header style={{ marginBottom: '28px' }}>
        <h1 style={{ margin: '0 0 8px 0' }}>Methodology</h1>
        <p style={{ margin: 0, color: '#6b7280', fontSize: '15px' }}>
          How this site turns markets, polls, and fundamentals into a single forecast for each 2026 race
          and for control of each chamber.
        </p>
      </header>

      <Section title="Data sources">
        <p>Each race's forecast combines up to four inputs:</p>
        <ul style={{ paddingLeft: '20px' }}>
          <li>
            <strong>Prediction markets</strong> — live win probabilities from Polymarket's 2026
            general-election markets, weighted by trading volume.
          </li>
          <li>
            <strong>Polling</strong> — a recency- and sample-size-weighted average of public polls for
            Senate and Governor races, parsed from the corresponding Wikipedia race articles.
          </li>
          <li>
            <strong>Fundamentals</strong> — a structural baseline from each state's Cook Partisan Voting
            Index, prior results where available, incumbency, and the midterm environment.
          </li>
          <li>
            <strong>National environment</strong> — presidential approval sets the overall tilt of the
            year. With a Republican president in 2026, Democrats are the midterm out-party, so weaker
            approval implies a more Democratic-leaning national mood.
          </li>
        </ul>
      </Section>

      <Section title="Blending the inputs">
        <p>
          Every input is expressed as an <strong>expected margin</strong> (the Democratic lead in points),
          not just a probability, so the sources can be averaged on a common scale. Markets are inverted
          through the same uncertainty model that later converts the blended margin back into a
          probability, so a market price round-trips to itself.
        </p>
        <p>
          The sources are then combined with weights that reflect how informative each one is: markets
          carry more weight when a race is actively traded, polling weight grows with the number and
          recency of polls, and fundamentals act as the anchor when little direct data exists. Weights
          also shift as Election Day approaches and direct signals become more decisive.
        </p>
      </Section>

      <Section title="From margin to probability">
        <p>
          The blended margin is converted to a win probability through a normal model whose standard
          error depends on the race type and the time left until the election. Uncertainty is largest far
          out and narrows toward Election Day, so the same margin implies a more confident probability in
          November than in the spring. Probabilities are capped short of 0% and 100% to reflect
          irreducible uncertainty.
        </p>
      </Section>

      <Section title="Chamber control">
        <p>
          Control of the Senate is estimated with a Monte Carlo simulation of ten thousand elections. Each
          simulation applies a shared national swing (so races move together, not independently) plus a
          race-specific error, tallies the seats each party wins, and adds them to the seats not up for
          election this cycle. Control is the share of simulations in which a party reaches a governing
          majority. This is the number shown as the chamber win probability and plotted on the timeline —
          it accounts for the existing balance of held seats and the majority threshold, which a simple
          count of favored races would not.
        </p>
      </Section>

      <Section title="History and the timeline">
        <p>
          The model's forecast history is reconstructed retrospectively: for each past day it rebuilds the
          market and polling inputs as they stood then — daily market prices from the market's price
          history, and only the polls conducted on or before that date — and re-runs the same blend. The
          result is a like-for-like record of what the model would have said over time, rather than a log
          of live snapshots.
        </p>
      </Section>

      <Section title="Caveats">
        <ul style={{ paddingLeft: '20px' }}>
          <li>House races use structural fundamentals only; district-level markets and polling are sparse.</li>
          <li>Market and candidate mappings are maintained by hand and can lag primaries and market changes.</li>
          <li>Seats not up for election are held at their existing partisan split.</li>
          <li>This is a personal modeling project, not professional election guidance.</li>
        </ul>
      </Section>

      <div style={{ marginTop: '32px', paddingTop: '16px', borderTop: '1px solid #eee' }}>
        <Link to="/" style={{ color: '#6366f1', fontWeight: 500 }}>← Back to the map</Link>
      </div>
    </div>
  );
};
