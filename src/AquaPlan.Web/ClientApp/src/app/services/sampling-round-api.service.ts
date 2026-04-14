import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SamplingRoundDetailDto,
  SamplingRoundCreateDto,
  SamplingRoundUpdateDto,
  SamplingRoundAssignDto,
  SamplingRoundFilterDto,
  SamplingRoundPagedResultDto,
  LocationReplacementDto,
  SamplerCommentDto,
} from '../models/sampling-round.model';

@Injectable({ providedIn: 'root' })
export class SamplingRoundApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/sampling-rounds';

  getFiltered(filter: SamplingRoundFilterDto): Observable<SamplingRoundPagedResultDto> {
    let params = new HttpParams();
    if (filter.statuses && filter.statuses.length > 0) {
      for (const status of filter.statuses) {
        params = params.append('statuses', status.toString());
      }
    }
    if (filter.distributorId) {
      params = params.set('distributorId', filter.distributorId);
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
    return this.http.get<SamplingRoundPagedResultDto>(this.baseUrl, { params });
  }

  getById(id: string): Observable<SamplingRoundDetailDto> {
    return this.http.get<SamplingRoundDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: SamplingRoundCreateDto): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(this.baseUrl, dto);
  }

  update(id: string, dto: SamplingRoundUpdateDto): Observable<SamplingRoundDetailDto> {
    return this.http.put<SamplingRoundDetailDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  assign(id: string, dto: SamplingRoundAssignDto): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(`${this.baseUrl}/${id}/assign`, dto);
  }

  revertToDraft(id: string): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(`${this.baseUrl}/${id}/revert-to-draft`, {});
  }

  transmitAll(id: string): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(`${this.baseUrl}/${id}/transmit-all`, {});
  }

  cancel(id: string): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(`${this.baseUrl}/${id}/cancel`, {});
  }

  addOrder(roundId: string, orderId: string): Observable<SamplingRoundDetailDto> {
    return this.http.post<SamplingRoundDetailDto>(`${this.baseUrl}/${roundId}/orders`, { orderId });
  }

  removeOrder(roundId: string, orderId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${roundId}/orders/${orderId}`);
  }

  reorderOrders(roundId: string, orderIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${roundId}/orders/reorder`, { orderIds });
  }

  replaceLocation(orderId: string, dto: LocationReplacementDto): Observable<void> {
    return this.http.post<void>(`/api/orders/${orderId}/replace-location`, dto);
  }

  startOrder(orderId: string): Observable<void> {
    return this.http.post<void>(`/api/orders/${orderId}/start`, {});
  }

  updateSamplerComment(orderId: string, dto: SamplerCommentDto): Observable<void> {
    return this.http.put<void>(`/api/orders/${orderId}/sampler-comment`, dto);
  }
}
