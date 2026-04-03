import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OrderStatus } from '../models/order.model';
import { OrderStatusDto, OrderStatusTransitionDto, OrderTransitionRequestDto } from '../models/order-status.model';

@Injectable({ providedIn: 'root' })
export class OrderStatusApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/orderstatus';

  getAll(): Observable<OrderStatusDto[]> {
    return this.http.get<OrderStatusDto[]>(this.baseUrl);
  }

  getAllowedTransitions(status: OrderStatus): Observable<OrderStatusDto[]> {
    return this.http.get<OrderStatusDto[]>(`${this.baseUrl}/${status}/transitions`);
  }

  transitionOrder(orderId: string, dto: OrderTransitionRequestDto): Observable<OrderStatusTransitionDto> {
    return this.http.post<OrderStatusTransitionDto>(`/api/orders/${orderId}/transition`, dto);
  }
}
