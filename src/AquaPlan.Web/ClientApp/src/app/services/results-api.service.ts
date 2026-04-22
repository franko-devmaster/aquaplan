import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  RecentResultDto,
  ResultsMatrixDto,
  ResultsMatrixFilter,
} from '../models/results.model';

/**
 * AQ-415 — Feeds the /results screen (recent zone + LDP×dates matrix).
 */
@Injectable({ providedIn: 'root' })
export class ResultsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/results';

  getRecent(days = 7, take = 50): Observable<RecentResultDto[]> {
    const params = new HttpParams()
      .set('days', days.toString())
      .set('take', take.toString());
    return this.http.get<RecentResultDto[]>(`${this.baseUrl}/recent`, { params });
  }

  getMatrix(filter: ResultsMatrixFilter = {}): Observable<ResultsMatrixDto> {
    let params = new HttpParams();
    if (filter.distributorId) {
      params = params.set('distributorId', filter.distributorId);
    }
    if (filter.sectorId) {
      params = params.set('sectorId', filter.sectorId);
    }
    if (filter.anomaliesOnly) {
      params = params.set('anomaliesOnly', 'true');
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }
    return this.http.get<ResultsMatrixDto>(`${this.baseUrl}/matrix`, { params });
  }
}
