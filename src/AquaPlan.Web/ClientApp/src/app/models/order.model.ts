export enum OrderStatus {
  New = 'New',
  InProgress = 'InProgress',
  Completed = 'Completed',
  Transmitted = 'Transmitted',
  Done = 'Done',
  Cancelled = 'Cancelled',
}

export enum UnplannedReason {
  Pollution = 'Pollution',
  Urgency = 'Urgency',
  ComplementaryControl = 'ComplementaryControl',
}

export const UnplannedReasonLabels: Record<UnplannedReason, string> = {
  [UnplannedReason.Pollution]: 'orders.unplannedReason.pollution',
  [UnplannedReason.Urgency]: 'orders.unplannedReason.urgency',
  [UnplannedReason.ComplementaryControl]: 'orders.unplannedReason.complementaryControl',
};

export const OrderStatusLabels: Record<OrderStatus, string> = {
  [OrderStatus.New]: 'orders.status.new',
  [OrderStatus.InProgress]: 'orders.status.inProgress',
  [OrderStatus.Completed]: 'orders.status.completed',
  [OrderStatus.Transmitted]: 'orders.status.transmitted',
  [OrderStatus.Done]: 'orders.status.done',
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
  isDelegated: boolean;
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
  isDelegated: boolean;
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
  hasNoRound?: boolean;
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
