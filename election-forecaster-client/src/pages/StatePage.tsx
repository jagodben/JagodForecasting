import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { statesApi } from '../services/api';
import { StateMap } from '../components/maps/StateMap';
import { RaceCard } from '../components/races/RaceCard';
import { RaceType, RaceRating } from '../types';

const getRatingLabel = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return 'Solid Democrat';
    case RaceRating.LikelyDem: return 'Likely Democrat';
    case RaceRating.LeanDem: return 'Lean Democrat';
    case RaceRating.Tossup: return 'Tossup';
    case RaceRating.LeanRep: return 'Lean Republican';
    case RaceRating.LikelyRep: return 'Likely Republican';
    case RaceRating.SolidRep: return 'Solid Republican';
    default: return 'Unknown';
  }
};

const getRatingColor = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return '#0015BC';
    case RaceRating.LikelyDem: return '#3355DD';
    case RaceRating.LeanDem: return '#7799EE';
    case RaceRating.Tossup: return '#9966CC';
    case RaceRating.LeanRep: return '#EE7777';
    case RaceRating.LikelyRep: return '#DD3333';
    case RaceRating.SolidRep: return '#BC0000';
    default: return '#CCCCCC';
  }
};

export const StatePage = () => {
  const { stateId } = useParams<{ stateId: string }>();

  const { data: state, isLoading, error } = useQuery({
    queryKey: ['state', stateId],
    queryFn: () => statesApi.getById(stateId!),
    enabled: !!stateId,
  });

  if (isLoading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
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
        <Link to="/">Map</Link>
        <span> / </span>
        <span>{state.name}</span>
      </nav>

      <header className="state-header">
        <div className="state-title">
          <h1>{state.name}</h1>
          <span
            className="rating-badge"
            style={{
              backgroundColor: getRatingColor(state.overallRating),
              color: 'white',
              padding: '8px 16px',
              borderRadius: '20px',
              fontWeight: 'bold',
            }}
          >
            {getRatingLabel(state.overallRating)}
          </span>
        </div>
        <div className="state-info">
          <span>{state.electoralVotes} Electoral Votes</span>
          <span>{state.congressionalDistricts} Congressional District{state.congressionalDistricts !== 1 ? 's' : ''}</span>
        </div>
      </header>

      <div className="state-content">
        <div className="races-column">
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

          {houseRaces.length > 0 && (
            <section className="race-section">
              <h2>House Races</h2>
              <div className="house-races-list">
                {houseRaces.map(race => (
                  <RaceCard key={race.id} race={race} />
                ))}
              </div>
            </section>
          )}
        </div>

        <div className="map-column">
          <StateMap
            stateId={state.id}
            districts={state.districts}
          />
        </div>
      </div>
    </div>
  );
};
