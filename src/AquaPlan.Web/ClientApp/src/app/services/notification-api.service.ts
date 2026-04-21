import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NotificationListDto, NotificationLogDto } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/notifications';

  getForCurrentUser(unreadOnly = false, take = 20): Observable<NotificationListDto> {
    let params = new HttpParams()
      .set('unreadOnly', unreadOnly.toString())
      .set('take', take.toString());
    return this.http.get<NotificationListDto>(this.baseUrl, { params });
  }

  getUnreadCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/unread-count`);
  }

  markAsRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/mark-read`, {});
  }

  markAllAsRead(): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/mark-all-read`, {});
  }

  getAdminLogs(take = 50): Observable<NotificationLogDto[]> {
    const params = new HttpParams().set('take', take.toString());
    return this.http.get<NotificationLogDto[]>('/api/admin/notifications/log', { params });
  }
}
