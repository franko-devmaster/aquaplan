import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SectorDto,
  SectorListDto,
  SectorAddDto,
  SectorUpdateDto,
  SectorFilteringInputDto,
} from '../models/sector.model';

@Injectable({ providedIn: 'root' })
export class SectorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/sectors';

  getAll(filter?: SectorFilteringInputDto): Observable<SectorListDto[]> {
    let params = new HttpParams();
    if (filter?.name) {
      params = params.set('name', filter.name);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<SectorListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<SectorDto> {
    return this.http.get<SectorDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: SectorAddDto): Observable<SectorDto> {
    return this.http.post<SectorDto>(this.baseUrl, dto);
  }

  update(id: string, dto: SectorUpdateDto): Observable<SectorDto> {
    return this.http.put<SectorDto>(`${this.baseUrl}/${id}`, dto);
  }

  toggleStatus(id: string): Observable<SectorDto> {
    return this.http.put<SectorDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }
}
