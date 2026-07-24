import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { statesApi, racesApi } from '../services/api';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { RaceMap, SelectedStateData, getRatingColor, getRatingLabel } from '../components/maps/RaceMap';
import { USDistrictMap, SelectedDistrictData } from '../components/maps/USDistrictMap';
import { ChamberForecast } from '../components/forecast/ChamberForecast';
import { RaceType } from '../types';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { districtCode } from '../utils/districts';

type MapView = 'senate' | 'house' | 'governors';
type MobilePanel = 'map' | 'data';

// The forecast now surfaces only the combined model — no Polymarket/Polls lens selector. The maps
// still take a dataSource prop, so keep passing the fixed 'combined' value.
const dataSource = 'combined' as const;

export const HomePage = () => {
  const navigate = useNavigate();
  // The active view lives in the URL (?view=house|governors; Senate is the bare "/") so
  // navigating into a state/race and coming back — or sharing the link — restores the tab.
  const [searchParams, setSearchParams] = useSearchParams();
  const viewParam = searchParams.get('view');
  const activeView: MapView = viewParam === 'house' || viewParam === 'governors' ? viewParam : 'senate';
  const [mobilePanel, setMobilePanel] = useState<MobilePanel>('map');
  const [selectedState, setSelectedState] = useState<SelectedStateData | null>(null);
  const [selectedDistrict, setSelectedDistrict] = useState<SelectedDistrictData | null>(null);

  useDocumentTitle(
    `2026 ${activeView === 'senate' ? 'Senate' : activeView === 'house' ? 'House' : 'Governor'} Forecast`
  );

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

  // Only block the first paint on what the current view actually needs (the state
  // geography + this chamber's races). The other chambers keep loading in the
  // background, so switching tabs is instant without gating the initial render on
  // the 435-race House list.
  const activeRacesLoading =
    activeView === 'senate' ? senateLoading :
    activeView === 'house' ? houseLoading :
    govLoading;

  if (statesLoading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>Loading the map…</p>
      </div>
    );
  }

  if (!states) {
    return (
      <div className="error-container">
        <h2>We couldn&rsquo;t load the forecast</h2>
        <p>This is usually temporary. Please try again in a moment.</p>
        <button onClick={() => window.location.reload()}>Try again</button>
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
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <img src="/favicon.png" alt="" aria-hidden className="dashboard-logo" />
          <h1 className="dashboard-title">Jagod Forecasting 2026</h1>
          <Link to="/polls" className="dashboard-header__link">All Polls</Link>
          <Link to="/methodology" className="dashboard-header__link">About</Link>
        </div>
        <div className="dashboard-tabs">
          {(['senate', 'house', 'governors'] as MapView[]).map((view) => (
            <button
              key={view}
              onClick={() => {
                // replace (not push) so flipping tabs doesn't stack history entries; the
                // current entry always carries the view the user is looking at.
                setSearchParams(view === 'senate' ? {} : { view }, { replace: true });
                setSelectedState(null);
                setSelectedDistrict(null);
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
        <div
          className={`dashboard-map ${mobilePanel === 'data' ? 'mobile-hidden' : ''}`}
          onClick={(e) => {
            // Clicking off the map (background, not a state/district shape, control, or info
            // card) returns to the default no-selection view.
            const el = e.target as Element;
            if (el.closest('path, button, a, .mobile-state-info, .mobile-projected, .mobile-chamber-card')) return;
            setSelectedState(null);
            setSelectedDistrict(null);
          }}
        >
          {activeRacesLoading && (
            <div className="loading-container">
              <div className="spinner" />
            </div>
          )}
          <div className="dashboard-map__inner">
            {activeView === 'senate' && senateRaces && (
              <RaceMap states={states} races={senateRaces} raceType={RaceType.Senate} dataSource={dataSource} onStateSelect={setSelectedState} />
            )}
            {activeView === 'governors' && govRaces && (
              <RaceMap states={states} races={govRaces} raceType={RaceType.Governor} dataSource={dataSource} onStateSelect={setSelectedState} />
            )}
            {activeView === 'house' && houseRaces && (
              <USDistrictMap races={houseRaces} dataSource={dataSource} onDistrictSelect={setSelectedDistrict} />
            )}
          </div>

          {/* Selected state info - mobile only (Senate/Governors) */}
          <div className="mobile-data-source">
            {/* Nothing selected yet: fill the pane with the chamber topline instead of dead space,
                and tell the user the map is tappable. */}
            {(activeView === 'house' ? !selectedDistrict : !selectedState) && forecastRaces && (
              <ChamberForecast races={forecastRaces} raceType={forecastRaceType} compact />
            )}
            {selectedState && activeView !== 'house' && (
              <>
                <div
                  className="mobile-state-info mobile-state-info--tappable"
                  role="button"
                  tabIndex={0}
                  onClick={() => navigate(selectedState.raceId ? `/race/${selectedState.raceId}` : `/state/${selectedState.stateId}`, { state: { fromView: activeView } })}
                  onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); navigate(selectedState.raceId ? `/race/${selectedState.raceId}` : `/state/${selectedState.stateId}`, { state: { fromView: activeView } }); } }}
                >
                  <div className="mobile-state-info__header">
                    <span className="mobile-state-info__name">{selectedState.stateId}</span>
                    {selectedState.rating && (
                      <span
                        className="mobile-state-info__rating"
                        style={{ backgroundColor: getRatingColor(selectedState.rating)}}
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
                {selectedState.marginText && (
                  <div className="mobile-projected">
                    <span className="mobile-projected__label">Projected Result</span>
                    <span className="mobile-projected__value" style={{ color: selectedState.marginColor }}>
                      {selectedState.marginText}
                    </span>
                  </div>
                )}
              </>
            )}

            {/* Selected district info - mobile only (House). Tap it to open the full race page. */}
            {selectedDistrict && activeView === 'house' && (
              <>
                <div
                  className="mobile-state-info mobile-state-info--tappable"
                  role="button"
                  tabIndex={0}
                  onClick={() => selectedDistrict.raceId && navigate(`/race/${selectedDistrict.raceId}`)}
                  onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); if (selectedDistrict.raceId) navigate(`/race/${selectedDistrict.raceId}`); } }}
                >
                  <div className="mobile-state-info__header">
                    <span className="mobile-state-info__name">
                      {districtCode(selectedDistrict.stateId, selectedDistrict.districtNumber)}
                    </span>
                    {selectedDistrict.rating && (
                      <span
                        className="mobile-state-info__rating"
                        style={{ backgroundColor: getRatingColor(selectedDistrict.rating)}}
                      >
                        {getRatingLabel(selectedDistrict.rating)}
                      </span>
                    )}
                  </div>
                  {selectedDistrict.demProb !== null && (
                    <div className="mobile-state-info__probs">
                      <div className="mobile-state-info__prob">
                        <img src="/democrat.png" alt="D" className="mobile-state-info__logo" />
                        <span className="mobile-state-info__value">{(selectedDistrict.demProb * 100).toFixed(1)}%</span>
                      </div>
                      <div className="mobile-state-info__prob">
                        <img src="/republican.png" alt="R" className="mobile-state-info__logo" />
                        <span className="mobile-state-info__value">{((1 - selectedDistrict.demProb) * 100).toFixed(1)}%</span>
                      </div>
                    </div>
                  )}
                </div>
                {selectedDistrict.marginText && (
                  <div className="mobile-projected">
                    <span className="mobile-projected__label">Projected Result</span>
                    <span className="mobile-projected__value" style={{ color: selectedDistrict.marginColor }}>
                      {selectedDistrict.marginText}
                    </span>
                  </div>
                )}
              </>
            )}
          </div>
        </div>

        {forecastRaces && (
          <div className={`dashboard-sidebar ${mobilePanel === 'map' ? 'mobile-hidden' : ''}`}>
            <ChamberForecast
              races={forecastRaces}
              raceType={forecastRaceType}
            />
          </div>
        )}
      </div>
    </div>
  );
};
