import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { HomePage } from './pages/HomePage';
import { StatePage } from './pages/StatePage';
import { RacePage } from './pages/RacePage';
import './App.css';

function App() {
  return (
    <BrowserRouter>
      <div className="app">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/state/:stateId" element={<StatePage />} />
          <Route path="/race/:raceId" element={<RacePage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
