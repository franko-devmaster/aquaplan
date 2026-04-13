export interface DistributorDto {
  id: string;
  name: string;
  shortName: string | null;
  cantonRegion: string | null;
  distributionNetwork: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface DistributorListDto {
  id: string;
  name: string;
  shortName: string | null;
  cantonRegion: string | null;
  distributionNetwork: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface DistributorAddDto {
  name: string;
  shortName: string | null;
  cantonRegion: string | null;
  distributionNetwork: string | null;
}

export interface DistributorUpdateDto {
  name: string;
  shortName: string | null;
  cantonRegion: string | null;
  distributionNetwork: string | null;
}

export interface DistributorFilteringInputDto {
  name?: string;
  isActive?: boolean;
}
