export const WEATHER_OPTIONS = ['dry', 'light_rain', 'heavy_rain'] as const;

export type WeatherOption = typeof WEATHER_OPTIONS[number];

export interface SamplingContainerDto {
  id: string;
  containerId: string;
  barcode: string | null;
  barcodeScannedAt: string | null;
}

export interface SamplingContainerInputDto {
  containerId: string;
  barcode: string | null;
  barcodeScannedAt?: string | null;
}

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
  barcodeScannedAt: string | null;
  isValidated: boolean;
  validatedAt: string | null;
  createdAt: string;
  containers: SamplingContainerDto[];
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
  containers?: SamplingContainerInputDto[];
}

export interface RequiredContainerDto {
  containerId: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  existingBarcode: string | null;
}
