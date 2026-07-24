import { Link, useLocation } from 'react-router-dom';
import { AccessibilityToggle } from './AccessibilityToggle';
import { SocialLinks } from './SocialLinks';

// Slim site-wide bottom bar (in normal flow, at the bottom of the page): copyright, a light
// disclaimer, and (only on the map/home page) the colorblind-pattern switch.
// On mobile it wraps and drops the long disclaimer so everything fits.
export const SiteFooter = () => {
  const onMap = useLocation().pathname === '/';
  return (
    <footer className="site-footer">
      <span>© {new Date().getFullYear()} Jagod Forecasting</span>
      <span className="site-footer__nav-mobile">
        <span className="site-footer__dot" aria-hidden>·</span>
        <Link to="/polls" className="site-footer__link">Polls</Link>
        <span className="site-footer__dot" aria-hidden>·</span>
        <Link to="/methodology" className="site-footer__link">About</Link>
      </span>
      <span className="site-footer__disclaimer">
        <span className="site-footer__dot" aria-hidden>·</span>
        <span>A personal modeling project — not affiliated with any campaign.</span>
      </span>
      <span className="site-footer__dot" aria-hidden>·</span>
      <SocialLinks color="rgba(255,255,255,0.7)" size={15} />
      {onMap && (
        <>
          <span className="site-footer__dot" aria-hidden>·</span>
          <AccessibilityToggle />
        </>
      )}
    </footer>
  );
};
