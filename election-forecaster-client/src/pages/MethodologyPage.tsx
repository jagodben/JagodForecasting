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
        <Item label="Polls">recency- and sample-size-weighted average of public polls.</Item>
        <Item label="Fundamentals">state partisan lean (Cook PVI), past results, and incumbency.</Item>
        <Item label="National environment">the national mood, from the generic congressional ballot average (presidential approval as a fallback).</Item>
      </ul>

      <p style={{ color: '#333333', fontSize: '15px', lineHeight: 1.6 }}>
        Inputs are weighted by how much signal each carries, combined into an expected margin, and
        converted to a win probability whose uncertainty shrinks as Election Day nears. Senate control
        comes from a Monte Carlo simulation over all races plus the seats not up this cycle.
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
