import { useQuery } from '@tanstack/react-query';
import { Party } from '../types';

// The 2026 scorecard: each race's final forecast (the Election Day snapshot) beside its reported
// result. Static — built by tools/fetch_results.py into public/data/results.json — since the
// forecast is frozen and results only change as late counts come in.

export interface ResultCandidate {
  name: string;
  party: Party;
  pct: number | null;
  won: boolean;
}

export type ResultStatus =
  | 'final'         // winner called, vote shares posted
  | 'uncalled'      // vote shares posted, no winner called yet
  | 'runoff'        // went to a runoff that hasn't been held yet
  | 'winnerOnly'    // winner called, vote shares not posted yet
  | 'noRepublican'  // no Republican on the ballot (e.g. a Dem-vs-Dem top two)
  | 'noChallenger'; // no non-Republican on the ballot

export interface RaceResult {
  // Margins are the leading non-Republican minus the leading Republican, in points — the same
  // challenger-side convention as the forecast's expected Dem margin.
  predictedMargin: number;
  predictedDemWinProbability: number;
  actualMargin: number | null;
  winner: string | null;
  winnerParty: Party | null;
  status: ResultStatus;
  candidates: ResultCandidate[];
}

// The Election Day run of the chamber simulation, plus how it counts seats.
export interface ChamberForecast {
  demControlProbability: number;
  expectedDemSeats: number;
  demSeatsLow: number;   // 10th percentile
  demSeatsHigh: number;  // 90th percentile
  seatsNotUp: { dem: number; rep: number };
  majority: number;      // Dem seats needed for control (a 50-50 Senate goes to the VP's party, R)
  totalSeats: number;
}

export interface ElectionResults {
  asOf: string;
  forecastDate: string;
  chambers: { Senate: ChamberForecast; House: ChamberForecast };
  races: Record<string, RaceResult>;
}

export type Chamber = 'senate' | 'house' | 'governors';

export const chamberOf = (raceId: string): Chamber =>
  raceId.includes('-SEN') ? 'senate' : raceId.includes('-GOV') ? 'governors' : 'house';

// Seats won by each side across a chamber's races (Dem side includes independents, as the
// simulation counts them), plus races not decided yet.
export const tallySeats = (results: ElectionResults, chamber: Chamber) => {
  let dem = 0, rep = 0, undecided = 0, forecastDem = 0, expectedDem = 0;
  Object.entries(results.races).forEach(([id, r]) => {
    if (chamberOf(id) !== chamber) return;
    const won = actualChallengerWins(r);
    if (won == null) undecided++;
    else if (won) dem++;
    else rep++;
    if (predictedChallengerWins(r)) forecastDem++;
    expectedDem += r.predictedDemWinProbability;
  });
  return { dem, rep, undecided, forecastDem, expectedDem };
};

// Explanations for misses with a known cause outside the model, shown on the race's page.
export const RESULT_NOTES: Record<string, string> = {
  'MO-05-2026':
    'This forecast was built on the wrong district. It used Missouri’s 2025 redrawn map, which ' +
    'split Kansas City and rated this seat R+9, but the result shows the election was held under ' +
    'the previous lines, where the Kansas City district is safely Democratic. With no polling in the ' +
    'race to correct it, the forecast rested entirely on that partisan lean.',
};

export const useElectionResults = () =>
  useQuery({
    queryKey: ['election-results'],
    queryFn: async (): Promise<ElectionResults> => {
      const res = await fetch('/data/results.json');
      if (!res.ok) throw new Error(`results.json: ${res.status}`);
      return res.json();
    },
    staleTime: Infinity,
  });

// Whether the forecast had the challenger (non-Republican) ahead — on the margin as displayed,
// so a forecast shown as "D+0.1" counts as a D call.
export const predictedChallengerWins = (r: RaceResult): boolean => {
  const rounded = Math.round(r.predictedMargin * 10) / 10;
  return rounded !== 0 ? rounded > 0 : r.predictedMargin >= 0;
};

// Whether the challenger actually won; null until that's settled — an uncalled race counts only once
// its count is out of reach (2+ points), so a near-tie isn't scored before it's decided.
export const actualChallengerWins = (r: RaceResult): boolean | null => {
  if (r.winnerParty) return r.winnerParty !== Party.Republican;
  // One side alone on the ballot (e.g. a Dem-vs-Dem top two) is decided whoever wins.
  if (r.status === 'noRepublican') return true;
  if (r.status === 'noChallenger') return false;
  if (r.status === 'uncalled' && r.actualMargin != null && Math.abs(r.actualMargin) >= 2) return r.actualMargin > 0;
  return null;
};

// Whether the forecast picked the winning side; null when the race has no winner yet.
export const calledCorrectly = (r: RaceResult): boolean | null => {
  const actual = actualChallengerWins(r);
  return actual == null ? null : actual === predictedChallengerWins(r);
};

// How far the result landed from the forecast, in points (positive: the challenger did better than
// forecast). Null when the race has no comparable margin.
export const marginError = (r: RaceResult): number | null =>
  r.actualMargin == null ? null : r.actualMargin - r.predictedMargin;

// The challenger's letter for a result: "I" when the leading non-Republican is an independent.
export const challengerLetter = (r: RaceResult): 'D' | 'I' => {
  const top = r.candidates
    .filter(c => c.party !== Party.Republican)
    .sort((a, b) => (b.pct ?? 0) - (a.pct ?? 0))[0];
  return top?.party === Party.Independent ? 'I' : 'D';
};

const fmt = (n: number) => (Number.isInteger(n) ? n.toString() : n.toFixed(1));

// "D+2.6" / "R+4" / "I+1.2". A forecast never shows "EVEN" (a margin that rounds to zero shows as
// 0.1 on its true side); an actual result can.
export const formatResultMargin = (margin: number, letter: 'D' | 'I', allowEven = false): string => {
  const rounded = Math.round(margin * 10) / 10;
  if (rounded === 0 && allowEven) return 'EVEN';
  const challengerLeads = rounded !== 0 ? rounded > 0 : margin >= 0;
  const abs = Math.max(Math.abs(rounded), 0.1);
  return challengerLeads ? `${letter}+${fmt(abs)}` : `R+${fmt(abs)}`;
};

// "3.2 pts toward D" — which way the result moved from the forecast.
export const formatError = (error: number, letter: 'D' | 'I'): string => {
  const abs = Math.round(Math.abs(error) * 10) / 10;
  if (abs === 0) return 'Exact';
  return `${fmt(abs)} pts toward ${error > 0 ? letter : 'R'}`;
};

export const marginColor = (margin: number, letter: 'D' | 'I'): string =>
  margin > 0 ? (letter === 'I' ? '#b8860b' : '#123f8f') : margin < 0 ? '#9c150b' : '#666666';
