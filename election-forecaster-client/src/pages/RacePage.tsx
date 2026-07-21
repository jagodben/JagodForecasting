import { useMemo, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { racesApi, forecastApi, statesApi } from '../services/api';
import { RaceType, Party, RacePolls, Race, Candidate, DetailedForecast } from '../types';
import { ProbabilityTrendChart } from '../components/charts/ProbabilityTrendChart';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { useIsDesktop } from '../utils/useMediaQuery';
import { isTbdCandidate, TBD_NOTE } from '../utils/candidates';
import { districtCode } from '../utils/districts';
import { getCandidatePhoto } from '../utils/photos';
import { CandidateAvatar } from '../components/CandidateAvatar';

interface HistoricalOdds {
  date: string;
  demOdds: number;
}

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

// Party logo (donkey/elephant) for the major parties; null for others (fall back to a letter badge).
const getPartyLogo = (party: Party): string | null => {
  if (party === Party.Democrat) return '/democrat.png';
  if (party === Party.Republican) return '/republican.png';
  return null;
};

const formatPollDate = (iso: string): string =>
  new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

// Chart label for a candidate: the chart abbreviates to the last word, so unresolved
// placeholders ("TBD Democrat") label as the party name instead.
const chartLabel = (name: string | undefined, party: string): string =>
  !name || isTbdCandidate(name) ? party : name;

// Compact form for phones ("6/27/26") — narrow enough that the polls table never scrolls.
const formatPollDateShort = (iso: string): string => {
  const d = new Date(iso);
  return `${d.getMonth() + 1}/${d.getDate()}/${String(d.getFullYear()).slice(2)}`;
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

export const RacePage = () => {
  const { raceId } = useParams<{ raceId: string }>();
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

  // Combined blended forecast (markets + polling + fundamentals + national environment) — the same
  // value the home page map shows. The per-source lens selector was removed; only the model forecast
  // is surfaced now. The actual polls still appear below in their own section.
  const { demProb, historicalData } = useMemo(() => {
    if (!race) return { demProb: 0.5, historicalData: [] };

    // The challenger (non-Republican) holds the Dem-side probability — a Democrat, or a viable
    // independent that replaced the token Democrat.
    const demCandidate = race.candidates.find(c => c.party !== Party.Republican);
    const demForecastData = race.forecasts.find(f => f.candidateId === demCandidate?.id);

    // Fall back to the fundamentals-only race forecast only if the blended forecast hasn't loaded.
    const demProbability = forecast?.demWinProbability ?? demForecastData?.winProbability ?? 0.5;

    // Timeline starts July 1, 2026 (the server already floors history to this date).
    const startDate = new Date('2026-07-01');
    const historical: HistoricalOdds[] = (forecast?.history ?? [])
      .filter(h => new Date(h.date) >= startDate)
      .map(h => ({
        date: h.date.split('T')[0],
        demOdds: Math.round(h.demWinProbability * 1000) / 10,
      }));

    return { demProb: demProbability, historicalData: historical };
  }, [race, forecast]);

  const pageTitle = race
    ? `${state?.name || race.stateId} ${getRaceTypeLabel(race.type, race.districtNumber, race.stateId)} 2026`
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
        <h2>We couldn&rsquo;t load this race</h2>
        <p>It may not exist, or the forecast is still loading.</p>
        <div style={{ display: 'flex', alignItems: 'center', gap: '20px', marginTop: '8px' }}>
          <button onClick={() => window.location.reload()}>Try again</button>
          <Link to="/">← Back to the map</Link>
        </div>
      </div>
    );
  }

  const repCandidate = race.candidates.find(c => c.party === Party.Republican);
  const demCandidate = race.candidates.find(c => c.id !== repCandidate?.id);
  // Challenger-side color: gold for a viable independent, blue for a Democrat.
  const demColor = demCandidate ? getPartyColor(demCandidate.party) : '#123f8f';

  const stateName = state?.name || race.stateId;
  const raceTypeLabel = getRaceTypeLabel(race.type, race.districtNumber, race.stateId);

  // Headline always uses the combined blended forecast (falling back to the useMemo demProb).
  const headDem = forecast?.demWinProbability ?? demProb;
  const headRep = 1 - headDem;

  return (
    <div className="race-page">
      <nav className="breadcrumb" style={{ marginBottom: '12px' }}>
        <Link to={race.type === RaceType.Governor ? '/?view=governors' : race.type === RaceType.House ? '/?view=house' : '/'}>Map</Link>
        <span> / </span>
        <Link to={`/state/${race.stateId}`}>{stateName}</Link>
        <span> / </span>
        <span>{raceTypeLabel}</span>
      </nav>

      <div className="race-page__header-top">
        <div>
          <h1 style={{ margin: 0 }}>{stateName}</h1>
          <h2 style={{ margin: '4px 0 0', color: '#666', fontWeight: 'normal', fontSize: '1.15rem' }}>
            {raceTypeLabel} {race.year}
            {race.isSpecialElection && <span style={{ marginLeft: '8px', color: '#dc2626' }}>(Special Election)</span>}
          </h2>
          {forecast && (
            <div style={{ fontSize: '12px', color: '#6b6b6b', marginTop: '6px' }}>
              Updated daily at 8:00 AM ET
            </div>
          )}
        </div>
      </div>

      <div className="race-page__body">
        {isDesktop ? (
          <>
            {/* Left column: Model chart (top) + projected result (bottom) */}
            <div className="race-page__col">
              {historicalData.length >= 2 && (
                <div style={{ width: '100%' }}>
                  <div style={{ fontSize: '0.7rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: '12px' }}>Race Timeline</div>
                  <ProbabilityTrendChart
                    data={historicalData.map(h => ({ date: h.date, demValue: h.demOdds / 100 }))}
                    demLabel={chartLabel(demCandidate?.name, 'Democrat')}
                    repLabel={chartLabel(repCandidate?.name, 'Republican')}
                    demColor={demColor}
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
                    color: forecast.expectedDemMargin > 0 ? '#123f8f' : forecast.expectedDemMargin < 0 ? '#9c150b' : '#666',
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
                forecast={forecast}
              />
              <CandidatesList race={race} />
            </div>

            <div className="race-page__right">
              {historicalData.length >= 2 && (
                <div style={{ width: '100%' }}>
                  <div style={{ fontSize: '0.7rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: '12px' }}>Race Timeline</div>
                  <ProbabilityTrendChart
                    data={historicalData.map(h => ({ date: h.date, demValue: h.demOdds / 100 }))}
                    demLabel={chartLabel(demCandidate?.name, 'Democrat')}
                    repLabel={chartLabel(repCandidate?.name, 'Republican')}
                    demColor={demColor}
                    width={760}
                    height={300}
                    pillScale={(760 / 320) * 0.8}
                  />
                </div>
              )}

              <PollsSection data={pollsData} demName={demCandidate?.name} repName={repCandidate?.name} />
            </div>
          </>
        )}
      </div>

      {/* Drill-up to the state overview (Senate + Governor + every district in one place) —
          otherwise state pages are only reachable from no-race gray states on the map. */}
      <div style={{ textAlign: 'center', margin: '28px 0 8px' }}>
        <Link to={`/state/${race.stateId}`} style={{ fontSize: '14px', fontWeight: 600 }}>
          See all {stateName} races →
        </Link>
      </div>
    </div>
  );
};

// Win-probability headline (big Dem/Rep % + bar + projected margin). Mobile only — on desktop the
// candidates list already shows each side's win probability, so the headline is redundant.
const WinProbHeadline = ({ headDem, headRep, demCandidate, forecast }: {
  headDem: number;
  headRep: number;
  demCandidate?: Candidate;
  forecast?: DetailedForecast;
}) => (
  <div>
    <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>Win Probability</h3>
    <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
      <div style={{ flex: 1, textAlign: 'center' }}>
        <div style={{ fontSize: '38px', fontWeight: 'bold', color: demCandidate ? getPartyColor(demCandidate.party) : '#123f8f' }}>{(headDem * 100).toFixed(1)}%</div>
      </div>
      <div style={{ flex: 2, height: '44px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
        <div style={{ width: `${headDem * 100}%`, backgroundColor: demCandidate ? getPartyColor(demCandidate.party) : '#123f8f', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontWeight: 'bold', transition: 'width 0.3s ease' }}>{demCandidate?.party === Party.Independent ? 'I' : 'D'}</div>
        <div style={{ width: `${headRep * 100}%`, backgroundColor: '#9c150b', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white', fontWeight: 'bold', transition: 'width 0.3s ease' }}>R</div>
      </div>
      <div style={{ flex: 1, textAlign: 'center' }}>
        <div style={{ fontSize: '38px', fontWeight: 'bold', color: '#9c150b' }}>{(headRep * 100).toFixed(1)}%</div>
      </div>
    </div>
    {forecast && (
      <div style={{ textAlign: 'center', marginTop: '16px' }}>
        <div style={{ fontSize: '13px', color: '#666', marginBottom: '4px' }}>Projected result</div>
        <div style={{ fontSize: '24px', fontWeight: 'bold', color: forecast.expectedDemMargin > 0 ? '#123f8f' : forecast.expectedDemMargin < 0 ? '#9c150b' : '#666' }}>
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
        const fallback = partyLogo ? (
          <img src={partyLogo} alt={candidate.party} style={{ width: '47px', height: '47px', objectFit: 'contain', display: 'block' }} />
        ) : (
          <div style={{ width: '47px', height: '47px', borderRadius: '50%', backgroundColor: getPartyColor(candidate.party), color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', fontSize: '18px' }}>{candidate.party.charAt(0)}</div>
        );
        return (
          <div key={candidate.id} style={{ display: 'flex', alignItems: 'center', gap: '14px', padding: '12px 0', borderBottom: '1px solid #eee' }}>
            <div style={{ flexShrink: 0 }}>
              <CandidateAvatar photo={getCandidatePhoto(race.id, candidate.name)} name={candidate.name} size={42} fallback={fallback} ringParty={candidate.party} />
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 'bold', fontSize: '17px' }}>
                {candidate.name}
                {isTbdCandidate(candidate.name) && <span title={TBD_NOTE.slice(2)}>*</span>}
              </div>
              <div style={{ color: '#666', fontSize: '14px' }}>{candidate.party}</div>
            </div>
          </div>
        );
      })}
    </div>
    {race.candidates.some(c => isTbdCandidate(c.name)) && (
      <div style={{ marginTop: '10px', fontSize: '12px', color: '#6b6b6b' }}>{TBD_NOTE}</div>
    )}
  </div>
);

// How many polls the table shows before the "show all" toggle expands it.
const COLLAPSED_POLL_COUNT = 3;

// Polls list + weighted average. Starts collapsed to the newest few polls with a toggle to show
// the rest. On phones the table slims down (no Sample column, compact dates, tighter cells) so it
// always fits the viewport without scrolling.
const PollsSection = ({ data, demName, repName }: { data?: RacePolls; demName?: string; repName?: string }) => {
  const isDesktop = useIsDesktop();
  const [expanded, setExpanded] = useState(false);
  if (!data || data.polls.length === 0) {
    return (
      <div>
        <h3 style={{ margin: '0 0 8px 0' }}>Polls</h3>
        <div style={{ textAlign: 'center', padding: '24px', color: '#6b6b6b', fontSize: '14px' }}>
          No polling data available for this race yet.
        </div>
      </div>
    );
  }

  const avg = data.average;
  const demLead = avg ? avg.margin >= 0 : true;
  const shownPolls = expanded ? data.polls : data.polls.slice(0, COLLAPSED_POLL_COUNT);
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
            <div style={{ fontSize: '28px', fontWeight: 'bold', color: '#123f8f' }}>{avg.demPercent.toFixed(1)}%</div>
            <div style={{ fontSize: '12px', color: '#666' }}>{demName || 'Democrat'}</div>
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: '16px', fontWeight: 600, color: demLead ? '#123f8f' : '#9c150b' }}>
              {demLead ? 'D' : 'R'} +{Math.abs(avg.margin).toFixed(1)}
            </div>
            <div style={{ fontSize: '11px', color: '#6b6b6b' }}>avg. margin</div>
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: '28px', fontWeight: 'bold', color: '#9c150b' }}>{avg.repPercent.toFixed(1)}%</div>
            <div style={{ fontSize: '12px', color: '#666' }}>{repName || 'Republican'}</div>
          </div>
        </div>
      )}

      {/* Individual polls — the table fits every viewport (the mobile variant slims down), so it
          needs no scroll container. */}
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: isDesktop ? '14px' : '13px' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid #eee', textAlign: 'left', color: '#666' }}>
              <th style={{ padding: isDesktop ? '8px 12px 8px 0' : '6px 6px 6px 0' }}>Pollster</th>
              <th style={{ padding: isDesktop ? '8px 12px' : '6px' }}>Date</th>
              {isDesktop && <th style={{ padding: '8px 12px', textAlign: 'right' }}>Sample</th>}
              <th style={{ padding: isDesktop ? '8px 12px' : '6px', textAlign: 'right', color: '#123f8f' }}>D</th>
              <th style={{ padding: isDesktop ? '8px 12px' : '6px', textAlign: 'right', color: '#9c150b' }}>R</th>
              <th style={{ padding: isDesktop ? '8px 0 8px 12px' : '6px 0 6px 6px', textAlign: 'right' }}>Margin</th>
            </tr>
          </thead>
          <tbody>
            {shownPolls.map((poll, i) => {
              const leadD = poll.margin >= 0;
              return (
                <tr key={i} style={{ borderBottom: '1px solid #f0f0f0' }}>
                  {/* overflowWrap lets slash-joined pollster names ("…University/ReconMR/Siena…")
                      break on phones — otherwise their unbreakable width forces the table to scroll. */}
                  <td style={{ padding: isDesktop ? '10px 12px 10px 0' : '8px 6px 8px 0', overflowWrap: isDesktop ? undefined : 'anywhere' }}>
                    {poll.pollster}
                    {poll.isPartisan && (
                      <span style={{ marginLeft: '6px', fontSize: '11px', color: '#b45309', backgroundColor: '#fef3c7', padding: '1px 6px', borderRadius: '4px' }}>
                        partisan
                      </span>
                    )}
                  </td>
                  <td style={{ padding: isDesktop ? '10px 12px' : '8px 6px', color: '#666', whiteSpace: 'nowrap' }}>
                    {isDesktop ? formatPollDate(poll.date) : formatPollDateShort(poll.date)}
                  </td>
                  {isDesktop && (
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#666' }}>
                      {poll.sampleSize ? poll.sampleSize.toLocaleString() : '—'}{poll.population ? ` ${poll.population}` : ''}
                    </td>
                  )}
                  <td style={{ padding: isDesktop ? '10px 12px' : '8px 6px', textAlign: 'right', fontWeight: leadD ? 'bold' : 'normal' }}>{poll.demPercent.toFixed(0)}%</td>
                  <td style={{ padding: isDesktop ? '10px 12px' : '8px 6px', textAlign: 'right', fontWeight: !leadD ? 'bold' : 'normal' }}>{poll.repPercent.toFixed(0)}%</td>
                  <td style={{ padding: isDesktop ? '10px 0 10px 12px' : '8px 0 8px 6px', textAlign: 'right', color: leadD ? '#123f8f' : '#9c150b', fontWeight: 600, whiteSpace: 'nowrap' }}>
                    {leadD ? 'D' : 'R'} +{Math.abs(poll.margin).toFixed(0)}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      {data.polls.length > COLLAPSED_POLL_COUNT && (
        <button
          onClick={() => setExpanded(!expanded)}
          style={{
            display: 'block',
            width: '100%',
            marginTop: '8px',
            padding: '10px',
            background: 'none',
            border: 'none',
            cursor: 'pointer',
            fontSize: '13px',
            fontWeight: 600,
            color: '#123f8f',
          }}
        >
          {expanded ? 'Show fewer ▲' : `Show ${hiddenCount} more poll${hiddenCount === 1 ? '' : 's'} ▼`}
        </button>
      )}
    </div>
  );
};
