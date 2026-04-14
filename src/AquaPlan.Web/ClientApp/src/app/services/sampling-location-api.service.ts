import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SamplingLocationDto,
  SamplingLocationCreateDto,
  SamplingLocationUpdateDto,
  SamplingLocationListDto,
  SamplingLocationFilteringInputDto,
  ToggleStatusResultDto,
} from '../models/sampling-location.model';

@Injectable({ providedIn: 'root' })
export class SamplingLocationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/sampling-locations';

  getForCurrentUser(): Observable<SamplingLocationDto[]> {
    return this.http.get<SamplingLocationDto[]>(this.baseUrl);
  }

  getFiltered(filter: SamplingLocationFilteringInputDto): Observable<SamplingLocationListDto> {
    let params = new HttpParams();
    if (filter.distributorId) {
      params = params.set('DistributorId', filter.distributorId);
    }
    if (filter.search) {
      params = params.set('Search', filter.search);
    }
    if (filter.sectorId) {
      params = params.set('SectorId', filter.sectorId);
    }
    if (filter.isActive !== undefined) {
      params = params.set('IsActive', filter.isActive.toString());
    }
    if (filter.page) {
      params = params.set('Page', filter.page.toString());
    }
    if (filter.pageSize) {
      params = params.set('PageSize', filter.pageSize.toString());
    }
    return this.http.get<SamplingLocationListDto>(`${this.baseUrl}/filtered`, { params });
  }

  getByDistributor(distributorId: string): Observable<SamplingLocationDto[]> {
    return this.http.get<SamplingLocationDto[]>(`${this.baseUrl}/by-distributor/${distributorId}`);
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

  toggleStatus(id: string): Observable<ToggleStatusResultDto> {
    return this.http.put<ToggleStatusResultDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }

  checkCodeUnique(locationCode: string, distributorId: string, excludeId?: string): Observable<boolean> {
    let params = new HttpParams()
      .set('locationCode', locationCode)
      .set('distributorId', distributorId);
    if (excludeId) {
      params = params.set('excludeId', excludeId);
    }
    return this.http.get<boolean>(`${this.baseUrl}/check-code-unique`, { params });
  }

  exportPdf(distributorId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (distributorId) {
      params = params.set('distributorId', distributorId);
    }
    return this.http.get(`${this.baseUrl}/export-pdf`, { params, responseType: 'blob' });
  }
}
