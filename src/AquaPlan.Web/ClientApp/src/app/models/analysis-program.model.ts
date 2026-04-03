import { AnalysisProfileListDto } from './analysis-profile.model';

export interface AnalysisProgramDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  profiles: AnalysisProfileListDto[];
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
