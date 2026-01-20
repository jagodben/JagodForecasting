import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { HomePage } from './pages/HomePage';
import { StatePage } from './pages/StatePage';
import './App.css';

function App() {
  return (
    <BrowserRouter>
      <div className="app">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/state/:stateId" element={<StatePage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
