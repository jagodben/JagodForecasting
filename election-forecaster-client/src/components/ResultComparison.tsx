import {
  RaceResult, calledCorrectly, challengerLetter, formatResultMargin, marginColor,
} from '../utils/results';
import './ResultComparison.css';

// Final forecast beside the actual result for one race: the race page's headline card, and a
// compact one-line form for lists.
export const ResultComparison = ({ result, compact = false, note }: {
  result: RaceResult;
  compact?: boolean;
  note?: string; // why the forecast missed, when the cause is known (RESULT_NOTES)
}) => {
  const letter = challengerLetter(result);
  const called = calledCorrectly(result);

  const forecast = formatResultMargin(result.predictedMargin, letter);
  const actual = result.actualMargin != null
    ? formatResultMargin(result.actualMargin, letter, true)
    : result.winner ? `${result.winner} won` : result.status === 'runoff' ? 'Runoff pending' : 'Not called yet';

  if (compact) {
    return (
      <div className="result-compact">
        <span>Forecast <strong style={{ color: marginColor(result.predictedMargin, letter) }}>{forecast}</strong></span>
        <span>Result <strong style={{ color: result.actualMargin != null ? marginColor(result.actualMargin, letter) : undefined }}>{actual}</strong></span>
        <CallBadge called={called} />
      </div>
    );
  }

  return (
    <section className="result-card" aria-label="Forecast versus result">
      <div className="result-card__head">
        <span className="result-card__title">Forecast vs. result</span>
        <CallBadge called={called} />
      </div>
      <div className="result-card__grid">
        <div>
          <div className="result-card__label">Final forecast</div>
          <div className="result-card__value" style={{ color: marginColor(result.predictedMargin, letter) }}>{forecast}</div>
        </div>
        <div>
          <div className="result-card__label">Result</div>
          <div
            className="result-card__value"
            style={{ color: result.actualMargin != null ? marginColor(result.actualMargin, letter) : '#333333' }}
          >
            {actual}
          </div>
          {statusNote(result) && <div className="result-card__sub">{statusNote(result)}</div>}
        </div>
      </div>
      {note && <p className="result-card__note">{note}</p>}
    </section>
  );
};

const statusNote = (r: RaceResult): string => {
  switch (r.status) {
    case 'noRepublican': return 'No Republican on the ballot';
    case 'noChallenger': return 'Republican unopposed';
    case 'winnerOnly': return 'Vote shares still being reported';
    case 'uncalled': return 'Not called yet';
    case 'runoff': return 'Runoff still to come';
    default: return '';
  }
};

export const CallBadge = ({ called }: { called: boolean | null }) => {
  if (called == null) return <span className="call-badge call-badge--pending">Pending</span>;
  return called
    ? <span className="call-badge call-badge--right">✓ Called it</span>
    : <span className="call-badge call-badge--wrong">✗ Missed</span>;
};
