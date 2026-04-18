import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DistributorDto } from '../models/distributor.model';

export interface DistributorDelegationDto {
  id: string;
  delegatingDistributorId: string;
  delegatingDistributorName: string;
  delegatedToDistributorId: string;
  delegatedToDistributorName: string;
  validFrom: string;
  validTo: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface DistributorDelegationCreateDto {
  delegatingDistributorId: string;
  delegatedToDistributorId: string;
  validFrom: string;
  validTo: string | null;
}

@Injectable({ providedIn: 'root' })
export class DelegationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/delegations';

  getAll(): Observable<DistributorDelegationDto[]> {
    return this.http.get<DistributorDelegationDto[]>(this.baseUrl);
  }

  create(dto: DistributorDelegationCreateDto): Observable<DistributorDelegationDto> {
    return this.http.post<DistributorDelegationDto>(this.baseUrl, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** AQ-369 — distributors on which the current user is authorized to create orders/rounds. */
  getMyAuthorizedDistributors(): Observable<DistributorDto[]> {
    return this.http.get<DistributorDto[]>(`${this.baseUrl}/my-authorized-distributors`);
  }
}
