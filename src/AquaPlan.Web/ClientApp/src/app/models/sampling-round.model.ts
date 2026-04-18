export enum SamplingRoundStatus {
  Draft = 'Draft',
  Assigned = 'Assigned',
  InProgress = 'InProgress',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
}

export const SamplingRoundStatusLabels: Record<SamplingRoundStatus, string> = {
  [SamplingRoundStatus.Draft]: 'samplingRounds.status.draft',
  [SamplingRoundStatus.Assigned]: 'samplingRounds.status.assigned',
  [SamplingRoundStatus.InProgress]: 'samplingRounds.status.inProgress',
  [SamplingRoundStatus.Completed]: 'samplingRounds.status.completed',
  [SamplingRoundStatus.Cancelled]: 'samplingRounds.status.cancelled',
};

export const SamplingRoundStatusColors: Record<SamplingRoundStatus, string> = {
  [SamplingRoundStatus.Draft]: '#455A64',
  [SamplingRoundStatus.Assigned]: '#0277BD',
  [SamplingRoundStatus.InProgress]: '#1565C0',
  [SamplingRoundStatus.Completed]: '#2E7D32',
  [SamplingRoundStatus.Cancelled]: '#C62828',
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
  // AQ-370 — offline lock fields
  isLocked: boolean;
  lockedById: string | null;
  lockedByName: string | null;
  lockedAt: string | null;
}

export interface SamplingRoundOrderDto {
  id: string;
  orderNumber: string;
  sortOrder: number;
  samplingLocationName: string;
  samplingLocationCode: string;
  sectorName: string | null;
  analysisProgramNames: string[];
  status: string;
  hasLocationReplacement: boolean;
  locationReplacementReason: string | null;
  originalLocationName: string | null;
  samplerComment: string | null;
  mandataireNotes: string | null;
}

export interface RoundContainerSummaryDto {
  containerId: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  count: number;
}

export interface SamplingRoundDetailDto {
  id: string;
  name: string;
  description: string | null;
  deadline: string;
  status: SamplingRoundStatus;
  preleveurId: string | null;
  samplerName: string | null;
  distributorId: string;
  distributorName: string;
  distributorShortName: string | null;
  orders: SamplingRoundOrderDto[];
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
  containerSummary: RoundContainerSummaryDto[];
  // AQ-370 — offline lock fields
  isLocked: boolean;
  lockedById: string | null;
  lockedByName: string | null;
  lockedAt: string | null;
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
