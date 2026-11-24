import { useMemo } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { statesApi } from '../services/api';
import { useDocumentTitle } from '../utils/useDocumentTitle';
import { useIsDesktop } from '../utils/useMediaQuery';
import { districtCode } from '../utils/districts';
import {
  Chamber, ChamberForecast, ElectionResults, RaceResult, useElectionResults, calledCorrectly, chamberOf,
  challengerLetter, formatError, formatResultMargin, marginColor, marginError, tallySeats,
} from '../utils/results';
import { CallBadge } from '../components/ResultComparison';
import './ResultsPage.css';

type Sort = 'race' | 'miss';

const CHAMBERS: { id: Chamber; label: string }[] = [
  { id: 'senate', label: 'Senate' },
  { id: 'governors', label: 'Governors' },
  { id: 'house', label: 'House' },
];

// Race lists group Senate, then governors, then House.
const chamberRank = (raceId: string) => CHAMBERS.findIndex(c => c.id === chamberOf(raceId));

// Best calls: the competitive races (not rated Solid) whose result landed closest to the forecast.
const BEST_CALLS_PER_CHAMBER = 5;

interface Row { id: string; result: RaceResult; called: boolean | null; error: number | null }

// Accuracy figures for a set of races.
const summarize = (rows: Row[]) => {
  const decided = rows.filter(r => r.called != null);
  const right = decided.filter(r => r.called).length;
  // Competitive: anything the forecast didn't rate Solid (within 8 points).
  const competitive = decided.filter(r => Math.abs(r.result.predictedMargin) < 8);
  const withMargin = rows.filter(r => r.error != null);
  const bias = withMargin.length
    ? withMargin.reduce((s, r) => s + r.error!, 0) / withMargin.length : null;
  return {
    decided: decided.length,
    right,
    competitive: competitive.length,
    competitiveRight: competitive.filter(r => r.called).length,
    bias,
  };
};

const pct = (n: number, d: number) => (d ? `${Math.round((n / d) * 100)}%` : '—');

