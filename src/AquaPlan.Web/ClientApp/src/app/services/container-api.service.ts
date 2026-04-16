import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ContainerDto,
  ContainerListDto,
  ContainerAddDto,
  ContainerUpdateDto,
  ContainerFilteringInputDto,
} from '../models/container.model';

@Injectable({ providedIn: 'root' })
export class ContainerApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/containers';

  getAll(filter?: ContainerFilteringInputDto): Observable<ContainerListDto[]> {
    let params = new HttpParams();
    if (filter?.search) {
      params = params.set('search', filter.search);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<ContainerListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<ContainerDto> {
    return this.http.get<ContainerDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: ContainerAddDto): Observable<ContainerDto> {
    return this.http.post<ContainerDto>(this.baseUrl, dto);
  }

  update(id: string, dto: ContainerUpdateDto): Observable<ContainerDto> {
    return this.http.put<ContainerDto>(`${this.baseUrl}/${id}`, dto);
  }

  toggleStatus(id: string): Observable<ContainerDto> {
    return this.http.patch<ContainerDto>(`${this.baseUrl}/${id}/toggle-status`, {});
  }
}
