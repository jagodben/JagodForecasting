import { useMemo, useState } from 'react';
import { Race, RaceType } from '../../types';

interface ChamberForecastProps {
  races: Race[];
  raceType: RaceType.Senate | RaceType.House;
}

interface SeatProjection {
  democrat: number;
  republican: number;
  independent: number;
  tossup: number;
}

interface HistoricalOdds {
  date: string;
  demOdds: number;
}

// Mock historical data - in a real app this would come from the API
const generateMockHistoricalData = (currentDemOdds: number, raceType: RaceType): HistoricalOdds[] => {
  const data: HistoricalOdds[] = [];
  const today = new Date();

  // Generate daily data for the past 60 days
  const baseOdds = raceType === RaceType.Senate ? 48 : 44;
  let runningOdds = baseOdds;

  for (let i = 60; i >= 0; i--) {
    const date = new Date(today);
    date.setDate(date.getDate() - i);

    // Simulate daily model recalculation with small random walk + trend toward current
    const randomShift = (Math.random() - 0.5) * 3; // -1.5 to +1.5 daily shift
    const trendPull = (currentDemOdds - runningOdds) * 0.05; // Gentle pull toward final value

    runningOdds = runningOdds + randomShift + trendPull;
    runningOdds = Math.max(20, Math.min(80, runningOdds));

    data.push({
      date: date.toISOString().split('T')[0],
      demOdds: Math.round(runningOdds * 10) / 10,
    });
  }

  // Ensure the last point matches current odds exactly
  if (data.length > 0) {
    data[data.length - 1].demOdds = currentDemOdds;
  }

  return data;
};

