export enum OrderStatus {
  Draft = 0,
  Assigned = 1,
  InProgress = 2,
  SamplingCompleted = 3,
  Validated = 4,
  SentToLims = 5,
  ResultsReceived = 6,
  Completed = 7,
  Cancelled = 8,
}

export enum UnplannedReason {
  Pollution = 0,
  Urgency = 1,
  ComplementaryControl = 2,
}

export const UnplannedReasonLabels: Record<UnplannedReason, string> = {
  [UnplannedReason.Pollution]: 'orders.unplannedReason.pollution',
  [UnplannedReason.Urgency]: 'orders.unplannedReason.urgency',
  [UnplannedReason.ComplementaryControl]: 'orders.unplannedReason.complementaryControl',
};

export const OrderStatusLabels: Record<OrderStatus, string> = {
  [OrderStatus.Draft]: 'orders.status.draft',
  [OrderStatus.Assigned]: 'orders.status.assigned',
  [OrderStatus.InProgress]: 'orders.status.inProgress',
  [OrderStatus.SamplingCompleted]: 'orders.status.samplingCompleted',
  [OrderStatus.Validated]: 'orders.status.validated',
  [OrderStatus.SentToLims]: 'orders.status.sentToLims',
  [OrderStatus.ResultsReceived]: 'orders.status.resultsReceived',
  [OrderStatus.Completed]: 'orders.status.completed',
  [OrderStatus.Cancelled]: 'orders.status.cancelled',
};

export interface SamplingDto {
  id: string;
  orderId: string;
  preleveurId: string;
  preleveurName: string | null;
  samplingDateTime: string;
  temperature: number | null;
  weather: string | null;
  locationLat: number | null;
  locationLng: number | null;
  notes: string | null;
  isValidated: boolean;
  validatedAt: string | null;
  createdAt: string;
}

export interface OrderAnalysisProfileDto {
  analysisProfileId: string;
  code: string;
  name: string;
}

export interface OrderListDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  isUnplanned: boolean;
  unplannedReason: UnplannedReason | null;
  createdById: string;
  createdByName: string | null;
  preleveurId: string | null;
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  samplingLocationId: string | null;
  samplingLocationName: string | null;
  plannedDate: string | null;
  createdAt: string;
}

export interface OrderDetailDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  isUnplanned: boolean;
  unplannedReason: UnplannedReason | null;
  unplannedReasonDetails: string | null;
  createdById: string;
  createdByName: string | null;
  preleveurId: string | null;
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  samplingLocationId: string | null;
  samplingLocationName: string | null;
  plannedDate: string | null;
  notes: string | null;
  analysisProfiles: OrderAnalysisProfileDto[];
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
  sampling: SamplingDto | null;
}

export interface OrderCreateDto {
  distributorId: string;
  samplingLocationId: string | null;
  preleveurId: string | null;
  plannedDate: string | null;
  analysisProfileIds: string[] | null;
  notes: string | null;
  isUnplanned: boolean;
  unplannedReason?: UnplannedReason | null;
  unplannedReasonDetails?: string | null;
}

export interface OrderUpdateDto {
  samplingLocationId: string | null;
  preleveurId: string | null;
  plannedDate: string | null;
  analysisProfileIds: string[] | null;
  notes: string | null;
}

export interface OrderAssignDto {
  preleveurId: string;
}

export interface OrderFilterDto {
  statuses?: OrderStatus[];
  isUnassigned?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
  distributorId?: string;
  preleveurId?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface OrderPagedResultDto {
  items: OrderListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}
