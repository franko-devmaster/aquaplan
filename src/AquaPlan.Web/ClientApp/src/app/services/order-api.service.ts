import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OrderListDto, OrderDetailDto, OrderCreateDto, OrderAssignDto } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/orders';

  getAll(): Observable<OrderListDto[]> {
    return this.http.get<OrderListDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<OrderDetailDto> {
    return this.http.get<OrderDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: OrderCreateDto): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(this.baseUrl, dto);
  }

  assignPreleveur(orderId: string, dto: OrderAssignDto): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(`${this.baseUrl}/${orderId}/assign`, dto);
  }
}
