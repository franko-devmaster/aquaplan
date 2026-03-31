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

export interface OrderListDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  isUnplanned: boolean;
  createdById: string;
  createdByName: string | null;
  preleveurId: string | null;
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  createdAt: string;
}

export interface OrderDetailDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  isUnplanned: boolean;
  createdById: string;
  createdByName: string | null;
  preleveurId: string | null;
  preleveurName: string | null;
  distributorId: string;
  distributorName: string;
  tenantId: string;
  createdAt: string;
  updatedAt: string | null;
  sampling: SamplingDto | null;
}

export interface OrderCreateDto {
  distributorId: string;
  preleveurId: string | null;
  isUnplanned: boolean;
}

export interface OrderAssignDto {
  preleveurId: string;
}
