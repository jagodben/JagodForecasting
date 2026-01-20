import { useQuery } from '@tanstack/react-query';
import { statesApi } from '../services/api';
import { USMap } from '../components/maps/USMap';
import { MapLegend } from '../components/maps/MapLegend';

export const HomePage = () => {
  const { data: states, isLoading, error } = useQuery({
    queryKey: ['states'],
    queryFn: statesApi.getAll,
  });

  if (isLoading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>Loading election data...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="error-container">
        <h2>Error loading data</h2>
        <p>Please make sure the API server is running at http://localhost:5000</p>
        <button onClick={() => window.location.reload()}>Retry</button>
      </div>
    );
  }

  return (
    <div className="home-page">
      <header className="page-header">
        <h1>2026 Election Forecast</h1>
        <p>Click on a state to view detailed race information</p>
      </header>

      <div className="map-section">
        {states && <USMap states={states} />}
        <MapLegend />
      </div>

      <div className="stats-section">
        <h2>Race Summary</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <span className="stat-value">{states?.length || 0}</span>
            <span className="stat-label">States</span>
          </div>
          <div className="stat-card">
            <span className="stat-value">{states?.reduce((acc, s) => acc + s.raceCount, 0) || 0}</span>
            <span className="stat-label">Total Races</span>
          </div>
          <div className="stat-card">
            <span className="stat-value">{states?.reduce((acc, s) => acc + s.electoralVotes, 0) || 0}</span>
            <span className="stat-label">Electoral Votes</span>
          </div>
        </div>
      </div>
    </div>
  );
};
