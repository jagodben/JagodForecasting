import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { SocialLinks } from '../components/SocialLinks';

const Item = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <li style={{ marginBottom: '12px' }}>
    <strong>{label}</strong> — {children}
  </li>
);

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
        <p style={{ margin: 0, color: '#555555', fontSize: '15px', lineHeight: 1.6 }}>
          Each 2026 race gets a single forecast by blending a few inputs. What the model considers:
        </p>
      </header>

      <ul style={{ paddingLeft: '20px', color: '#333333', fontSize: '15px', lineHeight: 1.6 }}>
        <Item label="Prediction markets">Polymarket odds for the race.</Item>
        <Item label="Polls">recency- and sample-size-weighted average of public polls — including
          district-level House polls where they exist. Partisan-sponsored polls count at half weight,
          and each pollster&rsquo;s measured lean is corrected before averaging.</Item>
        <Item label="Fundamentals">partisan lean (Cook PVI, district-level for the House), past
          results, and incumbency.</Item>
        <Item label="National environment">the generic congressional ballot average. House districts
          absorb only part of the national swing, matching how votes have translated into seats in
          recent cycles.</Item>
      </ul>

      <p style={{ color: '#333333', fontSize: '15px', lineHeight: 1.6 }}>
        Inputs are weighted by how much signal each carries, combined into an expected margin, and
        converted to a win probability using a fat-tailed distribution (big polling misses happen).
      </p>

      <p style={{ color: '#333333', fontSize: '15px', lineHeight: 1.6 }}>
        Uncertainty shrinks as Election Day nears; ranked-choice races (Alaska, Maine) carry extra.
        Chamber control comes from 10,000 Monte Carlo simulations with correlated national, regional,
        and race-level errors.
      </p>

      <p style={{ color: '#333333', fontSize: '15px', lineHeight: 1.6 }}>
        Everything updates once a day at 8:00 AM ET — polls, markets, and the candidates themselves,
        which are checked against Wikipedia so primaries, dropouts, and replacements show up
        automatically.
      </p>

      <p style={{ color: '#888888', fontSize: '13px', lineHeight: 1.6 }}>
        A personal modeling project — not affiliated with any campaign, and not professional guidance.
      </p>

      <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid #eee', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '16px' }}>
        <Link to="/" style={{ color: 'var(--dem-solid)', fontWeight: 500 }}>← Back to the map</Link>
        <SocialLinks color="#555555" size={20} />
      </div>
    </div>
  );
};
