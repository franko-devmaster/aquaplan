export type AnalysisCategory = 'Bacteriology' | 'Chemistry' | 'Physical' | 'Other';

export interface AnalysisProfileDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
  isActive: boolean;
  containerId: string;
  containerCode: string;
  containerName: string;
  createdAt: string;
}

export interface AnalysisProfileListDto {
  id: string;
  code: string;
  name: string;
  category: AnalysisCategory;
  isActive: boolean;
  containerId: string;
  containerCode: string;
}

export interface AnalysisProfileAddDto {
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
  containerId: string;
}

export interface AnalysisProfileUpdateDto {
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
  isActive: boolean;
  containerId: string;
}

export interface AnalysisProfileFilteringInputDto {
  search?: string;
  category?: AnalysisCategory;
  isActive?: boolean;
}
