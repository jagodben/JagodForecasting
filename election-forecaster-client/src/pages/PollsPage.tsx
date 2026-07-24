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
  return `${d.getMonth() + 1}/${d.getDate()}`;
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
  const stateParam = searchParams.get('state') ?? '';

  const setParams = (nextTab: Chamber, nextState: string) => {
    const params: Record<string, string> = {};
    if (nextTab !== 'senate') params.tab = nextTab;
    if (nextState) params.state = nextState;
    setSearchParams(params, { replace: true });
  };

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

  const tabPolls = (polls ?? []).filter(p => chamberOf(p.raceId) === tab);

  // Only states that actually have polls in this tab are offered; a selection that
  // doesn't exist here (e.g. carried over from another tab) falls back to all states.
  const stateOptions = [...new Set(tabPolls.map(p => p.raceId.slice(0, 2)))]
    .map(id => ({ id, name: stateName(id) }))
    .sort((a, b) => a.name.localeCompare(b.name));
  const selectedState = stateOptions.some(o => o.id === stateParam) ? stateParam : '';

  const shown = selectedState ? tabPolls.filter(p => p.raceId.startsWith(selectedState)) : tabPolls;

  const cell: React.CSSProperties = { padding: isDesktop ? '10px 12px' : '7px 4px', whiteSpace: 'nowrap' };

  return (
    <div style={{ backgroundColor: 'white', minHeight: '100vh', padding: isDesktop ? '20px' : '12px', maxWidth: '860px', margin: '0 auto' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>Polls</span>
      </nav>

      <header style={{ marginBottom: '16px' }}>
        <h1 style={{ margin: 0 }}>Polls</h1>
      </header>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', marginBottom: '16px' }}>
        {(['senate', 'house', 'governors'] as Chamber[]).map(c => (
          <button
            key={c}
            onClick={() => setParams(c, stateParam)}
            style={{
              padding: '8px 20px',
              borderRadius: '2px',
              border: tab === c ? '1px solid #121212' : '1px solid #d5d5d5',
              backgroundColor: tab === c ? '#121212' : 'white',
              color: tab === c ? 'white' : '#333',
              fontSize: '0.8rem',
              fontWeight: tab === c ? 700 : 600,
              textTransform: 'uppercase',
              letterSpacing: '0.08em',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            {c === 'senate' ? 'Senate' : c === 'house' ? 'House' : 'Governors'}
          </button>
        ))}
        {/* Mobile: break the flex row so the state filter starts at the left edge, under Senate */}
        {!isDesktop && <div style={{ flexBasis: '100%', height: 0 }} />}
        <select
          value={selectedState}
          onChange={e => setParams(tab, e.target.value)}
          aria-label="Filter by state"
          style={{
            marginLeft: isDesktop ? 'auto' : 0,
            padding: '6px 10px',
            borderRadius: '2px',
            border: '1px solid #d5d5d5',
            backgroundColor: 'white',
            color: '#333',
            fontSize: '13px',
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          <option value="">All states</option>
          {stateOptions.map(o => (
            <option key={o.id} value={o.id}>{o.name}</option>
          ))}
        </select>
      </div>

      {isLoading && (
        <div className="loading-container"><div className="spinner" /></div>
      )}

      {!isLoading && shown.length === 0 && (
        <div style={{ color: '#6b6b6b', padding: '24px 0' }}>No polls collected for this chamber yet.</div>
      )}

      {shown.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: isDesktop ? '14px' : '12.5px', tableLayout: isDesktop ? 'auto' : 'fixed' }}>
          {!isDesktop && (
            <colgroup>
              <col style={{ width: '28%' }} />
              <col style={{ width: '42%' }} />
              <col style={{ width: '13%' }} />
              <col style={{ width: '17%' }} />
            </colgroup>
          )}
          <thead>
            <tr style={{ borderBottom: '2px solid #e0e0e0', textAlign: 'left', color: '#555' }}>
              <th style={cell}>Race</th>
              <th style={cell}>Pollster</th>
              <th style={cell}>Date</th>
              {isDesktop && <th style={cell}>Sample</th>}
              {isDesktop && <th style={{ ...cell, textAlign: 'right', color: '#123f8f' }}>D</th>}
              {isDesktop && <th style={{ ...cell, textAlign: 'right', color: '#9c150b' }}>R</th>}
              <th style={{ ...cell, textAlign: 'right' }}>Margin</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((poll, i) => (
              <tr key={`${poll.raceId}-${poll.pollster}-${poll.date}-${i}`} style={{ borderBottom: '1px solid #eee' }}>
                <td style={{ ...cell, whiteSpace: isDesktop ? 'nowrap' : 'normal' }}>
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
                {isDesktop && <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#123f8f' }}>{poll.demPercent}%</td>}
                {isDesktop && <td style={{ ...cell, textAlign: 'right', fontWeight: 600, color: '#9c150b' }}>{poll.repPercent}%</td>}
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
