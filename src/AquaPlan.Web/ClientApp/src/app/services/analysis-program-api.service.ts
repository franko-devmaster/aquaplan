import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AnalysisProgramDto,
  AnalysisProgramListDto,
  AnalysisProgramAddDto,
  AnalysisProgramUpdateDto,
  AnalysisProgramAddProfilesDto,
  AnalysisProgramFilteringInputDto,
} from '../models/analysis-program.model';

@Injectable({ providedIn: 'root' })
export class AnalysisProgramApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/analysis-programs';

  getAll(filter?: AnalysisProgramFilteringInputDto): Observable<AnalysisProgramListDto[]> {
    let params = new HttpParams();
    if (filter?.search) {
      params = params.set('search', filter.search);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<AnalysisProgramListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<AnalysisProgramDto> {
    return this.http.get<AnalysisProgramDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: AnalysisProgramAddDto): Observable<AnalysisProgramDto> {
    return this.http.post<AnalysisProgramDto>(this.baseUrl, dto);
  }

  update(id: string, dto: AnalysisProgramUpdateDto): Observable<AnalysisProgramDto> {
    return this.http.put<AnalysisProgramDto>(`${this.baseUrl}/${id}`, dto);
  }

  toggleStatus(id: string): Observable<AnalysisProgramDto> {
    return this.http.patch<AnalysisProgramDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }

  addProfiles(id: string, dto: AnalysisProgramAddProfilesDto): Observable<AnalysisProgramDto> {
    return this.http.post<AnalysisProgramDto>(`${this.baseUrl}/${id}/profiles`, dto);
  }

  removeProfile(id: string, profileId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/profiles/${profileId}`);
  }
}
