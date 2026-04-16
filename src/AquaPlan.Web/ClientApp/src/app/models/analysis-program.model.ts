import { AnalysisProfileListDto } from './analysis-profile.model';

export interface ProgramContainerDto {
  containerId: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  profileCount: number;
}

export interface AnalysisProgramDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  profiles: AnalysisProfileListDto[];
  requiredContainers: ProgramContainerDto[];
}

export interface AnalysisProgramListDto {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  profileCount: number;
}

export interface AnalysisProgramAddDto {
  code: string;
  name: string;
  description: string | null;
}

export interface AnalysisProgramUpdateDto {
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface AnalysisProgramAddProfilesDto {
  profileIds: string[];
}

export interface AnalysisProgramFilteringInputDto {
  search?: string;
  isActive?: boolean;
}
