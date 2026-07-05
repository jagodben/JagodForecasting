import { useState, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { racesApi, forecastApi, statesApi } from '../services/api';
import { RaceType, Party, RacePolls } from '../types';
import { ProbabilityTrendChart } from '../components/charts/ProbabilityTrendChart';

type DataSource = 'combined' | 'markets' | 'polling';

interface HistoricalOdds {
  date: string;
  demOdds: number;
}

const getSourceLabel = (source: DataSource) => {
  switch (source) {
    case 'combined': return 'Forecast';
    case 'markets': return 'Polymarket';
    case 'polling': return 'Polls';
  }
};

const getPartyColor = (party: Party): string => {
  switch (party) {
    case Party.Democrat: return '#0044CC';
    case Party.Republican: return '#CC0000';
    case Party.Independent: return '#808080';
    case Party.Libertarian: return '#FED105';
    case Party.Green: return '#17AA5C';
    default: return '#808080';
  }
};

// Party logo (donkey/elephant) for the major parties; null for others (fall back to a letter badge).
const getPartyLogo = (party: Party): string | null => {
  if (party === Party.Democrat) return '/democrat.png';
  if (party === Party.Republican) return '/republican.png';
  return null;
};

// Standard normal CDF (Abramowitz & Stegun approximation) — matches the backend's
// polling-to-probability model so the "Polls" win probability is consistent.
const normalCdf = (x: number): number => {
  const a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
  const a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;
  const sign = x < 0 ? -1 : 1;
  const z = Math.abs(x) / Math.sqrt(2);
  const t = 1.0 / (1.0 + p * z);
  const y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.exp(-z * z);
  return 0.5 * (1.0 + sign * y);
};

const formatPollDate = (iso: string): string =>
  new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

// Formats a Dem-margin (points) as a called result to one decimal (dropping a trailing .0),
// e.g. +5.3 -> "D+5.3", +5.0 -> "D+5", -3.2 -> "R+3.2".
const formatMargin = (margin: number): string => {
  const rounded = Math.round(margin * 10) / 10;
  if (rounded === 0) return 'EVEN';
  const abs = Math.abs(rounded);
  const num = Number.isInteger(abs) ? abs.toString() : abs.toFixed(1);
  return rounded > 0 ? `D+${num}` : `R+${num}`;
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
  const [dataSource, setDataSource] = useState<DataSource>('combined');

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

  const { data: pollsData } = useQuery({
    queryKey: ['polls', raceId],
    queryFn: () => forecastApi.getPolls(raceId!),
    enabled: !!raceId,
  });

  // Calculate probabilities based on selected data source
  const { demProb, repProb, historicalData, hasMarketData, hasPollingData } = useMemo(() => {
    if (!race) return { demProb: 0.5, repProb: 0.5, historicalData: [], hasMarketData: false, hasPollingData: false };

    const demCandidate = race.candidates.find(c => c.party === Party.Democrat);
    const demForecastData = race.forecasts.find(f => f.candidateId === demCandidate?.id);

    // Check data availability from forecast inputs
    const marketAvailable = forecast?.inputs?.marketOdds != null;
    const pollingAvailable = (forecast?.inputs?.pollingAverage != null) || ((pollsData?.polls?.length ?? 0) > 0);

    let demProbability: number;

    if (dataSource === 'markets' && marketAvailable) {
      demProbability = forecast!.inputs.marketOdds!;
    } else if (dataSource === 'polling' && pollingAvailable) {
      // Use the backend's polling win probability (the same value the map shows). Fall back
      // to computing it from the polls margin if the blended forecast hasn't loaded yet.
      const avg = pollsData?.average;
      demProbability = forecast?.inputs?.pollingWinProbability
        ?? (avg ? normalCdf((avg.demPercent - avg.repPercent) / 3.5) : 0.5);
    } else {
      // Combined: use the blended forecast (markets + polling + fundamentals + approval),
      // the same value the home page map shows. Fall back to the fundamentals-only
      // race forecast only if the blended forecast hasn't loaded.
      demProbability = forecast?.demWinProbability ?? demForecastData?.winProbability ?? 0.5;
    }

    // Use real history from API, filtered from Nov 3 2025 onwards
    const startDate = new Date('2025-11-03');
    const historical: HistoricalOdds[] = (forecast?.history ?? [])
      .filter(h => new Date(h.date) >= startDate)
      .map(h => ({
        date: h.date.split('T')[0],
        demOdds: Math.round(h.demWinProbability * 1000) / 10,
      }));

    return {
      demProb: demProbability,
      repProb: 1 - demProbability,
      historicalData: historical,
      hasMarketData: marketAvailable,
      hasPollingData: pollingAvailable,
    };
  }, [race, forecast, dataSource, pollsData]);

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
        <p>jagodforecasting.com is currently undergoing maintenance, please try again later</p>
      </div>
    );
  }

  const demCandidate = race.candidates.find(c => c.party === Party.Democrat);
  const repCandidate = race.candidates.find(c => c.party === Party.Republican);

  const stateName = state?.name || race.stateId;
  const raceTypeLabel = getRaceTypeLabel(race.type, race.districtNumber);

  return (
    <div className="race-page" style={{ backgroundColor: 'white', minHeight: '100vh', padding: '20px', maxWidth: '900px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>{stateName} {raceTypeLabel}</span>
      </nav>

      <header style={{ marginBottom: '32px' }}>
        <div style={{ marginBottom: '8px' }}>
          <h1 style={{ margin: 0 }}>{stateName}</h1>
        </div>
        <h2 style={{ margin: 0, color: '#666', fontWeight: 'normal' }}>
          {raceTypeLabel} {race.year}
          {race.isSpecialElection && <span style={{ marginLeft: '8px', color: '#dc2626' }}>(Special Election)</span>}
        </h2>
      </header>

      {/* Data Source Toggle */}
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        gap: '8px',
        marginBottom: '32px',
      }}>
        {(['combined', 'markets', 'polling'] as DataSource[]).map((source) => {
          const isDisabled =
            (source === 'markets' && !hasMarketData) ||
            (source === 'polling' && !hasPollingData);

          return (
            <button
              key={source}
              onClick={() => !isDisabled && setDataSource(source)}
              disabled={isDisabled}
              style={{
                padding: '10px 20px',
                fontSize: '14px',
                fontWeight: dataSource === source ? 'bold' : 'normal',
                backgroundColor: dataSource === source ? '#6366f1' : isDisabled ? '#e5e7eb' : '#f3f4f6',
                color: dataSource === source ? 'white' : isDisabled ? '#9ca3af' : '#374151',
                border: 'none',
                borderRadius: '8px',
                cursor: isDisabled ? 'not-allowed' : 'pointer',
                transition: 'all 0.2s ease',
                opacity: isDisabled ? 0.6 : 1,
              }}
              title={isDisabled ? `No ${source === 'markets' ? 'market' : 'polling'} data available` : ''}
            >
              {getSourceLabel(source)}
            </button>
          );
        })}
      </div>

      {/* Win Probability Section */}
      <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>Win Probability</h3>
      {dataSource !== 'combined' && (
        <div style={{
          textAlign: 'center',
          fontSize: '13px',
          color: dataSource === 'markets' ? '#059669' : '#2563eb',
          marginBottom: '16px',
          fontWeight: 500,
        }}>
          {dataSource === 'markets' && 'Based on Polymarket prediction market odds'}
          {dataSource === 'polling' && 'Based on polling averages'}
        </div>
      )}

      <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '48px' }}>
        {/* Democrat */}
        <div style={{ flex: 1, textAlign: 'center' }}>
          <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0044CC' }}>
            {(demProb * 100).toFixed(1)}%
          </div>
          {demCandidate?.isIncumbent && (
            <div style={{ fontSize: '12px', color: '#999' }}>Incumbent</div>
          )}
        </div>

        {/* Bar */}
        <div style={{ flex: 2, height: '48px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
          <div style={{
            width: `${demProb * 100}%`,
            backgroundColor: '#0044CC',
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
            width: `${repProb * 100}%`,
            backgroundColor: '#CC0000',
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
          <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#CC0000' }}>
            {(repProb * 100).toFixed(1)}%
          </div>
          {repCandidate?.isIncumbent && (
            <div style={{ fontSize: '12px', color: '#999' }}>Incumbent</div>
          )}
        </div>
      </div>

      {/* Projected result (the forecast's expected margin) — Forecast view only */}
      {forecast && dataSource === 'combined' && (
        <div style={{ textAlign: 'center', marginTop: '-32px', marginBottom: '48px' }}>
          <div style={{ fontSize: '13px', color: '#666', marginBottom: '4px' }}>Projected result</div>
          <div style={{
            fontSize: '26px',
            fontWeight: 'bold',
            color: forecast.expectedDemMargin > 0 ? '#0044CC' : forecast.expectedDemMargin < 0 ? '#CC0000' : '#666',
          }}>
            {formatMargin(forecast.expectedDemMargin)}
          </div>
        </div>
      )}

      {/* Prediction Over Time — the model's daily forecast (combined view) */}
      {dataSource === 'combined' && historicalData.length >= 2 && (
        <>
          <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>Prediction Over Time</h3>
          <div style={{
            textAlign: 'center',
            fontSize: '13px',
            color: '#6b7280',
            marginBottom: '16px',
            fontWeight: 500,
          }}>
            Model forecast history
          </div>
          <div style={{ marginBottom: '48px' }}>
            <ProbabilityTrendChart
              data={historicalData.map(h => ({ date: h.date, demValue: h.demOdds / 100 }))}
              demLabel={demCandidate?.name || 'Democrat'}
              repLabel={repCandidate?.name || 'Republican'}
              width={760}
              height={300}
            />
          </div>
        </>
      )}

      {/* Polls Section */}
      {dataSource === 'polling' && (
        <PollsSection data={pollsData} demName={demCandidate?.name} repName={repCandidate?.name} />
      )}

      {/* Candidates Section */}
      <h3 style={{ margin: '0 0 20px 0' }}>Candidates</h3>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', paddingBottom: '32px' }}>
        {race.candidates.map(candidate => {
          const candidateForecast = race.forecasts.find(f => f.candidateId === candidate.id);
          // Show the same probability as the headline for the two major-party candidates
          // (reflects the selected data source); fall back to the per-candidate forecast for others.
          const displayProbability =
            candidate.party === Party.Democrat ? demProb :
            candidate.party === Party.Republican ? repProb :
            candidateForecast?.winProbability;
          const partyLogo = getPartyLogo(candidate.party);
          return (
            <div
              key={candidate.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                padding: '16px 0',
                borderBottom: '1px solid #eee',
              }}
            >
              {partyLogo ? (
                <img
                  src={partyLogo}
                  alt={candidate.party}
                  style={{
                    width: '48px',
                    height: '48px',
                    objectFit: 'contain',
                    marginRight: '16px',
                    flexShrink: 0,
                  }}
                />
              ) : (
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
                  flexShrink: 0,
                }}>
                  I
                </div>
              )}
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
                  {displayProbability != null ? `${(displayProbability * 100).toFixed(1)}%` : '-'}
                </div>
                <div style={{ fontSize: '12px', color: '#666' }}>Win Probability</div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

// Polls list + weighted average shown when the "Polls" data source is selected
const PollsSection = ({ data, demName, repName }: { data?: RacePolls; demName?: string; repName?: string }) => {
  if (!data || data.polls.length === 0) {
    return (
      <div style={{ marginBottom: '48px' }}>
        <h3 style={{ margin: '0 0 8px 0' }}>Polls</h3>
        <div style={{ textAlign: 'center', padding: '32px', color: '#999', fontSize: '14px' }}>
          No polling data available for this race yet.
        </div>
      </div>
    );
  }

  const avg = data.average;
  const demLead = avg ? avg.margin >= 0 : true;

  return (
    <div style={{ marginBottom: '48px' }}>
      <h3 style={{ margin: '0 0 4px 0' }}>Polls</h3>
      <div style={{ fontSize: '13px', color: '#666', marginBottom: '20px' }}>
        Weighted average of {data.polls.length} poll{data.polls.length === 1 ? '' : 's'} · recency &amp; sample-size weighted · source: Wikipedia
      </div>

      {/* Weighted average summary */}
      {avg && (
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-around',
          padding: '16px', backgroundColor: '#f9fafb', borderRadius: '8px', marginBottom: '20px',
        }}>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: '28px', fontWeight: 'bold', color: '#0044CC' }}>{avg.demPercent.toFixed(1)}%</div>
            <div style={{ fontSize: '12px', color: '#666' }}>{demName || 'Democrat'}</div>
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: '16px', fontWeight: 600, color: demLead ? '#0044CC' : '#CC0000' }}>
              {demLead ? 'D' : 'R'} +{Math.abs(avg.margin).toFixed(1)}
            </div>
            <div style={{ fontSize: '11px', color: '#999' }}>avg. margin</div>
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: '28px', fontWeight: 'bold', color: '#CC0000' }}>{avg.repPercent.toFixed(1)}%</div>
            <div style={{ fontSize: '12px', color: '#666' }}>{repName || 'Republican'}</div>
          </div>
        </div>
      )}

      {/* Individual polls */}
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid #eee', textAlign: 'left', color: '#666' }}>
              <th style={{ padding: '8px 12px 8px 0' }}>Pollster</th>
              <th style={{ padding: '8px 12px' }}>Date</th>
              <th style={{ padding: '8px 12px', textAlign: 'right' }}>Sample</th>
              <th style={{ padding: '8px 12px', textAlign: 'right', color: '#0044CC' }}>D</th>
              <th style={{ padding: '8px 12px', textAlign: 'right', color: '#CC0000' }}>R</th>
              <th style={{ padding: '8px 0 8px 12px', textAlign: 'right' }}>Margin</th>
            </tr>
          </thead>
          <tbody>
            {data.polls.map((poll, i) => {
              const leadD = poll.margin >= 0;
              return (
                <tr key={i} style={{ borderBottom: '1px solid #f0f0f0' }}>
                  <td style={{ padding: '10px 12px 10px 0' }}>
                    {poll.pollster}
                    {poll.isPartisan && (
                      <span style={{ marginLeft: '6px', fontSize: '11px', color: '#b45309', backgroundColor: '#fef3c7', padding: '1px 6px', borderRadius: '4px' }}>
                        partisan
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '10px 12px', color: '#666' }}>{formatPollDate(poll.date)}</td>
                  <td style={{ padding: '10px 12px', textAlign: 'right', color: '#666' }}>
                    {poll.sampleSize ? poll.sampleSize.toLocaleString() : '—'}{poll.population ? ` ${poll.population}` : ''}
                  </td>
                  <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: leadD ? 'bold' : 'normal' }}>{poll.demPercent.toFixed(0)}%</td>
                  <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: !leadD ? 'bold' : 'normal' }}>{poll.repPercent.toFixed(0)}%</td>
                  <td style={{ padding: '10px 0 10px 12px', textAlign: 'right', color: leadD ? '#0044CC' : '#CC0000', fontWeight: 600 }}>
                    {leadD ? 'D' : 'R'} +{Math.abs(poll.margin).toFixed(0)}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};
