export const WEATHER_OPTIONS = ['dry', 'light_rain', 'heavy_rain'] as const;

export type WeatherOption = typeof WEATHER_OPTIONS[number];

export interface SamplingDto {
  id: string;
  orderId: string;
  preleveurId: string;
  preleveurName: string;
  samplingDateTime: string;
  temperature: number | null;
  weather: string | null;
  notes: string | null;
  hasWaterSoftener: boolean | null;
  isChlorinated: boolean;
  sampleBarcode: string | null;
  isValidated: boolean;
  validatedAt: string | null;
  createdAt: string;
}

export interface SamplingCreateDto {
  orderId: string;
  samplingDateTime: string;
  temperature: number | null;
  weather: string | null;
  notes: string | null;
  hasWaterSoftener: boolean | null;
  isChlorinated: boolean;
  sampleBarcode: string | null;
}
