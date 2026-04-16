export interface ContainerDto {
  id: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  color: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ContainerListDto {
  id: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  color: string;
  isActive: boolean;
}

export interface ContainerAddDto {
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  color: string;
}

export interface ContainerUpdateDto {
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  color: string;
  isActive: boolean;
}

export interface ContainerFilteringInputDto {
  search?: string;
  isActive?: boolean;
}
