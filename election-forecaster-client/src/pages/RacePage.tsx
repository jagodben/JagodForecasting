import { useState, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { racesApi, forecastApi, statesApi } from '../services/api';
import { RaceType, Party, RacePolls, Race, Candidate, DetailedForecast } from '../types';
import { ProbabilityTrendChart } from '../components/charts/ProbabilityTrendChart';
import { timeAgo } from '../utils/time';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { useIsDesktop } from '../utils/useMediaQuery';

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
  const isDesktop = useIsDesktop();

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
  const { demProb, historicalData, hasMarketData, hasPollingData } = useMemo(() => {
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

  const pageTitle = race
    ? `${state?.name || race.stateId} ${getRaceTypeLabel(race.type, race.districtNumber)} 2026`
    : null;
  useDocumentTitle(pageTitle);

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

  // On desktop the toggle is dropped and everything is shown at once, so the headline always uses
  // the combined forecast; on mobile it follows the selected data source.
  const headDem = isDesktop ? (forecast?.demWinProbability ?? demProb) : demProb;
  const headRep = 1 - headDem;

  return (
    <div className="race-page">
      <nav className="breadcrumb" style={{ marginBottom: '12px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>{stateName} {raceTypeLabel}</span>
      </nav>

      <div className="race-page__header-top">
        <div>
          <h1 style={{ margin: 0 }}>{stateName}</h1>
          <h2 style={{ margin: '4px 0 0', color: '#666', fontWeight: 'normal', fontSize: '1.15rem' }}>
            {raceTypeLabel} {race.year}
            {race.isSpecialElection && <span style={{ marginLeft: '8px', color: '#dc2626' }}>(Special Election)</span>}
          </h2>
          {forecast && timeAgo(forecast.lastUpdated) && (
            <div style={{ fontSize: '12px', color: '#9ca3af', marginTop: '6px' }}>
              Forecast updated {timeAgo(forecast.lastUpdated)}
            </div>
          )}
        </div>

        {/* Data Source Toggle — mobile only; desktop shows every lens at once */}
        {!isDesktop && (
        <div className="race-page__sources">
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
                  padding: '9px 18px',
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
        )}
      </div>

      <div className="race-page__body">
        {isDesktop ? (
          <>
            {/* Left column: Model chart (top) + projected result (bottom) */}
            <div className="race-page__col">
              {historicalData.length >= 2 && (
                <div style={{ width: '100%' }}>
                  <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: '12px' }}>Model Forecast</div>
                  <ProbabilityTrendChart
                    data={historicalData.map(h => ({ date: h.date, demValue: h.demOdds / 100 }))}
                    demLabel={demCandidate?.name || 'Democrat'}
                    repLabel={repCandidate?.name || 'Republican'}
                    width={520}
                    height={300}
                  />
                </div>
              )}

              {forecast && (
                <div>
                  <div style={{ fontSize: '13px', color: '#666', marginBottom: '4px' }}>Projected result</div>
                  <div style={{
                    fontSize: '30px',
                    fontWeight: 'bold',
                    color: forecast.expectedDemMargin > 0 ? '#0044CC' : forecast.expectedDemMargin < 0 ? '#CC0000' : '#666',
                  }}>
                    {formatMargin(forecast.expectedDemMargin)}
                  </div>
                </div>
              )}
            </div>

            {/* Right column: candidates (top) + polls (bottom) */}
            <div className="race-page__col">
              <CandidatesList race={race} />
              <PollsSection data={pollsData} demName={demCandidate?.name} repName={repCandidate?.name} />
            </div>
          </>
        ) : (
          <>
            {/* Mobile: win-probability headline + candidates, then the toggle-selected view */}
            <div className="race-page__left">
              <WinProbHeadline
                headDem={headDem}
                headRep={headRep}
                demCandidate={demCandidate}
                repCandidate={repCandidate}
                dataSource={dataSource}
                forecast={forecast}
              />
              <CandidatesList race={race} />
            </div>

            <div className="race-page__right">
              {dataSource === 'combined' && historicalData.length >= 2 && (
                <div style={{ width: '100%' }}>
                  <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: '12px' }}>Model Forecast</div>
                  <ProbabilityTrendChart
                    data={historicalData.map(h => ({ date: h.date, demValue: h.demOdds / 100 }))}
                    demLabel={demCandidate?.name || 'Democrat'}
                    repLabel={repCandidate?.name || 'Republican'}
                    width={760}
                    height={300}
                  />
                </div>
              )}

              {dataSource === 'polling' && (
                <PollsSection data={pollsData} demName={demCandidate?.name} repName={repCandidate?.name} />
              )}

              {dataSource === 'markets' && (
                <div style={{ textAlign: 'center', color: '#9ca3af', fontSize: '14px', padding: '24px' }}>
                  Showing the Polymarket prediction-market win probability.
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
};

// Win-probability headline (big Dem/Rep % + bar + projected margin). Mobile only — on desktop the
// candidates list already shows each side's win probability, so the headline is redundant.
const WinProbHeadline = ({ headDem, headRep, demCandidate, repCandidate, dataSource, forecast }: {
  headDem: number;
  headRep: number;
  demCandidate?: Candidate;
  repCandidate?: Candidate;
  dataSource: DataSource;
  forecast?: DetailedForecast;
}) => (
  <div>
    <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>Win Probability</h3>
    {dataSource !== 'combined' && (
      <div style={{ textAlign: 'center', fontSize: '13px', color: dataSource === 'markets' ? '#059669' : '#2563eb', marginBottom: '12px', fontWeight: 500 }}>
        {dataSource === 'markets' && 'Based on Polymarket prediction market odds'}
        {dataSource === 'polling' && 'Based on polling averages'}
      </div>
    )}
    <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
      <div style={{ flex: 1, textAlign: 'center' }}>
        <div style={{ fontSize: '38px', fontWeight: 'bold', color: '#0044CC' }}>{(headDem * 100).toFixed(1)}%</div>
        {demCandidate?.isIncumbent && <div style={{ fontSize: '12px', color: '#999' }}>(i)</div>}
      </div>
      <div style={{ flex: 2, height: '44px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
        <div style={{ width: `${headDem * 100}%`, backgroundColor: '#0044CC', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontWeight: 'bold', transition: 'width 0.3s ease' }}>D</div>
        <div style={{ width: `${headRep * 100}%`, backgroundColor: '#CC0000', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontWeight: 'bold', transition: 'width 0.3s ease' }}>R</div>
      </div>
      <div style={{ flex: 1, textAlign: 'center' }}>
        <div style={{ fontSize: '38px', fontWeight: 'bold', color: '#CC0000' }}>{(headRep * 100).toFixed(1)}%</div>
        {repCandidate?.isIncumbent && <div style={{ fontSize: '12px', color: '#999' }}>(i)</div>}
      </div>
    </div>
    {forecast && dataSource === 'combined' && (
      <div style={{ textAlign: 'center', marginTop: '16px' }}>
        <div style={{ fontSize: '13px', color: '#666', marginBottom: '4px' }}>Projected result</div>
        <div style={{ fontSize: '24px', fontWeight: 'bold', color: forecast.expectedDemMargin > 0 ? '#0044CC' : forecast.expectedDemMargin < 0 ? '#CC0000' : '#666' }}>
          {formatMargin(forecast.expectedDemMargin)}
        </div>
      </div>
    )}
  </div>
);

// Candidate list — name, party, incumbency. Win probability is omitted (it's already shown by the
// forecast chart / headline).
const CandidatesList = ({ race }: { race: Race }) => (
  <div>
    <h3 style={{ margin: '0 0 12px 0' }}>Candidates</h3>
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      {race.candidates.map(candidate => {
        const partyLogo = getPartyLogo(candidate.party);
        return (
          <div key={candidate.id} style={{ display: 'flex', alignItems: 'center', padding: '12px 0', borderBottom: '1px solid #eee' }}>
            {partyLogo ? (
              <img src={partyLogo} alt={candidate.party} style={{ width: '42px', height: '42px', objectFit: 'contain', marginRight: '14px', flexShrink: 0 }} />
            ) : (
              <div style={{ width: '42px', height: '42px', borderRadius: '50%', backgroundColor: getPartyColor(candidate.party), color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', fontSize: '18px', marginRight: '14px', flexShrink: 0 }}>I</div>
            )}
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 'bold', fontSize: '17px' }}>
                {candidate.name}
                {candidate.isIncumbent && <span style={{ marginLeft: '8px', fontSize: '12px', color: '#666', fontWeight: 'normal' }}>(i)</span>}
              </div>
              <div style={{ color: '#666', fontSize: '14px' }}>{candidate.party}</div>
            </div>
          </div>
        );
      })}
    </div>
  </div>
);

// Polls list + weighted average. `maxRows` caps the table (desktop, to keep everything on one
// screen) and shows a "+N more" note.
const PollsSection = ({ data, demName, repName, maxRows }: { data?: RacePolls; demName?: string; repName?: string; maxRows?: number }) => {
  if (!data || data.polls.length === 0) {
    return (
      <div>
        <h3 style={{ margin: '0 0 8px 0' }}>Polls</h3>
        <div style={{ textAlign: 'center', padding: '24px', color: '#999', fontSize: '14px' }}>
          No polling data available for this race yet.
        </div>
      </div>
    );
  }

  const avg = data.average;
  const demLead = avg ? avg.margin >= 0 : true;
  const shownPolls = maxRows ? data.polls.slice(0, maxRows) : data.polls;
  const hiddenCount = data.polls.length - shownPolls.length;

  return (
    <div style={{ width: '100%' }}>
      <h3 style={{ margin: '0 0 4px 0' }}>Polls</h3>
      <div style={{ fontSize: '13px', color: '#666', marginBottom: '16px' }}>
        Weighted average of {data.polls.length} poll{data.polls.length === 1 ? '' : 's'} · recency &amp; sample-size weighted
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
            {shownPolls.map((poll, i) => {
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
      {hiddenCount > 0 && (
        <div style={{ fontSize: '12px', color: '#9ca3af', marginTop: '8px', textAlign: 'center' }}>
          + {hiddenCount} more poll{hiddenCount === 1 ? '' : 's'}
        </div>
      )}
    </div>
  );
};
