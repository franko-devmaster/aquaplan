export interface SectorDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface SectorListDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface SectorAddDto {
  name: string;
  code: string;
  description: string | null;
}

export interface SectorUpdateDto {
  name: string;
  code: string;
  description: string | null;
}

export interface SectorFilteringInputDto {
  name?: string;
  isActive?: boolean;
}
