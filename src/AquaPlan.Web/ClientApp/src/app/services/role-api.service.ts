import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RoleDto, RoleWithPermissionsDto, PermissionDto, RoleAssignDto } from '../models/role.model';

@Injectable({ providedIn: 'root' })
export class RoleApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/roles';

  getAll(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<RoleWithPermissionsDto> {
    return this.http.get<RoleWithPermissionsDto>(`${this.baseUrl}/${id}`);
  }

  getPermissions(): Observable<PermissionDto[]> {
    return this.http.get<PermissionDto[]>(`${this.baseUrl}/permissions`);
  }

  assignRole(userId: string, dto: RoleAssignDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/users/${userId}/assign`, dto);
  }

  removeRole(userId: string, dto: RoleAssignDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/users/${userId}/remove`, dto);
  }
}
