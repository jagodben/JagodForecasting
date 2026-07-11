import { Link, useLocation } from 'react-router-dom';
import { AccessibilityToggle } from './AccessibilityToggle';
import { SocialLinks } from './SocialLinks';

// Slim site-wide bottom bar: about/methodology link, copyright, a light disclaimer, and (only on the
// map/home page, where it actually does something) the colorblind-pattern accessibility switch.
export const SiteFooter = () => {
  const onMap = useLocation().pathname === '/';
  return (
  <footer
    style={{
      // In normal document flow (not fixed): it sits at the bottom of the page, so it's out of
      // view when scrolled up and reached when you scroll to the bottom. flexShrink:0 keeps its
      // height inside the app's flex column.
      flexShrink: 0,
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
      About
    </Link>
    <span aria-hidden style={{ opacity: 0.4 }}>·</span>
    <span>A personal modeling project — not affiliated with any campaign.</span>
    <span aria-hidden style={{ opacity: 0.4 }}>·</span>
    <SocialLinks color="rgba(255,255,255,0.7)" size={15} />
    {onMap && (
      <>
        <span aria-hidden style={{ opacity: 0.4 }}>·</span>
        <AccessibilityToggle />
      </>
    )}
  </footer>
  );
};
