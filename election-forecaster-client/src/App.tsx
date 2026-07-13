import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { HomePage } from './pages/HomePage';
import { StatePage } from './pages/StatePage';
import { RacePage } from './pages/RacePage';
import { MethodologyPage } from './pages/MethodologyPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { AccessibilityProvider } from './context/AccessibilityContext';
import { SiteFooter } from './components/SiteFooter';
import './App.css';

function App() {
  return (
    <AccessibilityProvider>
      <BrowserRouter>
        <div className="app">
          <div className="app-content">
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/state/:stateId" element={<StatePage />} />
              <Route path="/race/:raceId" element={<RacePage />} />
              <Route path="/methodology" element={<MethodologyPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </div>
          <SiteFooter />
        </div>
      </BrowserRouter>
    </AccessibilityProvider>
  );
}

export default App;
