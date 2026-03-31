import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UserListDto, UserDetailDto, UserCreateDto, UserUpdateDto } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/users';

  getAll(): Observable<UserListDto[]> {
    return this.http.get<UserListDto[]>(this.baseUrl);
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
}
