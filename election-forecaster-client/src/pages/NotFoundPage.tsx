import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../utils/useDocumentTitle';

// Catch-all for unknown URLs — without it a bad link rendered a blank page with just the footer.
export const NotFoundPage = () => {
  useDocumentTitle('Page not found');
  return (
    <div className="error-container">
      <h2>Page not found</h2>
      <p>That page doesn&rsquo;t exist — it may have moved, or the link is wrong.</p>
      <Link to="/">← Back to the map</Link>
    </div>
  );
};
