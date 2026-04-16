export interface SamplingLocationDto {
  id: string;
  name: string;
  locationCode: string;
  description: string | null;
  address: string | null;
  accessDescription: string | null;
  isActive: boolean;
  distributorId: string;
  distributorName: string | null;
  sectorId: string;
  sectorName: string | null;
  isValidated: boolean;
  createdAt: string;
  canDelete?: boolean;
}

export interface SamplingLocationCreateDto {
  name: string;
  locationCode: string;
  description: string | null;
  address: string | null;
  accessDescription: string | null;
  distributorId: string;
  sectorId: string;
}

export interface SamplingLocationUpdateDto {
  name: string;
  locationCode: string;
  description: string | null;
  address: string | null;
  accessDescription: string | null;
  isActive: boolean;
  sectorId: string;
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