export const ResultsPage = () => {
  useDocumentTitle('2026 Results vs. Forecast');
  const isDesktop = useIsDesktop();
  const [params, setParams] = useSearchParams();
  const tab: Chamber = (CHAMBERS.find(c => c.id === params.get('chamber'))?.id) ?? 'senate';
  const sort: Sort = params.get('sort') === 'miss' ? 'miss' : 'race';
  const stateParam = params.get('state') ?? '';

  const { data, isLoading, isError } = useElectionResults();
  const { data: states } = useQuery({ queryKey: ['states'], queryFn: statesApi.getAll });
  const stateName = (id: string) => states?.find(s => s.id === id)?.name ?? id;

  const raceLabel = (raceId: string): string => {
    const stateId = raceId.slice(0, 2);
    if (raceId.includes('-SEN')) return `${stateId} Senate`;
    if (raceId.includes('-GOV')) return `${stateId} Governor`;
    return districtCode(stateId, parseInt(raceId.split('-')[1], 10));
  };

  const rows: Row[] = useMemo(() => Object.entries(data?.races ?? {}).map(([id, result]) => ({
    id, result, called: calledCorrectly(result), error: marginError(result),
  })), [data]);

  const update = (next: Partial<{ chamber: Chamber; sort: Sort; state: string }>) => {
    const merged = { chamber: tab, sort, state: stateParam, ...next };
    const p: Record<string, string> = {};
    if (merged.chamber !== 'senate') p.chamber = merged.chamber;
    if (merged.sort !== 'race') p.sort = merged.sort;
    if (merged.state) p.state = merged.state;
    setParams(p, { replace: true });
  };

  if (isLoading) return <div className="loading-container"><div className="spinner" /></div>;
  if (isError || !data) {
    return (
      <div className="results-page">
        <h1>Results</h1>
        <p>Results couldn&rsquo;t be loaded. <Link to="/">← Back to the map</Link></p>
      </div>
    );
  }

  const overall = summarize(rows);
  const misses = rows.filter(r => r.called === false)
    .sort((a, b) => chamberRank(a.id) - chamberRank(b.id)
      || Math.abs(b.result.predictedMargin) - Math.abs(a.result.predictedMargin));
  const bestCalls = CHAMBERS.flatMap(c => rows
    .filter(r => chamberOf(r.id) === c.id && r.called && r.error != null && Math.abs(r.result.predictedMargin) < 8)
    .sort((a, b) => Math.abs(a.error!) - Math.abs(b.error!))
    .slice(0, BEST_CALLS_PER_CHAMBER));

  const tabRows = rows.filter(r => chamberOf(r.id) === tab);
  const stateOptions = [...new Set(tabRows.map(r => r.id.slice(0, 2)))]
    .map(id => ({ id, name: stateName(id) }))
    .sort((a, b) => a.name.localeCompare(b.name));
  const selectedState = stateOptions.some(o => o.id === stateParam) ? stateParam : '';
  const shown = (selectedState ? tabRows.filter(r => r.id.startsWith(selectedState)) : tabRows)
    .slice()
    .sort((a, b) => sort === 'miss'
      ? Math.abs(b.error ?? -1) - Math.abs(a.error ?? -1)
      : raceLabel(a.id).localeCompare(raceLabel(b.id), undefined, { numeric: true }));

  const asOf = new Date(`${data.asOf}T12:00:00`).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });

  return (
    <div className="results-page" style={{ padding: isDesktop ? '20px' : '12px' }}>
      <nav className="breadcrumb" style={{ marginBottom: '20px' }}>
        <Link to="/">Map</Link>
        <span> / </span>
        <span>Results</span>
      </nav>

      <header>
        <h1 style={{ margin: 0 }}>How the forecast did</h1>
        <p className="results-page__dek">
          Our final forecast from Election Day morning beside the results. Results as of {asOf};
          some counts are still coming in.
        </p>
      </header>

      <div className="chamber-cards">
        <ChamberCard label="Senate" forecast={data.chambers.Senate} tally={tallySeats(data, 'senate')} />
        <GovernorCard data={data} />
        <ChamberCard label="House" forecast={data.chambers.House} tally={tallySeats(data, 'house')} />
      </div>

      <h2 className="results-h2">Across every race</h2>

      <div className="results-tiles">
        <Tile big={pct(overall.right, overall.decided)} label="Winners called correctly" sub={`${overall.right} of ${overall.decided} races`} />
        <Tile
          big={pct(overall.competitiveRight, overall.competitive)}
          label="Competitive races"
          sub={`${overall.competitiveRight} of ${overall.competitive} not rated Solid`}
        />
        <Tile
          big={overall.bias != null ? formatResultMargin(overall.bias, 'D') : '—'}
          label="Average shift from forecast"
          sub={overall.bias != null ? `results ran ${Math.abs(overall.bias).toFixed(1)} pts ${overall.bias > 0 ? 'bluer' : 'redder'} than forecast` : ''}
        />
      </div>

      <div className="results-table-wrap">
      <table className="results-table results-table--summary">
        <thead>
          <tr><th>Chamber</th><th>Called</th><th>Competitive</th></tr>
        </thead>
        <tbody>
          {CHAMBERS.map(c => {
            const s = summarize(rows.filter(r => chamberOf(r.id) === c.id));
            return (
              <tr key={c.id}>
                <td>{c.label}</td>
                <td>{s.right}/{s.decided} <span className="results-muted">({pct(s.right, s.decided)})</span></td>
                <td>{s.competitiveRight}/{s.competitive}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      </div>

      <h2 className="results-h2">Best calls</h2>
      <ComparisonTable rows={bestCalls} raceLabel={raceLabel} />

      <h2 className="results-h2">Races we got wrong</h2>
      {misses.length === 0 ? <p>None so far.</p> : (
        <ComparisonTable rows={misses} raceLabel={raceLabel} />
      )}

      <h2 className="results-h2">Every race</h2>
      <div className="results-controls">
        {CHAMBERS.map(c => (
          <button
            key={c.id}
            onClick={() => update({ chamber: c.id, state: '' })}
            className={`results-tab ${tab === c.id ? 'results-tab--active' : ''}`}
          >
            {c.label}
          </button>
        ))}
        {!isDesktop && <div style={{ flexBasis: '100%', height: 0 }} />}
        <select value={selectedState} onChange={e => update({ state: e.target.value })} aria-label="Filter by state">
          <option value="">All states</option>
          {stateOptions.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
        <select value={sort} onChange={e => update({ sort: e.target.value as Sort })} aria-label="Sort races">
          <option value="race">Sort: race</option>
          <option value="miss">Sort: biggest miss</option>
        </select>
      </div>
      <ComparisonTable rows={shown} raceLabel={raceLabel} />

      <p className="results-note">
        Margins compare the top Democratic or independent candidate with the top Republican, the
        same way the forecast measured them. Races with candidates from only one side on the ballot
        count toward winners called but have no margin to compare. Results are from each
        race&rsquo;s reported count.
      </p>
    </div>
  );
};

type Tally = ReturnType<typeof tallySeats>;

// Chamber control and seat totals: the simulation's final call beside the result.
const ChamberCard = ({ label, forecast, tally }: { label: string; forecast: ChamberForecast; tally: Tally }) => {
  const demSeats = forecast.seatsNotUp.dem + tally.dem;
  const repSeats = forecast.seatsNotUp.rep + tally.rep;
  const favoredDem = forecast.demControlProbability >= 0.5;
  const favoredOdds = Math.round((favoredDem ? forecast.demControlProbability : 1 - forecast.demControlProbability) * 100);
  // Control is settled once the undecided races can no longer change it.
  const demControl = demSeats >= forecast.majority ? true
    : demSeats + tally.undecided < forecast.majority ? false : null;
  const called = demControl == null ? null : demControl === favoredDem;

  return (
    <section className="chamber-card">
      <div className="chamber-card__head">
        <h3>{label}</h3>
        <CallBadge called={called} />
      </div>
      <div className="chamber-card__row">
        <div>
          <div className="chamber-card__label">Forecast</div>
          <div className="chamber-card__big" style={{ color: favoredDem ? '#123f8f' : '#9c150b' }}>
            {favoredDem ? 'D' : 'R'} {favoredOdds}%
          </div>
        </div>
        <div>
          <div className="chamber-card__label">Result</div>
          <div className="chamber-card__big" style={{ color: demControl == null ? '#333333' : demControl ? '#123f8f' : '#9c150b' }}>
            {demControl == null ? 'Pending' : demControl ? 'D control' : 'R control'}
          </div>
          <div className="chamber-card__sub">{forecast.majority} seats for a majority</div>
        </div>
      </div>
      <div className="chamber-card__row">
        <div>
          <div className="chamber-card__label">Forecast seats</div>
          <div className="chamber-card__seats">
            <span style={{ color: '#123f8f' }}>D {forecast.expectedDemSeats.toFixed(1)}</span>
            {' – '}
            <span style={{ color: '#9c150b' }}>R {(forecast.totalSeats - forecast.expectedDemSeats).toFixed(1)}</span>
          </div>
        </div>
        <div>
          <div className="chamber-card__label">Actual seats</div>
          <div className="chamber-card__seats">
            <span style={{ color: '#123f8f' }}>D {demSeats}</span>
            {' – '}
            <span style={{ color: '#9c150b' }}>R {repSeats}</span>
          </div>
          {tally.undecided > 0 && <div className="chamber-card__sub">{tally.undecided} still undecided</div>}
        </div>
      </div>
    </section>
  );
};

// Governors have no chamber to control: compare the governorships each side was favored to win.
const GovernorCard = ({ data }: { data: ElectionResults }) => {
  const t = tallySeats(data, 'governors');
  const total = t.dem + t.rep + t.undecided;
  return (
    <section className="chamber-card">
      <div className="chamber-card__head">
        <h3>Governors</h3>
      </div>
      <div className="chamber-card__row">
        <div>
          <div className="chamber-card__label">Forecast</div>
          <div className="chamber-card__seats">
            <span style={{ color: '#123f8f' }}>D {t.forecastDem}</span>
            {' – '}
            <span style={{ color: '#9c150b' }}>R {total - t.forecastDem}</span>
          </div>
        </div>
        <div>
          <div className="chamber-card__label">Result</div>
          <div className="chamber-card__seats">
            <span style={{ color: '#123f8f' }}>D {t.dem}</span>
            {' – '}
            <span style={{ color: '#9c150b' }}>R {t.rep}</span>
          </div>
          <div className="chamber-card__sub">
            of {total} governorships up{t.undecided > 0 ? `, ${t.undecided} undecided` : ''}
          </div>
        </div>
      </div>
    </section>
  );
};

const Tile = ({ big, label, sub }: { big: string; label: string; sub: string }) => (
  <div className="results-tile">
    <div className="results-tile__big">{big}</div>
    <div className="results-tile__label">{label}</div>
    <div className="results-tile__sub">{sub}</div>
  </div>
);

const ComparisonTable = ({ rows, raceLabel }: { rows: Row[]; raceLabel: (id: string) => string }) => (
  <div className="results-table-wrap">
    <table className="results-table">
      <thead>
        <tr><th>Race</th><th>Forecast</th><th>Result</th><th>Miss</th><th>Call</th></tr>
      </thead>
      <tbody>
        {rows.map(({ id, result, called, error }) => {
          const letter = challengerLetter(result);
          return (
            <tr key={id}>
              <td><Link to={`/race/${id}`}>{raceLabel(id)}</Link></td>
              <td style={{ color: marginColor(result.predictedMargin, letter), fontWeight: 600 }}>
                {formatResultMargin(result.predictedMargin, letter)}
              </td>
              <td style={{ color: result.actualMargin != null ? marginColor(result.actualMargin, letter) : undefined, fontWeight: 600 }}>
                {result.actualMargin != null
                  ? formatResultMargin(result.actualMargin, letter, true)
                  : <span className="results-muted">{result.winner ? `${result.winner} won` : result.status === 'runoff' ? 'Runoff pending' : 'Pending'}</span>}
              </td>
              <td>{error != null ? formatError(error, letter) : <span className="results-muted">—</span>}</td>
              <td><CallBadge called={called} /></td>
            </tr>
          );
        })}
      </tbody>
    </table>
  </div>
);
