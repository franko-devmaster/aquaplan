export interface DistributorDto {
  id: string;
  name: string;
  cantonRegion: string | null;
  distributionNetwork: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface DistributorListDto {
  id: string;
  name: string;
  cantonRegion: string | null;
  distributionNetwork: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface DistributorAddDto {
  name: string;
  cantonRegion: string | null;
  distributionNetwork: string | null;
}

export interface DistributorUpdateDto {
  name: string;
  cantonRegion: string | null;
  distributionNetwork: string | null;
}

export interface DistributorFilteringInputDto {
  name?: string;
  isActive?: boolean;
}
