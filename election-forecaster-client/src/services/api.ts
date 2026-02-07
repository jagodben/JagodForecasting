import axios from 'axios';
import { State, StateSummary, Race, District, RaceType, DetailedForecast } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL ||
  (import.meta.env.DEV ? 'http://localhost:5000/api' : 'https://electionforecaster.onrender.com/api');

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const statesApi = {
  getAll: async (): Promise<StateSummary[]> => {
    const response = await api.get<StateSummary[]>('/states');
    return response.data;
  },

  getById: async (id: string): Promise<State> => {
    const response = await api.get<State>(`/states/${id}`);
    return response.data;
  },

  getRaces: async (id: string): Promise<Race[]> => {
    const response = await api.get<Race[]>(`/states/${id}/races`);
    return response.data;
  },

  getDistricts: async (id: string): Promise<District[]> => {
    const response = await api.get<District[]>(`/states/${id}/districts`);
    return response.data;
  },
};

export const racesApi = {
  getAll: async (type?: RaceType): Promise<Race[]> => {
    const params = type ? { type } : {};
    const response = await api.get<Race[]>('/races', { params });
    return response.data;
  },

  getById: async (id: string): Promise<Race> => {
    const response = await api.get<Race>(`/races/${id}`);
    return response.data;
  },
};

export const districtsApi = {
  getById: async (id: string): Promise<District> => {
    const response = await api.get<District>(`/districts/${id}`);
    return response.data;
  },
};

export interface ChamberMarketOdds {
  chamber: string;
  demOdds: number;
  repOdds: number;
  timestamp: string;
  source: string;
}

export const forecastApi = {
  getByRaceId: async (raceId: string): Promise<DetailedForecast> => {
    const response = await api.get<DetailedForecast>(`/forecast/${raceId}`);
    return response.data;
  },

  getAll: async (): Promise<DetailedForecast[]> => {
    const response = await api.get<DetailedForecast[]>('/forecast');
    return response.data;
  },

  getChamberMarketOdds: async (chamberType: string): Promise<ChamberMarketOdds | null> => {
    try {
      const response = await api.get<ChamberMarketOdds>(`/forecast/chamber/${chamberType}/market-odds`);
      return response.data;
    } catch {
      return null;
    }
  },
};
