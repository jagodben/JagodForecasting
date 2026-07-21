import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { forecastApi, statesApi } from '../services/api';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { useIsDesktop } from '../utils/useMediaQuery';
import { districtCode } from '../utils/districts';
import { PartisanBadge } from '../components/PartisanBadge';

type Chamber = 'senate' | 'house' | 'governors';

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
  const tab: Chamber = tabParam === 'house' || tabParam === 'governors' ? tabParam : 'senate';

  const { data: polls, isLoading } = useQuery({
    queryKey: ['allPolls'],
    queryFn: forecastApi.getAllPolls,
  });

  const { data: states } = useQuery({
    queryKey: ['states'],
    queryFn: statesApi.getAll,
  });

  const stateName = (id: string) => states?.find(s => s.id === id)?.name ?? id;

  // Race column label: the state name (chamber's implied by the tab), or the district code "OH-01".
  const raceLabel = (raceId: string): string => {
    const stateId = raceId.slice(0, 2);
    if (raceId.includes('-SEN') || raceId.includes('-GOV')) return stateName(stateId);
    const district = parseInt(raceId.split('-')[1], 10);
    return districtCode(stateId, district);
  };

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
        <h1 style={{ margin: 0 }}>Polls</h1>
      </header>

      <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
        {(['senate', 'house', 'governors'] as Chamber[]).map(c => (
          <button
            key={c}
            onClick={() => setSearchParams(c === 'senate' ? {} : { tab: c }, { replace: true })}
            style={{
              padding: '7px 16px',
              borderRadius: '8px',
              border: tab === c ? '1px solid #121212' : '1px solid #d5d5d5',
              backgroundColor: tab === c ? '#121212' : 'white',
              color: tab === c ? 'white' : '#333',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            {c === 'senate' ? 'Senate' : c === 'house' ? 'House' : 'Governors'}
          </button>
        ))}
      </div>

      {isLoading && (
        <div className="loading-container"><div className="spinner" /></div>
      )}

      {!isLoading && shown.length === 0 && (
        <div style={{ color: '#6b6b6b', padding: '24px 0' }}>No polls collected for this chamber yet.</div>
      )}

      {shown.length > 0 && (
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
