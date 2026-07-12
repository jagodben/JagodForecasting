import { useQuery } from '@tanstack/react-query';
import { Race, RaceType, RaceRating, Party } from '../../types';
import { forecastApi } from '../../services/api';
import { districtCode } from '../../utils/districts';

const getRatingLabel = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return 'Solid D';
    case RaceRating.LikelyDem: return 'Likely D';
    case RaceRating.LeanDem: return 'Lean D';
    case RaceRating.TiltDem: return 'Tilt D';
    case RaceRating.TiltRep: return 'Tilt R';
    case RaceRating.LeanRep: return 'Lean R';
    case RaceRating.LikelyRep: return 'Likely R';
    case RaceRating.SolidRep: return 'Solid R';
    default: return 'Unknown';
  }
};

const getRatingColor = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return '#123f8f';
    case RaceRating.LikelyDem: return '#2e63bd';
    case RaceRating.LeanDem: return '#5a8fd6';
    case RaceRating.TiltDem: return '#9dbff0';
    case RaceRating.TiltRep: return '#f4aa9b';
    case RaceRating.LeanRep: return '#e2694f';
    case RaceRating.LikelyRep: return '#cf2f1a';
    case RaceRating.SolidRep: return '#9c150b';
    default: return '#E0E0E0';
  }
};

const getPartyColor = (party: Party): string => {
  switch (party) {
    case Party.Democrat: return '#123f8f';
    case Party.Republican: return '#9c150b';
    case Party.Independent: return '#eab308';
    case Party.Libertarian: return '#FED105';
    case Party.Green: return '#17AA5C';
    default: return '#808080';
  }
};

// Formats a Dem-margin (points) as a called result to one decimal (dropping a trailing .0),
// e.g. +5.3 -> "D+5.3", +5.0 -> "D+5", -3.2 -> "R+3.2".
const formatMargin = (margin: number): string => {
  const rounded = Math.round(margin * 10) / 10;
  if (rounded === 0) return 'EVEN';
  const abs = Math.abs(rounded);
  const num = Number.isInteger(abs) ? abs.toString() : abs.toFixed(1);
  return rounded > 0 ? `D+${num}` : `R+${num}`;
};

const getRaceTypeLabel = (type: RaceType, districtNumber?: number, stateId?: string): string => {
  switch (type) {
    case RaceType.Senate: return 'U.S. Senate';
    case RaceType.Governor: return 'Governor';
    case RaceType.House:
      return stateId
        ? `U.S. House ${districtCode(stateId, districtNumber)}`
        : `U.S. House District ${districtNumber}`;
    default: return 'Unknown Race';
  }
};

interface RaceCardProps {
  race: Race;
  compact?: boolean;
}

export const RaceCard = ({ race, compact = false }: RaceCardProps) => {
  // The Republican holds the R-side; the challenger is the other candidate — a Democrat, or a viable
  // independent (e.g. Dan Osborn) that replaced the token Democrat. It carries the Dem-side probability.
  const repCandidate = race.candidates.find(c => c.party === Party.Republican);
  const demCandidate = race.candidates.find(c => c.id !== repCandidate?.id);
  const demForecast = race.forecasts.find(f => f.candidateId === demCandidate?.id);
  const repForecast = race.forecasts.find(f => f.candidateId === repCandidate?.id);

  // Use the blended forecast (markets + polling + fundamentals + national environment) — the same
  // number the home page map and race page show — instead of the fundamentals-only
  // race.forecasts value. Shares a query cache key with RacePage/RaceMap.
  const { data: detailed } = useQuery({
    queryKey: ['forecast', race.id],
    queryFn: () => forecastApi.getByRaceId(race.id),
    enabled: !compact && !!race.id,
  });

  const demProbability = detailed?.demWinProbability ?? demForecast?.winProbability;
  const repProbability = detailed?.repWinProbability ?? repForecast?.winProbability;

  if (compact) {
    return (
      <div className="race-card compact" style={{
        padding: '12px',
        borderRadius: '8px',
        backgroundColor: '#fff',
        border: '1px solid #ececec',
        boxShadow: '0 1px 2px rgba(0,0,0,0.04)',
        marginBottom: '8px',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontWeight: 'bold' }}>
            {race.type === RaceType.House
              ? districtCode(race.stateId, race.districtNumber)
              : getRaceTypeLabel(race.type)}
          </span>
          <span style={{
            padding: '4px 8px',
            borderRadius: '4px',
            backgroundColor: getRatingColor(race.rating),
            color: 'white',
            fontSize: '12px',
          }}>
            {getRatingLabel(race.rating)}
          </span>
        </div>
      </div>
    );
  }

  return (
    <div className="race-card" style={{
      padding: '20px',
      borderRadius: '12px',
      backgroundColor: '#fff',
      border: '1px solid #ececec',
      boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
      marginBottom: '16px',
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <h3 style={{ margin: 0 }}>{getRaceTypeLabel(race.type, race.districtNumber, race.stateId)}</h3>
        <span style={{
          padding: '6px 12px',
          borderRadius: '20px',
          backgroundColor: getRatingColor(race.rating),
          color: 'white',
          fontWeight: 'bold',
        }}>
          {getRatingLabel(race.rating)}
        </span>
      </div>

      <div className="candidates" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
        {demCandidate && demProbability != null && (
          <CandidateRow
            name={demCandidate.name}
            party={demCandidate.party}
            probability={demProbability}
          />
        )}
        {repCandidate && repProbability != null && (
          <CandidateRow
            name={repCandidate.name}
            party={repCandidate.party}
            probability={repProbability}
          />
        )}
      </div>

      {detailed && (
        <div style={{
          marginTop: '16px',
          paddingTop: '12px',
          borderTop: '1px solid #eee',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          fontSize: '13px',
          color: '#666',
        }}>
          <span>Projected result</span>
          <span style={{
            fontWeight: 'bold',
            fontSize: '16px',
            color: detailed.expectedDemMargin > 0 ? '#123f8f' : detailed.expectedDemMargin < 0 ? '#9c150b' : '#666',
          }}>
            {formatMargin(detailed.expectedDemMargin)}
          </span>
        </div>
      )}
    </div>
  );
};

interface CandidateRowProps {
  name: string;
  party: Party;
  probability: number;
}

const CandidateRow = ({ name, party, probability }: CandidateRowProps) => {
  const partyLabel = party === Party.Democrat ? 'D' : party === Party.Republican ? 'R' : 'I';
  const partyColor = getPartyColor(party);
  const percentage = (probability * 100).toFixed(0);

  return (
    <div className="candidate-row">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{
            width: '24px',
            height: '24px',
            borderRadius: '50%',
            backgroundColor: partyColor,
            color: 'white',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: '12px',
            fontWeight: 'bold',
          }}>
            {partyLabel}
          </span>
          <span style={{ fontWeight: 500 }}>
            {name}
          </span>
        </div>
        <span style={{ fontWeight: 'bold', fontSize: '18px' }}>{percentage}%</span>
      </div>
      <div style={{
        height: '8px',
        backgroundColor: '#e0e0e0',
        borderRadius: '4px',
        overflow: 'hidden',
      }}>
        <div style={{
          height: '100%',
          width: `${percentage}%`,
          backgroundColor: partyColor,
          transition: 'width 0.3s ease',
        }} />
      </div>
    </div>
  );
};
