import { useState, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { racesApi, forecastApi, statesApi } from '../services/api';
import { RaceType, Party, RacePolls } from '../types';

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
      // Derive a win probability from the polling margin (mirrors the backend's ~3.5pt SE model)
      const avg = pollsData?.average;
      const margin = avg ? avg.demPercent - avg.repPercent : 0;
      demProbability = normalCdf(margin / 3.5);
    } else {
      // Combined or fallback
      demProbability = demForecastData?.winProbability ?? 0.5;
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
      <div style={{
        textAlign: 'center',
        fontSize: '13px',
        color: dataSource === 'markets' ? '#059669' : dataSource === 'polling' ? '#2563eb' : '#6b7280',
        marginBottom: '16px',
        fontWeight: 500,
      }}>
        {dataSource === 'markets' && 'Based on Polymarket prediction market odds'}
        {dataSource === 'polling' && 'Based on polling averages'}
        {dataSource === 'combined' && 'Combined forecast (markets + polling + fundamentals)'}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '48px' }}>
        {/* Democrat */}
        <div style={{ flex: 1, textAlign: 'center' }}>
          <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0044CC' }}>
            {(demProb * 100).toFixed(1)}%
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
          <div style={{ fontSize: '14px', color: '#666' }}>
            {repCandidate?.name || 'Republican'}
          </div>
          {repCandidate?.isIncumbent && (
            <div style={{ fontSize: '12px', color: '#999' }}>Incumbent</div>
          )}
        </div>
      </div>

      {/* Prediction Over Time Chart */}
      <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>Prediction Over Time</h3>
      <div style={{
        textAlign: 'center',
        fontSize: '13px',
        color: dataSource === 'markets' ? '#059669' : '#6b7280',
        marginBottom: '16px',
        fontWeight: 500,
      }}>
        {dataSource === 'markets' ? 'Polymarket odds history' : getSourceLabel(dataSource)}
      </div>
      <div style={{ marginBottom: '48px' }}>
        {dataSource === 'markets' && historicalData.length >= 2 ? (
          <PredictionChart
            data={historicalData}
            demName={demCandidate?.name || 'Democrat'}
            repName={repCandidate?.name || 'Republican'}
          />
        ) : (
          <div style={{ textAlign: 'center', padding: '40px', color: '#999', fontSize: '14px' }}>
            No historical data available
          </div>
        )}
      </div>

      {/* Polls Section */}
      {dataSource === 'polling' && (
        <PollsSection data={pollsData} demName={demCandidate?.name} repName={repCandidate?.name} />
      )}

      {/* Forecast Inputs Section */}
      {forecast?.inputs && (
        <div style={{ marginBottom: '48px' }}>
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
      <h3 style={{ margin: '0 0 20px 0' }}>Candidates</h3>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', paddingBottom: '32px' }}>
        {race.candidates.map(candidate => {
          const candidateForecast = race.forecasts.find(f => f.candidateId === candidate.id);
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

// SVG line chart component for prediction history
const PredictionChart = ({ data, demName, repName }: { data: HistoricalOdds[]; demName: string; repName: string }) => {
  const [hoveredPoint, setHoveredPoint] = useState<{ index: number; party: 'dem' | 'rep' } | null>(null);

  const width = 900;
  const height = 400;
  const padding = { top: 35, right: 65, bottom: 45, left: 55 };

  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const xScale = (index: number) => padding.left + (index / (data.length - 1)) * chartWidth;
  const yScale = (value: number) => padding.top + chartHeight - ((value / 100) * chartHeight);

  const demLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  const repLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(100 - d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  const dateIndices = [0, Math.floor(data.length / 4), Math.floor(data.length / 2), Math.floor(3 * data.length / 4), data.length - 1];
  const dateLabels = dateIndices.map(i => ({
    index: i,
    label: new Date(data[i].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
  }));

  const currentDemOdds = data[data.length - 1].demOdds;
  const currentRepOdds = Math.round((100 - currentDemOdds) * 10) / 10;

  const getTooltipData = () => {
    if (!hoveredPoint) return null;
    const d = data[hoveredPoint.index];
    const date = new Date(d.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    const odds = hoveredPoint.party === 'dem' ? d.demOdds : Math.round((100 - d.demOdds) * 10) / 10;
    const party = hoveredPoint.party === 'dem' ? demName : repName;
    const color = hoveredPoint.party === 'dem' ? '#0044CC' : '#CC0000';
    const x = xScale(hoveredPoint.index);
    const y = yScale(hoveredPoint.party === 'dem' ? d.demOdds : 100 - d.demOdds);
    return { date, odds, party, color, x, y };
  };

  const tooltipData = getTooltipData();

  return (
    <svg width="100%" viewBox={`0 0 ${width} ${height}`} style={{ maxWidth: '100%' }}>
      {/* Grid lines */}
      {[20, 40, 60, 80].map(v => (
        <g key={v}>
          <line
            x1={padding.left}
            y1={yScale(v)}
            x2={width - padding.right}
            y2={yScale(v)}
            stroke="#eee"
            strokeWidth="1"
          />
          <text
            x={padding.left - 10}
            y={yScale(v)}
            textAnchor="end"
            alignmentBaseline="middle"
            fontSize="12"
            fill="#999"
          >
            {v}%
          </text>
        </g>
      ))}

      {/* 50% line */}
      <line
        x1={padding.left}
        y1={yScale(50)}
        x2={width - padding.right}
        y2={yScale(50)}
        stroke="#666"
        strokeWidth="1.5"
        strokeDasharray="6,4"
      />
      <text
        x={width - padding.right + 8}
        y={yScale(50)}
        alignmentBaseline="middle"
        fontSize="11"
        fill="#666"
      >
        50%
      </text>

      {/* Democrat line */}
      <path
        d={demLinePath}
        fill="none"
        stroke="#0044CC"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`dem-${i}`}
          cx={xScale(i)}
          cy={yScale(d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'dem' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#0044CC"
          style={{ cursor: 'pointer' }}
          onMouseEnter={() => setHoveredPoint({ index: i, party: 'dem' })}
          onMouseLeave={() => setHoveredPoint(null)}
        />
      ))}

      {/* Republican line */}
      <path
        d={repLinePath}
        fill="none"
        stroke="#CC0000"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`rep-${i}`}
          cx={xScale(i)}
          cy={yScale(100 - d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'rep' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#CC0000"
          style={{ cursor: 'pointer' }}
          onMouseEnter={() => setHoveredPoint({ index: i, party: 'rep' })}
          onMouseLeave={() => setHoveredPoint(null)}
        />
      ))}

      {/* X-axis labels */}
      {dateLabels.map(({ index, label }) => (
        <text
          key={index}
          x={xScale(index)}
          y={height - 12}
          textAnchor="middle"
          fontSize="12"
          fill="#666"
        >
          {label}
        </text>
      ))}

      {/* Current value labels */}
      <text
        x={width - padding.right + 8}
        y={yScale(currentDemOdds)}
        alignmentBaseline="middle"
        fontSize="14"
        fontWeight="bold"
        fill="#0044CC"
      >
        {currentDemOdds}%
      </text>
      <text
        x={width - padding.right + 8}
        y={yScale(currentRepOdds)}
        alignmentBaseline="middle"
        fontSize="14"
        fontWeight="bold"
        fill="#CC0000"
      >
        {currentRepOdds}%
      </text>

      {/* Legend */}
      <g transform={`translate(${padding.left + 10}, ${padding.top - 18})`}>
        <circle cx="0" cy="0" r="6" fill="#0044CC" />
        <text x="12" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">{demName}</text>
        <circle cx="180" cy="0" r="6" fill="#CC0000" />
        <text x="192" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">{repName}</text>
      </g>

      {/* Tooltip */}
      {tooltipData && (
        <g transform={`translate(${tooltipData.x}, ${tooltipData.y - 12})`}>
          <rect
            x="-50"
            y="-38"
            width="100"
            height="36"
            rx="6"
            fill="white"
            stroke={tooltipData.color}
            strokeWidth="2"
            filter="drop-shadow(0 2px 4px rgba(0,0,0,0.2))"
          />
          <text
            x="0"
            y="-22"
            textAnchor="middle"
            fontSize="11"
            fill="#666"
          >
            {tooltipData.date}
          </text>
          <text
            x="0"
            y="-6"
            textAnchor="middle"
            fontSize="15"
            fontWeight="bold"
            fill="#333"
          >
            {tooltipData.odds}%
          </text>
        </g>
      )}
    </svg>
  );
};
