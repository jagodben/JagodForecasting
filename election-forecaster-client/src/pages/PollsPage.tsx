import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { forecastApi, statesApi } from '../services/api';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { useIsDesktop } from '../utils/useMediaQuery';
import { districtCode } from '../utils/districts';
import { PartisanBadge } from '../components/PartisanBadge';

type Chamber = 'senate' | 'house' | 'governors' | 'ballot';

const chamberOf = (raceId: string): Chamber =>
  raceId.includes('-SEN') ? 'senate' : raceId.includes('-GOV') ? 'governors' : 'house';

const formatDate = (iso: string): string =>
  new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

const formatDateShort = (iso: string): string => {
  const d = new Date(iso);
  return `${d.getMonth() + 1}/${d.getDate()}/${String(d.getFullYear()).slice(2)}`;
};

const formatMargin = (margin: number): string => {
  const r = Math.round(margin * 10) / 10;
  if (r === 0) return 'EVEN';
  const abs = Math.abs(r);
  const num = Number.isInteger(abs) ? abs.toString() : abs.toFixed(1);
  return r > 0 ? `D+${num}` : `R+${num}`;
};

export const PollsPage = () => {
  useDocumentTitle('Polls');
  const isDesktop = useIsDesktop();

  // Active chamber lives in the URL so back-navigation and shared links keep the tab.
  const [searchParams, setSearchParams] = useSearchParams();
  const tabParam = searchParams.get('tab');
  const tab: Chamber = tabParam === 'house' || tabParam === 'governors' || tabParam === 'ballot' ? tabParam : 'senate';

  const { data: polls, isLoading } = useQuery({
    queryKey: ['allPolls'],
    queryFn: forecastApi.getAllPolls,
  });

  const { data: states } = useQuery({
    queryKey: ['states'],
    queryFn: statesApi.getAll,
  });

  const { data: ballot, isLoading: ballotLoading } = useQuery({
    queryKey: ['genericBallot'],
    queryFn: forecastApi.getGenericBallot,
    enabled: tab === 'ballot',
  });

  const stateName = (id: string) => states?.find(s => s.id === id)?.name ?? id;

  // Race column label: "Michigan Senate", "Ohio Governor", or the district code "OH-01".
  const raceLabel = (raceId: string): string => {
    const stateId = raceId.slice(0, 2);
    if (raceId.includes('-SEN')) return `${stateName(stateId)} Senate`;
    if (raceId.includes('-GOV')) return `${stateName(stateId)} Governor`;
    const district = parseInt(raceId.split('-')[1], 10);
    return districtCode(stateId, district);
  };

  const counts: Record<Chamber, number> = { senate: 0, house: 0, governors: 0, ballot: 0 };
  (polls ?? []).forEach(p => { counts[chamberOf(p.raceId)]++; });
  const shown = (polls ?? []).filter(p => chamberOf(p.raceId) === tab);

  const cell: React.CSSProperties = { padding: isDesktop ? '10px 12px' : '8px 6px', whiteSpace: 'nowrap' };

  return (
    <div style={{ backgroundColor: 'white', minHeight: '100vh', padding: '20px', maxWidth: '860px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>Polls</span>
      </nav>

      <header style={{ marginBottom: '16px' }}>
        <h1 style={{ margin: '0 0 6px 0' }}>Polls</h1>
        <p style={{ margin: 0, color: '#555555', fontSize: '14px' }}>
          Every poll the site has collected from Wikipedia, newest first. New polls appear here
          as the daily refresh finds them.
        </p>
      </header>

      <div className="dashboard-tabs" style={{ marginBottom: '16px' }}>
        {(['senate', 'house', 'governors', 'ballot'] as Chamber[]).map(c => (
          <button
            key={c}
            onClick={() => setSearchParams(c === 'senate' ? {} : { tab: c }, { replace: true })}
            className={`dashboard-tab ${tab === c ? 'dashboard-tab--active' : ''}`}
            style={{ color: tab === c ? undefined : '#333' }}
          >
            {c === 'ballot' ? 'Generic ballot'
              : `${c === 'senate' ? 'Senate' : c === 'house' ? 'House' : 'Governors'} (${counts[c]})`}
          </button>
        ))}
      </div>

      {tab === 'ballot' && (
        <div>
          <p style={{ margin: '0 0 16px 0', color: '#555', fontSize: '14px' }}>
            The national generic congressional ballot — the model&rsquo;s measure of the national
            environment. Wikipedia&rsquo;s aggregator table, averaged into the daily value the model uses.
          </p>
          {ballotLoading && <div className="loading-container"><div className="spinner" /></div>}
          {ballot && (
            <>
              <h3 style={{ margin: '8px 0' }}>Current aggregator averages</h3>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: isDesktop ? '14px' : '13px', marginBottom: '24px' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid #e0e0e0', textAlign: 'left', color: '#555' }}>
                    <th style={cell}>Aggregator</th>
                    <th style={{ ...cell, textAlign: 'right', color: '#123f8f' }}>D</th>
                    <th style={{ ...cell, textAlign: 'right', color: '#9c150b' }}>R</th>
                    <th style={{ ...cell, textAlign: 'right' }}>Margin</th>
                  </tr>
                </thead>
                <tbody>
                  {ballot.aggregates.map(a => (
                    <tr key={a.source} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ ...cell, whiteSpace: 'normal', fontWeight: 500 }}>{a.source}</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#123f8f' }}>{a.demPercent.toFixed(1)}%</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#9c150b' }}>{a.repPercent.toFixed(1)}%</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: a.demPercent - a.repPercent > 0 ? '#123f8f' : '#9c150b' }}>
                        {formatMargin(a.demPercent - a.repPercent)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <h3 style={{ margin: '8px 0' }}>Daily average used by the model</h3>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: isDesktop ? '14px' : '13px' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid #e0e0e0', textAlign: 'left', color: '#555' }}>
                    <th style={cell}>Date</th>
                    <th style={{ ...cell, textAlign: 'right', color: '#123f8f' }}>D</th>
                    <th style={{ ...cell, textAlign: 'right', color: '#9c150b' }}>R</th>
                    <th style={{ ...cell, textAlign: 'right' }}>Margin</th>
                  </tr>
                </thead>
                <tbody>
                  {ballot.history.map(d => (
                    <tr key={d.date} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ ...cell, color: '#666' }}>{isDesktop ? formatDate(d.date) : formatDateShort(d.date)}</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#123f8f' }}>{d.demPercent.toFixed(1)}%</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#9c150b' }}>{d.repPercent.toFixed(1)}%</td>
                      <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: d.demPercent - d.repPercent > 0 ? '#123f8f' : '#9c150b' }}>
                        {formatMargin(d.demPercent - d.repPercent)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
      )}

      {tab !== 'ballot' && isLoading && (
        <div className="loading-container"><div className="spinner" /></div>
      )}

      {tab !== 'ballot' && !isLoading && shown.length === 0 && (
        <div style={{ color: '#6b6b6b', padding: '24px 0' }}>No polls collected for this chamber yet.</div>
      )}

      {tab !== 'ballot' && shown.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: isDesktop ? '14px' : '13px' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid #e0e0e0', textAlign: 'left', color: '#555' }}>
              <th style={cell}>Race</th>
              <th style={cell}>Pollster</th>
              <th style={cell}>Date</th>
              {isDesktop && <th style={cell}>Sample</th>}
              <th style={{ ...cell, textAlign: 'right', color: '#123f8f' }}>D</th>
              <th style={{ ...cell, textAlign: 'right', color: '#9c150b' }}>R</th>
              <th style={{ ...cell, textAlign: 'right' }}>Margin</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((poll, i) => (
              <tr key={`${poll.raceId}-${poll.pollster}-${poll.date}-${i}`} style={{ borderBottom: '1px solid #eee' }}>
                <td style={cell}>
                  <Link to={`/race/${poll.raceId}`} style={{ color: 'inherit', fontWeight: 500 }}>
                    {raceLabel(poll.raceId)}
                  </Link>
                </td>
                <td style={{ ...cell, whiteSpace: 'normal' }}>
                  {poll.pollster}
                  {poll.isPartisan && <PartisanBadge lean={poll.partisanLean} />}
                </td>
                <td style={{ ...cell, color: '#666' }}>
                  {isDesktop ? formatDate(poll.date) : formatDateShort(poll.date)}
                </td>
                {isDesktop && (
                  <td style={{ ...cell, color: '#666' }}>
                    {poll.sampleSize ? `${poll.sampleSize.toLocaleString()}${poll.population ? ` ${poll.population}` : ''}` : '—'}
                  </td>
                )}
                <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#123f8f' }}>{poll.demPercent}%</td>
                <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#9c150b' }}>{poll.repPercent}%</td>
                <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: poll.margin > 0 ? '#123f8f' : poll.margin < 0 ? '#9c150b' : '#666' }}>
                  {formatMargin(poll.margin)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
};
