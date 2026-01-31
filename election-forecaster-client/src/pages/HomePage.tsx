import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { statesApi, racesApi } from '../services/api';
import { RaceMap } from '../components/maps/RaceMap';
import { USDistrictMap } from '../components/maps/USDistrictMap';
import { MapLegend } from '../components/maps/MapLegend';
import { ChamberForecast } from '../components/forecast/ChamberForecast';
import { RaceType } from '../types';

type MapView = 'senate' | 'house' | 'governors';
type DataSource = 'combined' | 'markets' | 'polling';

export const HomePage = () => {
  const [activeView, setActiveView] = useState<MapView>('senate');
  const [dataSource, setDataSource] = useState<DataSource>('combined');

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

  const forecastRaces =
    activeView === 'senate' ? senateRaces :
    activeView === 'house' ? houseRaces :
    govRaces;

  const forecastRaceType =
    activeView === 'senate' ? RaceType.Senate :
    activeView === 'house' ? RaceType.House :
    RaceType.Governor;

  return (
    <div className="dashboard">
      {/* Header row */}
      <header className="dashboard-header">
        <h1 className="dashboard-title">2026 Election Forecast</h1>
        <div className="dashboard-tabs">
          {(['senate', 'house', 'governors'] as MapView[]).map((view) => (
            <button
              key={view}
              onClick={() => setActiveView(view)}
              className={`dashboard-tab ${activeView === view ? 'dashboard-tab--active' : ''}`}
            >
              {view === 'senate' ? 'Senate' : view === 'house' ? 'House' : 'Governors'}
            </button>
          ))}
        </div>
      </header>

      {/* Main content: map + sidebar */}
      <div className="dashboard-main">
        <div className="dashboard-map">
          {activeView === 'senate' && senateRaces && (
            <RaceMap states={states} races={senateRaces} raceType={RaceType.Senate} dataSource={dataSource} />
          )}
          {activeView === 'governors' && govRaces && (
            <RaceMap states={states} races={govRaces} raceType={RaceType.Governor} dataSource={dataSource} />
          )}
          {activeView === 'house' && houseRaces && (
            <USDistrictMap races={houseRaces} dataSource={dataSource} />
          )}
        </div>

        {forecastRaces && (
          <div className="dashboard-sidebar">
            <ChamberForecast
              races={forecastRaces}
              raceType={forecastRaceType}
              compact
              dataSource={dataSource}
              onDataSourceChange={setDataSource}
            />
          </div>
        )}
      </div>

      {/* Legend row */}
      <div className="dashboard-legend">
        <MapLegend />
      </div>
    </div>
  );
};
