import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { racesApi, forecastApi, statesApi } from '../services/api';
import { RaceType, RaceRating, Party } from '../types';

const getRatingLabel = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return 'Solid Democrat';
    case RaceRating.LikelyDem: return 'Likely Democrat';
    case RaceRating.LeanDem: return 'Lean Democrat';
    case RaceRating.TiltDem: return 'Tilt Democrat';
    case RaceRating.TiltRep: return 'Tilt Republican';
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
    case RaceRating.TiltDem: return '#AABBFF';
    case RaceRating.TiltRep: return '#FFAAAA';
    case RaceRating.LeanRep: return '#EE7777';
    case RaceRating.LikelyRep: return '#DD3333';
    case RaceRating.SolidRep: return '#BC0000';
    default: return '#CCCCCC';
  }
};

const getPartyColor = (party: Party): string => {
  switch (party) {
    case Party.Democrat: return '#0015BC';
    case Party.Republican: return '#BC0000';
    case Party.Independent: return '#808080';
    case Party.Libertarian: return '#FED105';
    case Party.Green: return '#17AA5C';
    default: return '#808080';
  }
};

const getRaceTypeLabel = (type: RaceType, districtNumber?: number): string => {
  switch (type) {
    case RaceType.Senate: return 'U.S. Senate';
    case RaceType.Governor: return 'Governor';
    case RaceType.House: return `U.S. House District ${districtNumber}`;
    default: return 'Unknown Race';
  }
};

