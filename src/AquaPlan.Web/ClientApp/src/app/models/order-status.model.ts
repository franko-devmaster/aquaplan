import { OrderStatus } from './order.model';

export interface OrderStatusDto {
  status: OrderStatus;
  name: string;
  description: string;
  color: string;
  isTerminal: boolean;
}

export interface OrderStatusTransitionDto {
  fromStatus: OrderStatus;
  toStatus: OrderStatus;
  transitionDate: string;
}

export interface OrderTransitionRequestDto {
  newStatus: OrderStatus;
}
