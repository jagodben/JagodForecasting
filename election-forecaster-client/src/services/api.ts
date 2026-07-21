import axios from 'axios';
import { State, StateSummary, Race, RaceType, DetailedForecast, RacePolls, ChamberHistoryPoint, SitePoll } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL ||
  (import.meta.env.DEV ? 'http://localhost:5000/api' : 'https://api.jagodforecasting.com/api');

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

export const forecastApi = {
  getByRaceId: async (raceId: string): Promise<DetailedForecast> => {
    const response = await api.get<DetailedForecast>(`/forecast/${raceId}`);
    return response.data;
  },

  getPolls: async (raceId: string): Promise<RacePolls> => {
    const response = await api.get<RacePolls>(`/forecast/${raceId}/polls`);
    return response.data;
  },

  getAll: async (type?: RaceType): Promise<DetailedForecast[]> => {
    const params = type ? { type } : {};
    const response = await api.get<DetailedForecast[]>('/forecast', { params });
    return response.data;
  },

  getAllPolls: async (): Promise<SitePoll[]> => {
    const response = await api.get<SitePoll[]>('/forecast/polls');
    return response.data;
  },

  getChamberHistory: async (chamberType: string): Promise<ChamberHistoryPoint[]> => {
    const response = await api.get<ChamberHistoryPoint[]>(`/forecast/chamber/${chamberType}/history`);
    return response.data;
  },
};
