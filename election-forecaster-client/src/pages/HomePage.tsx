import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { statesApi, racesApi } from '../services/api';
import { RaceMap, SelectedStateData, getRatingColor, getRatingLabel } from '../components/maps/RaceMap';
import { USDistrictMap } from '../components/maps/USDistrictMap';
import { ChamberForecast } from '../components/forecast/ChamberForecast';
import { RaceType } from '../types';

type MapView = 'senate' | 'house' | 'governors';
type DataSource = 'combined' | 'markets' | 'polling';
type MobilePanel = 'map' | 'data';

export const HomePage = () => {
  const [activeView, setActiveView] = useState<MapView>('senate');
  const [dataSource, setDataSource] = useState<DataSource>('combined');
  const [mobilePanel, setMobilePanel] = useState<MobilePanel>('map');
  const [hasMarketData, setHasMarketData] = useState(false);
  const [hasPollingData, setHasPollingData] = useState(false);
  const [selectedState, setSelectedState] = useState<SelectedStateData | null>(null);

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
        <h2>JagodForecasting.com is undergoing maintenance</h2>
        <p>Check back later</p>
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
              onClick={() => {
                setActiveView(view);
                setSelectedState(null);
              }}
              className={`dashboard-tab ${activeView === view ? 'dashboard-tab--active' : ''}`}
            >
              {view === 'senate' ? 'Senate' : view === 'house' ? 'House' : 'Governors'}
            </button>
          ))}
        </div>
      </header>

      {/* Mobile panel toggle */}
      <div className="mobile-panel-toggle">
        <button
          className={`mobile-panel-btn ${mobilePanel === 'map' ? 'mobile-panel-btn--active' : ''}`}
          onClick={() => setMobilePanel('map')}
        >
          Map
        </button>
        <button
          className={`mobile-panel-btn ${mobilePanel === 'data' ? 'mobile-panel-btn--active' : ''}`}
          onClick={() => setMobilePanel('data')}
        >
          Forecast
        </button>
      </div>

      {/* Main content: map + sidebar */}
      <div className="dashboard-main">
        <div className={`dashboard-map ${mobilePanel === 'data' ? 'mobile-hidden' : ''}`}>
          {activeView === 'senate' && senateRaces && (
            <RaceMap states={states} races={senateRaces} raceType={RaceType.Senate} dataSource={dataSource} onStateSelect={setSelectedState} />
          )}
          {activeView === 'governors' && govRaces && (
            <RaceMap states={states} races={govRaces} raceType={RaceType.Governor} dataSource={dataSource} onStateSelect={setSelectedState} />
          )}
          {activeView === 'house' && houseRaces && (
            <USDistrictMap races={houseRaces} dataSource={dataSource} />
          )}

          {/* Data source control - mobile only (same as in forecast sidebar) */}
          <div className="mobile-data-source">
            <div className="forecast-sidebar__section">
              <div className="forecast-sidebar__label">Data Source</div>
              <div className="forecast-sidebar__sources">
                {(['combined', 'markets', 'polling'] as DataSource[]).map((source) => {
                  const isDisabled =
                    (source === 'markets' && !hasMarketData) ||
                    (source === 'polling' && !hasPollingData);
                  return (
                    <button
                      key={source}
                      onClick={() => {
                        if (!isDisabled) {
                          setDataSource(source);
                          setSelectedState(null);
                        }
                      }}
                      disabled={isDisabled}
                      className={`forecast-sidebar__source-btn ${dataSource === source ? 'forecast-sidebar__source-btn--active' : ''}`}
                    >
                      {source === 'combined' ? 'Forecast' : source === 'markets' ? 'Polymarket' : 'Polls'}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Selected state info - mobile only */}
            {selectedState && activeView !== 'house' && (
              <div className="mobile-state-info">
                <div className="mobile-state-info__header">
                  <span className="mobile-state-info__name">{selectedState.stateName}</span>
                  {selectedState.rating && (
                    <span
                      className="mobile-state-info__rating"
                      style={{ backgroundColor: getRatingColor(selectedState.rating) }}
                    >
                      {getRatingLabel(selectedState.rating)}
                    </span>
                  )}
                </div>
                {selectedState.demProb !== null && (
                  <div className="mobile-state-info__probs">
                    <div className="mobile-state-info__prob">
                      <img src="/democrat.png" alt="D" className="mobile-state-info__logo" />
                      <span className="mobile-state-info__value">{(selectedState.demProb * 100).toFixed(1)}%</span>
                    </div>
                    <div className="mobile-state-info__prob">
                      <img src="/republican.png" alt="R" className="mobile-state-info__logo" />
                      <span className="mobile-state-info__value">{((1 - selectedState.demProb) * 100).toFixed(1)}%</span>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {forecastRaces && (
          <div className={`dashboard-sidebar ${mobilePanel === 'map' ? 'mobile-hidden' : ''}`}>
            <ChamberForecast
              races={forecastRaces}
              raceType={forecastRaceType}
              compact
              dataSource={dataSource}
              onDataSourceChange={setDataSource}
              onDataAvailabilityChange={(hasMarket, hasPolling) => {
                setHasMarketData(hasMarket);
                setHasPollingData(hasPolling);
              }}
            />
          </div>
        )}
      </div>
    </div>
  );
};
