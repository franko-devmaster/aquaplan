import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SamplingPlanDetailDto,
  SamplingPlanCreateDto,
  SamplingPlanUpdateDto,
  SamplingPlanFilterDto,
  SamplingPlanPagedResultDto,
  SamplingPlanRejectDto,
} from '../models/sampling-plan.model';

@Injectable({ providedIn: 'root' })
export class SamplingPlanApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/samplingplans';

  getFiltered(filter: SamplingPlanFilterDto): Observable<SamplingPlanPagedResultDto> {
    let params = new HttpParams();
    if (filter.statuses && filter.statuses.length > 0) {
      for (const status of filter.statuses) {
        params = params.append('statuses', status.toString());
      }
    }
    if (filter.search) {
      params = params.set('search', filter.search);
    }
    if (filter.year !== undefined) {
      params = params.set('year', filter.year.toString());
    }
    if (filter.distributorId) {
      params = params.set('distributorId', filter.distributorId);
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
    return this.http.get<SamplingPlanPagedResultDto>(this.baseUrl, { params });
  }

  getById(id: string): Observable<SamplingPlanDetailDto> {
    return this.http.get<SamplingPlanDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: SamplingPlanCreateDto): Observable<SamplingPlanDetailDto> {
    return this.http.post<SamplingPlanDetailDto>(this.baseUrl, dto);
  }

  update(id: string, dto: SamplingPlanUpdateDto): Observable<SamplingPlanDetailDto> {
    return this.http.put<SamplingPlanDetailDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  submit(id: string): Observable<SamplingPlanDetailDto> {
    return this.http.post<SamplingPlanDetailDto>(`${this.baseUrl}/${id}/submit`, {});
  }

  validate(id: string): Observable<SamplingPlanDetailDto> {
    return this.http.post<SamplingPlanDetailDto>(`${this.baseUrl}/${id}/validate`, {});
  }

  reject(id: string, dto: SamplingPlanRejectDto): Observable<SamplingPlanDetailDto> {
    return this.http.post<SamplingPlanDetailDto>(`${this.baseUrl}/${id}/reject`, dto);
  }
}
