import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DistributorDto,
  DistributorListDto,
  DistributorAddDto,
  DistributorUpdateDto,
  DistributorFilteringInputDto,
} from '../models/distributor.model';

@Injectable({ providedIn: 'root' })
export class DistributorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/distributors';

  getAll(filter?: DistributorFilteringInputDto): Observable<DistributorListDto[]> {
    let params = new HttpParams();
    if (filter?.name) {
      params = params.set('name', filter.name);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<DistributorListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<DistributorDto> {
    return this.http.get<DistributorDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: DistributorAddDto): Observable<DistributorDto> {
    return this.http.post<DistributorDto>(this.baseUrl, dto);
  }

  update(id: string, dto: DistributorUpdateDto): Observable<DistributorDto> {
    return this.http.put<DistributorDto>(`${this.baseUrl}/${id}`, dto);
  }

  toggleStatus(id: string): Observable<DistributorDto> {
    return this.http.put<DistributorDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }
}
