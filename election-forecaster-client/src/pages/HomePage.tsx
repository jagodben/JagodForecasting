import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { statesApi, racesApi } from '../services/api';
import { RaceMap } from '../components/maps/RaceMap';
import { USDistrictMap } from '../components/maps/USDistrictMap';
import { MapLegend } from '../components/maps/MapLegend';
import { RaceType } from '../types';

type MapView = 'senate' | 'house' | 'governors';

export const HomePage = () => {
  const [activeView, setActiveView] = useState<MapView>('senate');

  const { data: states, isLoading: statesLoading } = useQuery({
    queryKey: ['states'],
    queryFn: statesApi.getAll,
  });

  const { data: senateRaces, isLoading: senateLoading } = useQuery({
    queryKey: ['races', 'senate'],
    queryFn: () => racesApi.getAll(RaceType.Senate),
  });

  const { data: govRaces, isLoading: govLoading } = useQuery({
    queryKey: ['races', 'governor'],
    queryFn: () => racesApi.getAll(RaceType.Governor),
  });

  const { data: houseRaces, isLoading: houseLoading } = useQuery({
    queryKey: ['races', 'house'],
    queryFn: () => racesApi.getAll(RaceType.House),
  });

  const isLoading = statesLoading || senateLoading || govLoading || houseLoading;

  if (isLoading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>Loading election data...</p>
      </div>
    );
  }

  if (!states) {
    return (
      <div className="error-container">
        <h2>Error loading data</h2>
        <p>Please make sure the API server is running at http://localhost:5000</p>
        <button onClick={() => window.location.reload()}>Retry</button>
      </div>
    );
  }

  const getMapTitle = () => {
    switch (activeView) {
      case 'senate': return 'Senate Races';
      case 'house': return 'House Races';
      case 'governors': return 'Governor Races';
    }
  };

  const getMapDescription = () => {
    switch (activeView) {
      case 'senate': return 'Hover over a state to see Senate race details. Click to view full state info.';
      case 'house': return 'Hover over a district to see House race details. Scroll to zoom, drag to pan.';
      case 'governors': return 'Hover over a state to see Governor race details. Click to view full state info.';
    }
  };

  return (
    <div className="home-page">
      <header className="page-header">
        <h1>2026 Election Forecast</h1>
        <p>{getMapDescription()}</p>
      </header>

      {/* Map type tabs */}
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        gap: '8px',
        marginBottom: '24px',
      }}>
        {(['senate', 'house', 'governors'] as MapView[]).map((view) => (
          <button
            key={view}
            onClick={() => setActiveView(view)}
            style={{
              padding: '12px 24px',
              fontSize: '16px',
              fontWeight: activeView === view ? 'bold' : 'normal',
              backgroundColor: activeView === view ? '#333' : '#f0f0f0',
              color: activeView === view ? 'white' : '#333',
              border: 'none',
              borderRadius: '8px',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            {view === 'senate' ? 'Senate Map' : view === 'house' ? 'House Map' : 'Governors Map'}
          </button>
        ))}
      </div>

      <div className="map-section">
        <h2 style={{ textAlign: 'center', marginBottom: '16px' }}>{getMapTitle()}</h2>

        {activeView === 'senate' && senateRaces && (
          <RaceMap states={states} races={senateRaces} raceType={RaceType.Senate} />
        )}

        {activeView === 'governors' && govRaces && (
          <RaceMap states={states} races={govRaces} raceType={RaceType.Governor} />
        )}

        {activeView === 'house' && houseRaces && (
          <USDistrictMap races={houseRaces} />
        )}

        <MapLegend />
      </div>

      <div className="stats-section">
        <h2>Race Summary</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <span className="stat-value">{senateRaces?.length || 0}</span>
            <span className="stat-label">Senate Races</span>
          </div>
          <div className="stat-card">
            <span className="stat-value">{govRaces?.length || 0}</span>
            <span className="stat-label">Governor Races</span>
          </div>
          <div className="stat-card">
            <span className="stat-value">{houseRaces?.length || 0}</span>
            <span className="stat-label">House Races</span>
          </div>
        </div>
      </div>
    </div>
  );
};
