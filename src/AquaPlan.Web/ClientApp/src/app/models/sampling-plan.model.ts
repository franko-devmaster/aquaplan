export enum SamplingPlanStatus {
  Draft = 0,
  Submitted = 1,
  Validated = 2,
  Rejected = 3,
}

export const SamplingPlanStatusLabels: Record<SamplingPlanStatus, string> = {
  [SamplingPlanStatus.Draft]: 'samplingPlans.status.draft',
  [SamplingPlanStatus.Submitted]: 'samplingPlans.status.submitted',
  [SamplingPlanStatus.Validated]: 'samplingPlans.status.validated',
  [SamplingPlanStatus.Rejected]: 'samplingPlans.status.rejected',
};

export interface SamplingPlanItemDto {
  id: string;
  samplingLocationId: string;
  samplingLocationName: string;
  samplingLocationCode: string;
  analysisProfileId: string;
  analysisProfileCode: string;
  analysisProfileName: string;
  frequencyPerYear: number;
  plannedMonths: number[];
}

export interface SamplingPlanListDto {
  id: string;
  year: number;
  status: SamplingPlanStatus;
  distributorId: string;
  distributorName: string;
  createdById: string;
  createdByName: string | null;
  itemCount: number;
  createdAt: string;
}

export interface SamplingPlanDetailDto {
  id: string;
  year: number;
  status: SamplingPlanStatus;
  distributorId: string;
  distributorName: string;
  createdById: string;
  createdByName: string | null;
  notes: string | null;
  rejectionReason: string | null;
  items: SamplingPlanItemDto[];
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
  statusChangedAt: string | null;
}

export interface SamplingPlanItemCreateDto {
  samplingLocationId: string;
  analysisProfileId: string;
  frequencyPerYear: number;
  plannedMonths: number[];
}

export interface SamplingPlanCreateDto {
  distributorId: string;
  year: number;
  notes: string | null;
  items: SamplingPlanItemCreateDto[];
}

export interface SamplingPlanUpdateDto {
  notes: string | null;
  items: SamplingPlanItemCreateDto[];
}

export interface SamplingPlanFilterDto {
  statuses?: SamplingPlanStatus[];
  search?: string;
  year?: number;
  distributorId?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface SamplingPlanPagedResultDto {
  items: SamplingPlanListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SamplingPlanRejectDto {
  reason: string;
}

export interface GenerateOrdersResultDto {
  ordersCreated: number;
  orders: GeneratedOrderSummaryDto[];
}

export interface GeneratedOrderSummaryDto {
  orderId: string;
  orderNumber: string;
  samplingLocationName: string;
  analysisProfileName: string;
  plannedDate: string | null;
}
