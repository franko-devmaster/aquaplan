import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  OrderDetailDto,
  OrderCreateDto,
  OrderUpdateDto,
  OrderAssignDto,
  OrderFilterDto,
  OrderPagedResultDto,
} from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/orders';

  getFiltered(filter: OrderFilterDto): Observable<OrderPagedResultDto> {
    let params = new HttpParams();
    if (filter.statuses && filter.statuses.length > 0) {
      for (const status of filter.statuses) {
        params = params.append('statuses', status.toString());
      }
    }
    if (filter.isUnassigned !== undefined) {
      params = params.set('isUnassigned', filter.isUnassigned.toString());
    }
    if (filter.hasNoRound !== undefined) {
      params = params.set('hasNoRound', filter.hasNoRound.toString());
    }
    if (filter.search) {
      params = params.set('search', filter.search);
    }
    if (filter.page !== undefined) {
      params = params.set('page', filter.page.toString());
    }
    if (filter.pageSize !== undefined) {
      params = params.set('pageSize', filter.pageSize.toString());
    }
    if (filter.sortBy) {
      params = params.set('sortBy', filter.sortBy);
    }
    if (filter.sortDescending !== undefined) {
      params = params.set('sortDescending', filter.sortDescending.toString());
    }
    if (filter.distributorId) {
      params = params.set('distributorId', filter.distributorId);
    }
    if (filter.preleveurId) {
      params = params.set('preleveurId', filter.preleveurId);
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }
    return this.http.get<OrderPagedResultDto>(this.baseUrl, { params });
  }

  getById(id: string): Observable<OrderDetailDto> {
    return this.http.get<OrderDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: OrderCreateDto): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(this.baseUrl, dto);
  }

  update(id: string, dto: OrderUpdateDto): Observable<OrderDetailDto> {
    return this.http.put<OrderDetailDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  assignPreleveur(orderId: string, dto: OrderAssignDto): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(`${this.baseUrl}/${orderId}/assign`, dto);
  }

  transition(orderId: string, newStatus: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${orderId}/transition`, { newStatus });
  }

  exportCsv(filter: OrderFilterDto): Observable<Blob> {
    let params = new HttpParams();
    if (filter.statuses && filter.statuses.length > 0) {
      for (const status of filter.statuses) {
        params = params.append('statuses', status.toString());
      }
    }
    if (filter.search) {
      params = params.set('search', filter.search);
    }
    if (filter.distributorId) {
      params = params.set('distributorId', filter.distributorId);
    }
    if (filter.preleveurId) {
      params = params.set('preleveurId', filter.preleveurId);
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }
    return this.http.get(`${this.baseUrl}/export`, { params, responseType: 'blob' });
  }
}
