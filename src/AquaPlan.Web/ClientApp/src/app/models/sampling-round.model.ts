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

export interface SamplingRoundListDto {
  id: string;
  name: string;
  description: string | null;
  deadline: string;
  status: SamplingRoundStatus;
  // AQ-410 — backend returns preleveurId/preleveurName (was samplerId/samplerName, never populated).
  preleveurId: string | null;
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  distributorShortName: string | null;
  notes: string | null;
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
  samplingLocationId: string | null;
  samplingLocationName: string | null;
  samplingLocationCode: string | null;
  sectorName: string | null;
  analysisProgramNames: string[];
  status: string;
  originalSamplingLocationId: string | null;
  originalSamplingLocationName: string | null;
  locationReplacementReason: string | null;
  samplerComment: string | null;
  notes: string | null;
  // AQ-414 — indicator flags populated by the backend for the round detail table.
  hasMandatorNote: boolean;
  hasPreleveurNote: boolean;
  hasReplacedLocation: boolean;
  preleveurNote: string | null;
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
  // AQ-410 — backend returns preleveurName (was samplerName, never populated).
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  distributorShortName: string | null;
  notes: string | null;
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
  // AQ-411 — filter rounds assigned to a specific preleveur (used by "Mes tournées" tile/deep-link).
  preleveurId?: string;
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
