import { Link } from 'react-router-dom';
import { AccessibilityToggle } from './AccessibilityToggle';

// Slim site-wide bottom bar: about/methodology link, copyright, a light disclaimer, and the
// colorblind-pattern accessibility switch.
export const SiteFooter = () => (
  <footer
    style={{
      position: 'fixed',
      left: 0,
      right: 0,
      bottom: 0,
      zIndex: 900,
      height: 40,
      display: 'flex',
      flexWrap: 'nowrap',
      alignItems: 'center',
      justifyContent: 'center',
      gap: '0 12px',
      padding: '0 16px',
      background: 'var(--bg-dark)',
      color: 'rgba(255,255,255,0.6)',
      fontSize: 12,
      textAlign: 'center',
      whiteSpace: 'nowrap',
      overflowX: 'auto',
    }}
  >
    <span>© {new Date().getFullYear()} Jagod Forecasting</span>
    <span aria-hidden style={{ opacity: 0.4 }}>·</span>
    <Link to="/methodology" style={{ color: 'rgba(255,255,255,0.85)', textDecoration: 'none' }}>
      About &amp; Methodology
    </Link>
    <span aria-hidden style={{ opacity: 0.4 }}>·</span>
    <span>A personal modeling project — not affiliated with any campaign.</span>
    <span aria-hidden style={{ opacity: 0.4 }}>·</span>
    <AccessibilityToggle />
  </footer>
);
