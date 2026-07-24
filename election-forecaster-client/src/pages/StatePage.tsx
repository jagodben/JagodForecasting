import { useParams, Link, useNavigate, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { statesApi } from '../services/api';
import { StateMap } from '../components/maps/StateMap';
import { RaceCard } from '../components/races/RaceCard';
import { RaceType } from '../types';
import { isTbdCandidate, TBD_NOTE } from '../utils/candidates';

export const StatePage = () => {
  const { stateId } = useParams<{ stateId: string }>();
  const navigate = useNavigate();
  // Set by the map/preview-card that navigated here; the Map breadcrumb returns to that tab.
  const fromView = (useLocation().state as { fromView?: string } | null)?.fromView;
  const mapHref = fromView === 'governors' || fromView === 'house' ? `/?view=${fromView}` : '/';

  const { data: state, isLoading, error } = useQuery({
    queryKey: ['state', stateId],
    queryFn: () => statesApi.getById(stateId!),
    enabled: !!stateId,
  });

  if (isLoading) {
    return (
      <div className="loading-container">
        <img src="/favicon.png" alt="" aria-hidden className="loading-glasses" />
        <p>Loading state data...</p>
      </div>
    );
  }

  if (error || !state) {
    return (
      <div className="error-container">
        <h2>State not found</h2>
        <Link to="/">Back to Map</Link>
      </div>
    );
  }

  const senateRaces = state.races.filter(r => r.type === RaceType.Senate);
  const govRaces = state.races.filter(r => r.type === RaceType.Governor);
  const houseRaces = state.races.filter(r => r.type === RaceType.House).sort((a, b) => (a.districtNumber || 0) - (b.districtNumber || 0));

  return (
    <div className="state-page">
      <nav className="breadcrumb">
        <Link to={mapHref}>Map</Link>
        <span> / </span>
        <span>{state.name}</span>
      </nav>

      <header className="state-header">
        <div className="state-title">
          <h1>{state.name}</h1>
        </div>
        <div className="state-info">
          <span>{state.congressionalDistricts} Congressional District{state.congressionalDistricts !== 1 ? 's' : ''}</span>
        </div>
      </header>

      {/* Large centered map */}
      <div style={{ width: '100%', display: 'flex', justifyContent: 'center', marginBottom: '40px' }}>
        <StateMap
          stateId={state.id}
          districts={state.districts}
          onDistrictClick={d => d.houseRace && navigate(`/race/${d.houseRace.id}`)}
        />
      </div>

      {/* Race data below */}
      <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '0 20px' }}>
        {(senateRaces.length > 0 || govRaces.length > 0) && (
          <div style={{
            display: 'grid',
            // min() so the 350px track floor can't push a narrow phone's page past its viewport.
            gridTemplateColumns: 'repeat(auto-fit, minmax(min(350px, 100%), 1fr))',
            gap: '24px',
            marginBottom: '32px'
          }}>
            {senateRaces.length > 0 && (
              <section className="race-section">
                <h2>Senate Race</h2>
                {senateRaces.map(race => (
                  <RaceCard key={race.id} race={race} />
                ))}
              </section>
            )}

            {govRaces.length > 0 && (
              <section className="race-section">
                <h2>Governor Race</h2>
                {govRaces.map(race => (
                  <RaceCard key={race.id} race={race} />
                ))}
              </section>
            )}
          </div>
        )}

        {houseRaces.length > 0 && (
          <section className="race-section">
            <h2>House Races</h2>
            <div style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(min(300px, 100%), 1fr))',
              gap: '16px'
            }}>
              {houseRaces.map(race => (
                <RaceCard key={race.id} race={race} />
              ))}
            </div>
          </section>
        )}
        {/* The race cards mark unresolved nominees with an asterisk; explain it once per page. */}
        {state.races.some(r => r.candidates.some(c => isTbdCandidate(c.name))) && (
          <div style={{ margin: '16px 0 24px', fontSize: '12px', color: '#6b6b6b' }}>{TBD_NOTE}</div>
        )}
      </div>
    </div>
  );
};
