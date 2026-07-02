export enum RaceType {
  Senate = 'Senate',
  Governor = 'Governor',
  House = 'House'
}

export enum Party {
  Democrat = 'Democrat',
  Republican = 'Republican',
  Independent = 'Independent',
  Libertarian = 'Libertarian',
  Green = 'Green',
  Other = 'Other'
}

export enum RaceRating {
  SolidDem = 'SolidDem',
  LikelyDem = 'LikelyDem',
  LeanDem = 'LeanDem',
  TiltDem = 'TiltDem',
  TiltRep = 'TiltRep',
  LeanRep = 'LeanRep',
  LikelyRep = 'LikelyRep',
  SolidRep = 'SolidRep'
}

export interface Candidate {
  id: string;
  name: string;
  party: Party;
  isIncumbent: boolean;
}

export interface Forecast {
  candidateId: string;
  candidateName: string;
  winProbability: number;
  projectedVoteShare: number;
}

export interface Race {
  id: string;
  stateId: string;
  type: RaceType;
  districtNumber?: number;
  rating: RaceRating;
  candidates: Candidate[];
  forecasts: Forecast[];
  isSpecialElection: boolean;
  year: number;
}

export interface District {
  id: string;
  stateId: string;
  number: number;
  rating: RaceRating;
  houseRace?: Race;
}

export interface State {
  id: string;
  name: string;
  electoralVotes: number;
  congressionalDistricts: number;
  overallRating: RaceRating;
  races: Race[];
  districts: District[];
}

export interface StateSummary {
  id: string;
  name: string;
  electoralVotes: number;
  congressionalDistricts: number;
  overallRating: RaceRating;
  raceCount: number;
}

export interface ForecastInputs {
  marketOdds: number | null;
  pollingAverage: number | null;
  fundamentalsPrediction: number | null;
  approvalAdjustment: number | null;
  marketWeight: number;
  pollingWeight: number;
  fundamentalsWeight: number;
  approvalWeight: number;
  marketLastUpdated: string | null;
  pollingLastUpdated: string | null;
  pollCount: number | null;
}

export interface HistoricalDataPoint {
  date: string;
  demWinProbability: number;
  repWinProbability: number;
  demVoteShare: number | null;
  repVoteShare: number | null;
}

export interface DetailedForecast {
  raceId: string;
  demWinProbability: number;
  repWinProbability: number;
  demVoteShare: number;
  repVoteShare: number;
  confidence: number;
  lastUpdated: string;
  inputs: ForecastInputs;
  history: HistoricalDataPoint[];
}

export interface PollingAverage {
  raceId: string;
  demPercent: number;
  repPercent: number;
  margin: number;
  pollCount: number;
  latestPollDate: string | null;
  averageSampleSize: number | null;
  confidence: number;
}

export interface Poll {
  pollster: string;
  date: string;
  sampleSize: number | null;
  population: string | null;
  demPercent: number;
  repPercent: number;
  margin: number;
  isPartisan: boolean;
}

export interface RacePolls {
  raceId: string;
  average: PollingAverage | null;
  polls: Poll[];
}
