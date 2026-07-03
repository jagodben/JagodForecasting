import { useQuery } from '@tanstack/react-query';
import { Race, RaceType, RaceRating, Party } from '../../types';
import { forecastApi } from '../../services/api';

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
    case RaceRating.SolidDem: return '#0033AA';
    case RaceRating.LikelyDem: return '#2266DD';
    case RaceRating.LeanDem: return '#5599EE';
    case RaceRating.TiltDem: return '#99CCFF';
    case RaceRating.TiltRep: return '#FFCC99';
    case RaceRating.LeanRep: return '#E07070';
    case RaceRating.LikelyRep: return '#DD4422';
    case RaceRating.SolidRep: return '#AA0000';
    default: return '#E0E0E0';
  }
};

const getPartyColor = (party: Party): string => {
  switch (party) {
    case Party.Democrat: return '#0033AA';
    case Party.Republican: return '#AA0000';
    case Party.Independent: return '#808080';
    case Party.Libertarian: return '#FED105';
    case Party.Green: return '#17AA5C';
    default: return '#808080';
  }
};

// Formats a Dem-margin (points) as a called result, e.g. +5 -> "D+5", -3 -> "R+3".
const formatMargin = (margin: number): string => {
  const rounded = Math.round(margin);
  if (rounded === 0) return 'EVEN';
  return rounded > 0 ? `D+${rounded}` : `R+${Math.abs(rounded)}`;
};

const getRaceTypeLabel = (type: RaceType, districtNumber?: number): string => {
  switch (type) {
    case RaceType.Senate: return 'U.S. Senate';
    case RaceType.Governor: return 'Governor';
    case RaceType.House: return `U.S. House District ${districtNumber}`;
    default: return 'Unknown Race';
  }
};

interface RaceCardProps {
  race: Race;
  compact?: boolean;
}

export const RaceCard = ({ race, compact = false }: RaceCardProps) => {
  const demCandidate = race.candidates.find(c => c.party === Party.Democrat);
  const repCandidate = race.candidates.find(c => c.party === Party.Republican);
  const demForecast = race.forecasts.find(f => f.candidateId === demCandidate?.id);
  const repForecast = race.forecasts.find(f => f.candidateId === repCandidate?.id);

  // Use the blended forecast (markets + polling + fundamentals + approval) — the same
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
        boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
        marginBottom: '8px',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontWeight: 'bold' }}>
            {race.type === RaceType.House ? `District ${race.districtNumber}` : getRaceTypeLabel(race.type)}
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
      boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
      marginBottom: '16px',
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <h3 style={{ margin: 0 }}>{getRaceTypeLabel(race.type, race.districtNumber)}</h3>
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
            isIncumbent={demCandidate.isIncumbent}
            probability={demProbability}
          />
        )}
        {repCandidate && repProbability != null && (
          <CandidateRow
            name={repCandidate.name}
            party={repCandidate.party}
            isIncumbent={repCandidate.isIncumbent}
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
            color: detailed.expectedDemMargin > 0 ? '#0033AA' : detailed.expectedDemMargin < 0 ? '#AA0000' : '#666',
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
  isIncumbent: boolean;
  probability: number;
}

const CandidateRow = ({ name, party, isIncumbent, probability }: CandidateRowProps) => {
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
            {isIncumbent && <span style={{ color: '#666', marginLeft: '4px' }}>(i)</span>}
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
