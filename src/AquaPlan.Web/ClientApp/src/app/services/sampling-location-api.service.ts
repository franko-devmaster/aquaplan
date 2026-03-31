import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SamplingLocationDto,
  SamplingLocationCreateDto,
  SamplingLocationUpdateDto,
} from '../models/sampling-location.model';

@Injectable({ providedIn: 'root' })
export class SamplingLocationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/samplinglocations';

  getForCurrentUser(): Observable<SamplingLocationDto[]> {
    return this.http.get<SamplingLocationDto[]>(this.baseUrl);
  }

  getByDistributor(distributorId: string): Observable<SamplingLocationDto[]> {
    return this.http.get<SamplingLocationDto[]>(`${this.baseUrl}/distributor/${distributorId}`);
  }

  getById(id: string): Observable<SamplingLocationDto> {
    return this.http.get<SamplingLocationDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: SamplingLocationCreateDto): Observable<SamplingLocationDto> {
    return this.http.post<SamplingLocationDto>(this.baseUrl, dto);
  }

  update(id: string, dto: SamplingLocationUpdateDto): Observable<SamplingLocationDto> {
    return this.http.put<SamplingLocationDto>(`${this.baseUrl}/${id}`, dto);
  }
}
