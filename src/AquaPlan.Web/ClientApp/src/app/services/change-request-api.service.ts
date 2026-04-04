import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ChangeRequestDto,
  ChangeRequestCreateDto,
  ChangeRequestUpdateDto,
  ChangeRequestReviewDto,
} from '../models/change-request.model';

@Injectable({ providedIn: 'root' })
export class ChangeRequestApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/sampling-location-requests';

  submitCreate(dto: ChangeRequestCreateDto): Observable<ChangeRequestDto> {
    return this.http.post<ChangeRequestDto>(`${this.baseUrl}/create`, dto);
  }

  submitUpdate(slId: string, dto: ChangeRequestUpdateDto): Observable<ChangeRequestDto> {
    return this.http.post<ChangeRequestDto>(`${this.baseUrl}/${slId}/update`, dto);
  }

  submitDeactivate(slId: string): Observable<ChangeRequestDto> {
    return this.http.post<ChangeRequestDto>(`${this.baseUrl}/${slId}/deactivate`, {});
  }

  getMyRequests(): Observable<ChangeRequestDto[]> {
    return this.http.get<ChangeRequestDto[]>(`${this.baseUrl}/my`);
  }

  getPendingRequests(): Observable<ChangeRequestDto[]> {
    return this.http.get<ChangeRequestDto[]>(`${this.baseUrl}/pending`);
  }

  getById(id: string): Observable<ChangeRequestDto> {
    return this.http.get<ChangeRequestDto>(`${this.baseUrl}/${id}`);
  }

  approve(id: string, dto: ChangeRequestReviewDto): Observable<ChangeRequestDto> {
    return this.http.post<ChangeRequestDto>(`${this.baseUrl}/${id}/approve`, dto);
  }

  reject(id: string, dto: ChangeRequestReviewDto): Observable<ChangeRequestDto> {
    return this.http.post<ChangeRequestDto>(`${this.baseUrl}/${id}/reject`, dto);
  }
}