export const ChamberForecast = ({ races, raceType }: ChamberForecastProps) => {
  const { seatProjection, demVictoryOdds, historicalData } = useMemo(() => {
    // Calculate seat projections based on race forecasts
    const projection: SeatProjection = {
      democrat: 0,
      republican: 0,
      independent: 0,
      tossup: 0,
    };

    races.forEach(race => {
      // Find the candidate with highest win probability
      const sortedForecasts = [...race.forecasts].sort((a, b) => b.winProbability - a.winProbability);
      const topForecast = sortedForecasts[0];
      const topCandidate = race.candidates.find(c => c.id === topForecast?.candidateId);

      if (!topForecast || !topCandidate) return;

      // If it's close (within 10%), count as tossup for display
      const margin = topForecast.winProbability - (sortedForecasts[1]?.winProbability || 0);

      if (margin < 0.1) {
        projection.tossup++;
      } else if (topCandidate.party === 'Democrat') {
        projection.democrat++;
      } else if (topCandidate.party === 'Republican') {
        projection.republican++;
      } else {
        projection.independent++;
      }
    });

    // Calculate overall victory odds
    // For Senate: Dems need 50 seats (or 51 without VP tiebreaker)
    // For House: Need 218 seats for majority
    // This is a simplified calculation - real models use Monte Carlo simulations

    let demOdds: number;

    if (raceType === RaceType.Senate) {
      // Assume 34 seats up for election, Dems currently have some safe seats
      // Simplified: base it on projected seats vs needed
      const demTotal = projection.democrat + 0.5 * projection.tossup;
      const seatsNeeded = races.length / 2;
      const advantage = (demTotal - seatsNeeded) / races.length;
      demOdds = Math.round((50 + advantage * 100) * 10) / 10;
    } else {
      // House: 435 seats, need 218 for majority
      const demTotal = projection.democrat + 0.5 * projection.tossup;
      const advantage = (demTotal - 218) / 50; // Normalize
      demOdds = Math.round((50 + advantage * 30) * 10) / 10;
    }

    demOdds = Math.max(5, Math.min(95, demOdds));

    const historical = generateMockHistoricalData(demOdds, raceType);

    return {
      seatProjection: projection,
      demVictoryOdds: demOdds,
      historicalData: historical,
    };
  }, [races, raceType]);

  const repVictoryOdds = Math.round((100 - demVictoryOdds) * 10) / 10;
  const chamberName = raceType === RaceType.Senate ? 'Senate' : 'House';
  const totalSeats = raceType === RaceType.Senate ? 100 : 435;
  const majorityNeeded = raceType === RaceType.Senate ? 50 : 218;

  // For seats not up for election (simplified assumption)
  const seatsNotUp = totalSeats - races.length;
  const assumedDemHeld = Math.round(seatsNotUp * 0.48);
  const assumedRepHeld = seatsNotUp - assumedDemHeld;

  const totalDemSeats = seatProjection.democrat + assumedDemHeld;
  const totalRepSeats = seatProjection.republican + assumedRepHeld;
  const totalIndSeats = seatProjection.independent;

  return (
    <div style={{ marginTop: '24px', width: '100%', maxWidth: '1000px', margin: '24px auto 0' }}>
      {/* Victory Odds Box */}
      <div style={{
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
        marginBottom: '16px',
      }}>
        <h3 style={{ margin: '0 0 20px 0', textAlign: 'center' }}>
          {chamberName} Forecast - Chance of Winning Majority
        </h3>

        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {/* Democrat odds */}
          <div style={{ flex: 1, textAlign: 'center' }}>
            <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0015BC' }}>
              {demVictoryOdds}%
            </div>
            <div style={{ fontSize: '16px', color: '#666' }}>Democrats</div>
          </div>

          {/* Visual bar */}
          <div style={{ flex: 2, height: '48px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
            <div style={{
              width: `${demVictoryOdds}%`,
              backgroundColor: '#0015BC',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              fontWeight: 'bold',
              fontSize: '16px',
              transition: 'width 0.3s ease',
            }}>
              {demVictoryOdds > 20 && 'D'}
            </div>
            <div style={{
              width: `${repVictoryOdds}%`,
              backgroundColor: '#BC0000',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              fontWeight: 'bold',
              fontSize: '16px',
              transition: 'width 0.3s ease',
            }}>
              {repVictoryOdds > 20 && 'R'}
            </div>
          </div>

          {/* Republican odds */}
          <div style={{ flex: 1, textAlign: 'center' }}>
            <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#BC0000' }}>
              {repVictoryOdds}%
            </div>
            <div style={{ fontSize: '16px', color: '#666' }}>Republicans</div>
          </div>
        </div>
      </div>

      {/* Seat Projections Box */}
      <div style={{
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
        marginBottom: '16px',
      }}>
        <h3 style={{ margin: '0 0 20px 0', textAlign: 'center' }}>
          Projected Seats ({majorityNeeded} needed for majority)
        </h3>

        <div style={{ display: 'flex', justifyContent: 'center', gap: '48px', flexWrap: 'wrap' }}>
          <div style={{ textAlign: 'center', minWidth: '120px' }}>
            <div style={{
              fontSize: '40px',
              fontWeight: 'bold',
              color: '#0015BC',
              backgroundColor: 'rgba(0, 21, 188, 0.1)',
              borderRadius: '12px',
              padding: '16px 28px',
            }}>
              {totalDemSeats}
            </div>
            <div style={{ fontSize: '16px', color: '#666', marginTop: '10px' }}>Democrats</div>
          </div>

          {totalIndSeats > 0 && (
            <div style={{ textAlign: 'center', minWidth: '120px' }}>
              <div style={{
                fontSize: '40px',
                fontWeight: 'bold',
                color: '#666',
                backgroundColor: 'rgba(102, 102, 102, 0.1)',
                borderRadius: '12px',
                padding: '16px 28px',
              }}>
                {totalIndSeats}
              </div>
              <div style={{ fontSize: '16px', color: '#666', marginTop: '10px' }}>Independent</div>
            </div>
          )}

          <div style={{ textAlign: 'center', minWidth: '120px' }}>
            <div style={{
              fontSize: '40px',
              fontWeight: 'bold',
              color: '#BC0000',
              backgroundColor: 'rgba(188, 0, 0, 0.1)',
              borderRadius: '12px',
              padding: '16px 28px',
            }}>
              {totalRepSeats}
            </div>
            <div style={{ fontSize: '16px', color: '#666', marginTop: '10px' }}>Republicans</div>
          </div>
        </div>

        {/* Seat bar visualization */}
        <div style={{
          marginTop: '24px',
          height: '32px',
          display: 'flex',
          borderRadius: '6px',
          overflow: 'hidden',
          position: 'relative',
        }}>
          <div style={{ width: `${(totalDemSeats / totalSeats) * 100}%`, backgroundColor: '#0015BC' }} />
          {totalIndSeats > 0 && (
            <div style={{ width: `${(totalIndSeats / totalSeats) * 100}%`, backgroundColor: '#666' }} />
          )}
          <div style={{ width: `${(totalRepSeats / totalSeats) * 100}%`, backgroundColor: '#BC0000' }} />
          {/* Majority line */}
          <div style={{
            position: 'absolute',
            left: `${(majorityNeeded / totalSeats) * 100}%`,
            top: 0,
            bottom: 0,
            width: '3px',
            backgroundColor: '#333',
          }} />
        </div>
        <div style={{
          textAlign: 'center',
          fontSize: '13px',
          color: '#666',
          marginTop: '8px',
        }}>
          {majorityNeeded} seats needed for majority
        </div>
      </div>

      {/* Historical Trend Chart Box */}
      <div style={{
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
      }}>
        <h3 style={{ margin: '0 0 20px 0', textAlign: 'center' }}>
          Win Probability Over Time
        </h3>

        <OddsChart data={historicalData} />
      </div>
    </div>
  );
};

// SVG line chart component with both party lines
const OddsChart = ({ data }: { data: HistoricalOdds[] }) => {
  const [hoveredPoint, setHoveredPoint] = useState<{ index: number; party: 'dem' | 'rep' } | null>(null);

  const width = 900;
  const height = 400;
  const padding = { top: 35, right: 65, bottom: 45, left: 55 };

  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  // Scale functions
  const xScale = (index: number) => padding.left + (index / (data.length - 1)) * chartWidth;
  const yScale = (value: number) => padding.top + chartHeight - ((value / 100) * chartHeight);

  // Generate Democrat path
  const demLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  // Generate Republican path (100 - demOdds)
  const repLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(100 - d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  // Get labels for x-axis (show dates at intervals)
  const dateIndices = [0, Math.floor(data.length / 4), Math.floor(data.length / 2), Math.floor(3 * data.length / 4), data.length - 1];
  const dateLabels = dateIndices.map(i => ({
    index: i,
    label: new Date(data[i].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
  }));

  const currentDemOdds = data[data.length - 1].demOdds;
  const currentRepOdds = Math.round((100 - currentDemOdds) * 10) / 10;

  // Get tooltip data
  const getTooltipData = () => {
    if (!hoveredPoint) return null;
    const d = data[hoveredPoint.index];
    const date = new Date(d.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    const odds = hoveredPoint.party === 'dem' ? d.demOdds : Math.round((100 - d.demOdds) * 10) / 10;
    const party = hoveredPoint.party === 'dem' ? 'Democrats' : 'Republicans';
    const color = hoveredPoint.party === 'dem' ? '#0015BC' : '#BC0000';
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

      {/* 50% line (highlighted - the "toss-up" line) */}
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

      {/* Data points and lines for Democrats */}
      <path
        d={demLinePath}
        fill="none"
        stroke="#0015BC"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`dem-${i}`}
          cx={xScale(i)}
          cy={yScale(d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'dem' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#0015BC"
          style={{ cursor: 'pointer' }}
          onMouseEnter={() => setHoveredPoint({ index: i, party: 'dem' })}
          onMouseLeave={() => setHoveredPoint(null)}
        />
      ))}

      {/* Data points and lines for Republicans */}
      <path
        d={repLinePath}
        fill="none"
        stroke="#BC0000"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`rep-${i}`}
          cx={xScale(i)}
          cy={yScale(100 - d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'rep' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#BC0000"
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
        fill="#0015BC"
      >
        {currentDemOdds}%
      </text>
      <text
        x={width - padding.right + 8}
        y={yScale(currentRepOdds)}
        alignmentBaseline="middle"
        fontSize="14"
        fontWeight="bold"
        fill="#BC0000"
      >
        {currentRepOdds}%
      </text>

      {/* Legend */}
      <g transform={`translate(${padding.left + 10}, ${padding.top - 18})`}>
        <circle cx="0" cy="0" r="6" fill="#0015BC" />
        <text x="12" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">Democrats</text>
        <circle cx="110" cy="0" r="6" fill="#BC0000" />
        <text x="122" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">Republicans</text>
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
