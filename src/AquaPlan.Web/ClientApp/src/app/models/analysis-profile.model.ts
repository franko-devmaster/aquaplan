export type AnalysisCategory = 'Bacteriology' | 'Chemistry' | 'Physical' | 'Other';

export interface AnalysisProfileDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
  isActive: boolean;
  createdAt: string;
}

export interface AnalysisProfileListDto {
  id: string;
  code: string;
  name: string;
  category: AnalysisCategory;
  isActive: boolean;
}

export interface AnalysisProfileAddDto {
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
}

export interface AnalysisProfileUpdateDto {
  code: string;
  name: string;
  description: string | null;
  category: AnalysisCategory;
  isActive: boolean;
}

export interface AnalysisProfileFilteringInputDto {
  search?: string;
  category?: AnalysisCategory;
  isActive?: boolean;
}
