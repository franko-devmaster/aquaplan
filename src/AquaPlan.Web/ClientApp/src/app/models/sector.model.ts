export interface SectorDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  distributorId: string;
  distributorName: string | null;
  createdAt: string;
}

export interface SectorListDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  distributorId: string;
  distributorName: string | null;
  createdAt: string;
}

export interface SectorAddDto {
  name: string;
  code: string;
  description: string | null;
  distributorId: string;
}

export interface SectorUpdateDto {
  name: string;
  code: string;
  description: string | null;
}

export interface SectorFilteringInputDto {
  name?: string;
  isActive?: boolean;
  distributorId?: string;
}
