import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { NotificationApiService } from '../services/notification-api.service';
import { NotificationDto, NotificationListDto } from '../models/notification.model';

// AQ-43 — Signal-based datastore backing the header bell + dropdown.
// The header component kicks off polling via `startPolling()`.
@Injectable({ providedIn: 'root' })
export class NotificationDatastore {
  private readonly api = inject(NotificationApiService);

  readonly notifications = signal<NotificationDto[]>([]);
  readonly unreadCount = signal(0);
  readonly urgentUnreadCount = signal(0);
  readonly loading = signal(false);

  readonly hasUnread = computed(() => this.unreadCount() > 0);
  readonly hasUrgentUnread = computed(() => this.urgentUnreadCount() > 0);

  private pollingHandle: ReturnType<typeof setInterval> | null = null;

  async reload(take = 20): Promise<void> {
    this.loading.set(true);
    try {
      const result: NotificationListDto = await firstValueFrom(
        this.api.getForCurrentUser(false, take),
      );
      this.notifications.set(result.items);
      this.unreadCount.set(result.unreadCount);
      this.urgentUnreadCount.set(result.urgentUnreadCount);
    } finally {
      this.loading.set(false);
    }
  }

  async refreshUnreadCount(): Promise<void> {
    try {
      const count = await firstValueFrom(this.api.getUnreadCount());
      this.unreadCount.set(count);
    } catch {
      // swallow transient errors — polling will retry at the next tick.
    }
  }

  async markAsRead(id: string): Promise<void> {
    await firstValueFrom(this.api.markAsRead(id));
    this.notifications.update(items =>
      items.map(n => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)),
    );
    this.unreadCount.update(c => Math.max(0, c - 1));
    const wasUrgent = this.notifications().some(n => n.id === id && n.isUrgent);
    if (wasUrgent) {
      this.urgentUnreadCount.update(c => Math.max(0, c - 1));
    }
  }

  async markAllAsRead(): Promise<void> {
    await firstValueFrom(this.api.markAllAsRead());
    this.notifications.update(items =>
      items.map(n => (n.isRead ? n : { ...n, isRead: true, readAt: new Date().toISOString() })),
    );
    this.unreadCount.set(0);
    this.urgentUnreadCount.set(0);
  }

  startPolling(intervalMs = 60000): void {
    this.stopPolling();
    // Kick off immediate fetch then poll on interval.
    void this.reload();
    this.pollingHandle = setInterval(() => {
      void this.refreshUnreadCount();
    }, intervalMs);
  }

  stopPolling(): void {
    if (this.pollingHandle !== null) {
      clearInterval(this.pollingHandle);
      this.pollingHandle = null;
    }
  }

  reset(): void {
    this.stopPolling();
    this.notifications.set([]);
    this.unreadCount.set(0);
    this.urgentUnreadCount.set(0);
  }
}
