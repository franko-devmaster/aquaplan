import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SamplingDto, SamplingCreateDto } from '../models/sampling.model';

@Injectable({ providedIn: 'root' })
export class SamplingApiService {
  private readonly http = inject(HttpClient);

  getByOrderId(orderId: string): Observable<SamplingDto> {
    return this.http.get<SamplingDto>(`/api/orders/${orderId}/sampling`);
  }

  create(orderId: string, dto: SamplingCreateDto): Observable<SamplingDto> {
    return this.http.post<SamplingDto>(`/api/orders/${orderId}/sampling`, dto);
  }

  update(orderId: string, dto: SamplingCreateDto): Observable<SamplingDto> {
    return this.http.put<SamplingDto>(`/api/orders/${orderId}/sampling`, dto);
  }

  complete(orderId: string): Observable<void> {
    return this.http.post<void>(`/api/orders/${orderId}/sampling/complete`, {});
  }

  validate(orderId: string): Observable<void> {
    return this.http.post<void>(`/api/orders/${orderId}/sampling/validate`, {});
  }
}
