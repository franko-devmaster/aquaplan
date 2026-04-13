import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UserListDto, UserDetailDto, UserCreateDto, UserUpdateDto } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/users';

  getAll(filter?: { role?: string; distributorId?: string; isActive?: boolean }): Observable<UserListDto[]> {
    let params = new HttpParams();
    if (filter?.role) {
      params = params.set('role', filter.role);
    }
    if (filter?.distributorId) {
      params = params.set('distributorId', filter.distributorId);
    }
    if (filter?.isActive !== undefined) {
      params = params.set('isActive', filter.isActive.toString());
    }
    return this.http.get<UserListDto[]>(this.baseUrl, { params });
  }

  getById(id: string): Observable<UserDetailDto> {
    return this.http.get<UserDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: UserCreateDto): Observable<UserDetailDto> {
    return this.http.post<UserDetailDto>(this.baseUrl, dto);
  }

  update(id: string, dto: UserUpdateDto): Observable<UserDetailDto> {
    return this.http.put<UserDetailDto>(`${this.baseUrl}/${id}`, dto);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  getPreleveurs(distributorId?: string): Observable<UserListDto[]> {
    let params = new HttpParams();
    if (distributorId) {
      params = params.set('distributorId', distributorId);
    }
    return this.http.get<UserListDto[]>(`${this.baseUrl}/preleveurs`, { params });
  }
}
