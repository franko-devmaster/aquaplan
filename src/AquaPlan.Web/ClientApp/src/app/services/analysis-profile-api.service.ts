import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AnalysisProfileDto,
  AnalysisProfileListDto,
  AnalysisProfileAddDto,
  AnalysisProfileUpdateDto,
  AnalysisProfileFilteringInputDto,
} from '../models/analysis-profile.model';

@Injectable({ providedIn: 'root' })
export class AnalysisProfileApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/analysis-profiles';

  getAll(filter?: AnalysisProfileFilteringInputDto): Observable<AnalysisProfileListDto[]> {
    let params = new HttpParams();
    if (filter?.search) {
      params = params.set('search', filter.search);
    }
    if (filter?.category !== undefined) {
      params = params.set('category', filter.category);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<AnalysisProfileListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<AnalysisProfileDto> {
    return this.http.get<AnalysisProfileDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: AnalysisProfileAddDto): Observable<AnalysisProfileDto> {
    return this.http.post<AnalysisProfileDto>(this.baseUrl, dto);
  }

  update(id: string, dto: AnalysisProfileUpdateDto): Observable<AnalysisProfileDto> {
    return this.http.put<AnalysisProfileDto>(`${this.baseUrl}/${id}`, dto);
  }

  toggleStatus(id: string): Observable<AnalysisProfileDto> {
    return this.http.patch<AnalysisProfileDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }
}