export const RacePage = () => {
  const { raceId } = useParams<{ raceId: string }>();

  const { data: race, isLoading: raceLoading, error: raceError } = useQuery({
    queryKey: ['race', raceId],
    queryFn: () => racesApi.getById(raceId!),
    enabled: !!raceId,
  });

  const { data: forecast } = useQuery({
    queryKey: ['forecast', raceId],
    queryFn: () => forecastApi.getByRaceId(raceId!),
    enabled: !!raceId,
  });

  const { data: state } = useQuery({
    queryKey: ['state', race?.stateId],
    queryFn: () => statesApi.getById(race!.stateId),
    enabled: !!race?.stateId,
  });

  if (raceLoading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>Loading race data...</p>
      </div>
    );
  }

  if (raceError || !race) {
    return (
      <div className="error-container">
        <h2>Race not found</h2>
        <Link to="/">Back to Map</Link>
      </div>
    );
  }

  const demCandidate = race.candidates.find(c => c.party === Party.Democrat);
  const repCandidate = race.candidates.find(c => c.party === Party.Republican);
  const demForecast = race.forecasts.find(f => f.candidateId === demCandidate?.id);
  const repForecast = race.forecasts.find(f => f.candidateId === repCandidate?.id);

  const stateName = state?.name || race.stateId;
  const raceTypeLabel = getRaceTypeLabel(race.type, race.districtNumber);

  return (
    <div className="race-page" style={{ padding: '20px', maxWidth: '900px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>{stateName} {raceTypeLabel}</span>
      </nav>

      <header style={{ marginBottom: '32px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '8px' }}>
          <h1 style={{ margin: 0 }}>{stateName}</h1>
          <span
            style={{
              backgroundColor: getRatingColor(race.rating),
              color: 'white',
              padding: '8px 16px',
              borderRadius: '20px',
              fontWeight: 'bold',
              fontSize: '14px',
            }}
          >
            {getRatingLabel(race.rating)}
          </span>
        </div>
        <h2 style={{ margin: 0, color: '#666', fontWeight: 'normal' }}>
          {raceTypeLabel} {race.year}
          {race.isSpecialElection && <span style={{ marginLeft: '8px', color: '#dc2626' }}>(Special Election)</span>}
        </h2>
      </header>

      {/* Win Probability Section */}
      <div style={{
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
        marginBottom: '24px',
      }}>
        <h3 style={{ margin: '0 0 20px 0', textAlign: 'center' }}>Win Probability</h3>

        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {/* Democrat */}
          <div style={{ flex: 1, textAlign: 'center' }}>
            <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0015BC' }}>
              {demForecast ? `${(demForecast.winProbability * 100).toFixed(1)}%` : '-'}
            </div>
            <div style={{ fontSize: '14px', color: '#666' }}>
              {demCandidate?.name || 'Democrat'}
            </div>
            {demCandidate?.isIncumbent && (
              <div style={{ fontSize: '12px', color: '#999' }}>Incumbent</div>
            )}
          </div>

          {/* Bar */}
          <div style={{ flex: 2, height: '48px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
            <div style={{
              width: `${demForecast ? demForecast.winProbability * 100 : 50}%`,
              backgroundColor: '#0015BC',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              fontWeight: 'bold',
              transition: 'width 0.3s ease',
            }}>
              D
            </div>
            <div style={{
              width: `${repForecast ? repForecast.winProbability * 100 : 50}%`,
              backgroundColor: '#BC0000',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              fontWeight: 'bold',
              transition: 'width 0.3s ease',
            }}>
              R
            </div>
          </div>

          {/* Republican */}
          <div style={{ flex: 1, textAlign: 'center' }}>
            <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#BC0000' }}>
              {repForecast ? `${(repForecast.winProbability * 100).toFixed(1)}%` : '-'}
            </div>
            <div style={{ fontSize: '14px', color: '#666' }}>
              {repCandidate?.name || 'Republican'}
            </div>
            {repCandidate?.isIncumbent && (
              <div style={{ fontSize: '12px', color: '#999' }}>Incumbent</div>
            )}
          </div>
        </div>
      </div>

      {/* Forecast Inputs Section */}
      {forecast?.inputs && (
        <div style={{
          backgroundColor: 'white',
          borderRadius: '12px',
          padding: '24px',
          boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
          marginBottom: '24px',
        }}>
          <h3 style={{ margin: '0 0 20px 0' }}>Forecast Inputs</h3>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
            {forecast.inputs.marketOdds != null && (
              <InputCard
                label="Prediction Markets"
                value={`${(forecast.inputs.marketOdds * 100).toFixed(1)}%`}
                sublabel={forecast.inputs.marketLastUpdated ? `Updated: ${new Date(forecast.inputs.marketLastUpdated).toLocaleDateString()}` : undefined}
                color="#059669"
              />
            )}

            {forecast.inputs.pollingAverage != null && (
              <InputCard
                label="Polling Average"
                value={`${forecast.inputs.pollingAverage.toFixed(1)}%`}
                sublabel={forecast.inputs.pollCount ? `${forecast.inputs.pollCount} polls` : undefined}
                color="#2563eb"
              />
            )}

            {forecast.inputs.fundamentalsPrediction != null && (
              <InputCard
                label="Fundamentals"
                value={`${(forecast.inputs.fundamentalsPrediction * 100).toFixed(1)}%`}
                color="#7c3aed"
              />
            )}
          </div>

          <div style={{ marginTop: '16px', fontSize: '13px', color: '#666', textAlign: 'center' }}>
            Confidence: {(forecast.confidence * 100).toFixed(0)}% |
            Last Updated: {new Date(forecast.lastUpdated).toLocaleString()}
          </div>
        </div>
      )}

      {/* Candidates Section */}
      <div style={{
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
      }}>
        <h3 style={{ margin: '0 0 20px 0' }}>Candidates</h3>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {race.candidates.map(candidate => {
            const candidateForecast = race.forecasts.find(f => f.candidateId === candidate.id);
            return (
              <div
                key={candidate.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  padding: '16px',
                  backgroundColor: '#f9fafb',
                  borderRadius: '8px',
                  borderLeft: `4px solid ${getPartyColor(candidate.party)}`,
                }}
              >
                <div style={{
                  width: '48px',
                  height: '48px',
                  borderRadius: '50%',
                  backgroundColor: getPartyColor(candidate.party),
                  color: 'white',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontWeight: 'bold',
                  fontSize: '20px',
                  marginRight: '16px',
                }}>
                  {candidate.party === Party.Democrat ? 'D' : candidate.party === Party.Republican ? 'R' : 'I'}
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 'bold', fontSize: '18px' }}>
                    {candidate.name}
                    {candidate.isIncumbent && (
                      <span style={{ marginLeft: '8px', fontSize: '12px', color: '#666', fontWeight: 'normal' }}>
                        (Incumbent)
                      </span>
                    )}
                  </div>
                  <div style={{ color: '#666' }}>{candidate.party}</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                    {candidateForecast ? `${(candidateForecast.winProbability * 100).toFixed(1)}%` : '-'}
                  </div>
                  <div style={{ fontSize: '12px', color: '#666' }}>Win Probability</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

interface InputCardProps {
  label: string;
  value: string;
  sublabel?: string;
  color: string;
}

const InputCard = ({ label, value, sublabel, color }: InputCardProps) => (
  <div style={{
    padding: '16px',
    backgroundColor: '#f9fafb',
    borderRadius: '8px',
    borderTop: `3px solid ${color}`,
  }}>
    <div style={{ fontSize: '13px', color: '#666', marginBottom: '4px' }}>{label}</div>
    <div style={{ fontSize: '24px', fontWeight: 'bold', color }}>{value}</div>
    {sublabel && <div style={{ fontSize: '11px', color: '#999', marginTop: '4px' }}>{sublabel}</div>}
  </div>
);
