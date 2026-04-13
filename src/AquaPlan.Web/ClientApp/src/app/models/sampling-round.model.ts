export enum SamplingRoundStatus {
  Draft = 0,
  Assigned = 1,
  Validated = 2,
  InProgress = 3,
  Completed = 4,
  Cancelled = 5,
}

export const SamplingRoundStatusLabels: Record<SamplingRoundStatus, string> = {
  [SamplingRoundStatus.Draft]: 'samplingRounds.status.draft',
  [SamplingRoundStatus.Assigned]: 'samplingRounds.status.assigned',
  [SamplingRoundStatus.Validated]: 'samplingRounds.status.validated',
  [SamplingRoundStatus.InProgress]: 'samplingRounds.status.inProgress',
  [SamplingRoundStatus.Completed]: 'samplingRounds.status.completed',
  [SamplingRoundStatus.Cancelled]: 'samplingRounds.status.cancelled',
};

export const SamplingRoundStatusColors: Record<SamplingRoundStatus, string> = {
  [SamplingRoundStatus.Draft]: '#9E9E9E',
  [SamplingRoundStatus.Assigned]: '#1976D2',
  [SamplingRoundStatus.Validated]: '#00897B',
  [SamplingRoundStatus.InProgress]: '#FF9800',
  [SamplingRoundStatus.Completed]: '#388E3C',
  [SamplingRoundStatus.Cancelled]: '#D32F2F',
};

export interface SamplingRoundListDto {
  id: string;
  name: string;
  description: string | null;
  deadline: string;
  status: SamplingRoundStatus;
  samplerId: string | null;
  samplerName: string | null;
  distributorId: string;
  distributorName: string;
  distributorShortName: string | null;
  orderCount: number;
  completedOrderCount: number;
  createdAt: string;
}

export interface SamplingRoundOrderDto {
  id: string;
  orderNumber: string;
  sortOrder: number;
  samplingLocationName: string;
  samplingLocationCode: string;
  sectorName: string | null;
  analysisProfileNames: string[];
  status: string;
  hasLocationReplacement: boolean;
  locationReplacementReason: string | null;
  originalLocationName: string | null;
  samplerComment: string | null;
  mandataireNotes: string | null;
}

export interface SamplingRoundDetailDto {
  id: string;
  name: string;
  description: string | null;
  deadline: string;
  status: SamplingRoundStatus;
  samplerId: string | null;
  samplerName: string | null;
  distributorId: string;
  distributorName: string;
  distributorShortName: string | null;
  orders: SamplingRoundOrderDto[];
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface SamplingRoundCreateDto {
  name: string;
  description: string | null;
  deadline: string;
  distributorId: string;
}

export interface SamplingRoundUpdateDto {
  name: string;
  description: string | null;
  deadline: string;
}

export interface SamplingRoundAssignDto {
  preleveurId: string;
}

export interface SamplingRoundFilterDto {
  statuses?: SamplingRoundStatus[];
  distributorId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface SamplingRoundPagedResultDto {
  items: SamplingRoundListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LocationReplacementDto {
  newSamplingLocationId: string;
  reason: string;
}

export interface SamplerCommentDto {
  comment: string;
}
