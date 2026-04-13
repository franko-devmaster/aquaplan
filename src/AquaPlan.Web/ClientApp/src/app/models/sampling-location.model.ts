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
  sectorId: string | null;
  sectorName: string | null;
  createdAt: string;
}

export interface SamplingLocationCreateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  distributorId: string;
  sectorId: string | null;
}

export interface SamplingLocationUpdateDto {
  name: string;
  locationCode: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  isActive: boolean;
  sectorId: string | null;
}

export interface SamplingLocationListDto {
  items: SamplingLocationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SamplingLocationFilteringInputDto {
  distributorId?: string;
  sectorId?: string;
  search?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ToggleStatusResultDto {
  location: SamplingLocationDto;
  hasActiveReferences: boolean;
  warning: string | null;
}
