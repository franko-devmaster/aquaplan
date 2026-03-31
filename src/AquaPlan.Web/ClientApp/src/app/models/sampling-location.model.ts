export interface SamplingLocationDto {
  id: string;
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  isActive: boolean;
  distributorId: string;
  distributorName: string | null;
  createdAt: string;
}

export interface SamplingLocationCreateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  distributorId: string;
}

export interface SamplingLocationUpdateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  isActive: boolean;
}
