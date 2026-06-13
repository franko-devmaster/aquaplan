import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { NotificationDatastore } from '../../datastore/notification.datastore';
import { NotificationDto } from '../../models/notification.model';

/**
 * AQ-43 — Notifications bell for the app header. Displays a badge with the
 * unread count and a dropdown showing the 10 most recent items. Urgent
 * notifications (AQ-45) are flagged with a red background and warning icon.
 */
@Component({
  selector: 'app-notifications-bell',
  standalone: true,
  imports: [
    MatButtonModule, MatIconModule, MatMenuModule, MatBadgeModule,
    MatDividerModule, MatTooltipModule, TranslateModule, DatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-icon-button
            [matMenuTriggerFor]="notifMenu"
            [attr.aria-label]="'notifications.bell.ariaLabel' | translate"
            [matTooltip]="'notifications.bell.ariaLabel' | translate"
            class="notifications-trigger"
            (menuOpened)="onOpened()">
      @if (store.unreadCount() > 0) {
        <mat-icon [matBadge]="store.unreadCount()"
                  matBadgeColor="warn"
                  matBadgeSize="small"
                  [class.urgent-bell]="store.hasUrgentUnread()">
          {{ store.hasUrgentUnread() ? 'notification_important' : 'notifications_active' }}
        </mat-icon>
      } @else {
        <mat-icon>notifications</mat-icon>
      }
    </button>

    <mat-menu #notifMenu="matMenu" class="notifications-menu" xPosition="before">
      <div class="notifications-header" (click)="$event.stopPropagation()">
        <span class="notifications-title">{{ 'notifications.bell.title' | translate }}</span>
        @if (store.hasUnread()) {
          <button mat-button
                  class="mark-all-btn"
                  (click)="markAllAsRead($event)">
            {{ 'notifications.bell.markAllRead' | translate }}
          </button>
        }
      </div>
      <mat-divider></mat-divider>

      @if (visibleItems().length === 0) {
        <div class="notifications-empty">
          {{ 'notifications.bell.empty' | translate }}
        </div>
      } @else {
        @for (n of visibleItems(); track n.id) {
          <button mat-menu-item
                  class="notification-item"
                  [class.notification-item--unread]="!n.isRead"
                  [class.notification-item--urgent]="n.isUrgent"
                  (click)="onClick(n)">
            <mat-icon class="notification-icon">
              {{ n.isUrgent ? 'warning' : iconFor(n) }}
            </mat-icon>
            <div class="notification-body">
              <div class="notification-title">{{ n.title }}</div>
              <div class="notification-message">{{ n.message }}</div>
              <div class="notification-date">{{ n.createdAt | date:'dd.MM.yyyy HH:mm' }}</div>
            </div>
          </button>
        }
      }
    </mat-menu>
  `,
  styles: [`
    /* AQ-423 — header is now a white surface; the bell must use the default
       foreground token so the icon stays visible (was #fff from the legacy blue header). */
    .notifications-trigger {
      color: var(--color-fg-default);
      margin-right: 4px;
    }
    .urgent-bell { color: var(--color-error-500); }
    .notifications-menu { max-width: 360px; }
    .notifications-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 6px 12px;
      font-size: 13px;
      font-weight: 500;
    }
    .notifications-title { color: rgba(0,0,0,0.87); }
    .mark-all-btn { font-size: 12px; padding: 0 6px; line-height: 28px; min-width: 0; }
    .notifications-empty {
      padding: 16px;
      text-align: center;
      font-size: 13px;
      color: rgba(0,0,0,0.54);
    }
    .notification-item {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 8px 12px;
      height: auto;
      line-height: 1.35;
      white-space: normal;
      min-width: 300px;
      max-width: 360px;
    }
    .notification-item--unread { background-color: #E3F2FD; }
    .notification-item--urgent {
      background-color: #FFCDD2 !important;
      border-left: 4px solid #C62828;
    }
    .notification-icon { color: #1565C0; flex-shrink: 0; margin-right: 0; }
    .notification-item--urgent .notification-icon { color: #C62828; }
    .notification-body { display: flex; flex-direction: column; gap: 2px; overflow: hidden; }
    .notification-title { font-weight: 500; font-size: 13px; }
    .notification-message {
      font-size: 12px;
      color: rgba(0,0,0,0.72);
      white-space: normal;
      overflow-wrap: anywhere;
    }
    .notification-date { font-size: 11px; color: rgba(0,0,0,0.54); }
  `],
})
export class NotificationsBellComponent implements OnInit, OnDestroy {
  readonly store = inject(NotificationDatastore);
  private readonly router = inject(Router);

  readonly visibleItems = computed(() => this.store.notifications().slice(0, 10));

  ngOnInit(): void {
    this.store.startPolling(60000);
  }

  ngOnDestroy(): void {
    this.store.stopPolling();
  }

  onOpened(): void {
    // Reload the full list whenever the user opens the dropdown so the
    // dropdown matches the latest server state (polling only refreshes the count).
    void this.store.reload();
  }

  async onClick(n: NotificationDto): Promise<void> {
    if (!n.isRead) {
      try {
        await this.store.markAsRead(n.id);
      } catch {
        // ignore
      }
    }
    const target = this.resolveTarget(n);
    if (target !== null) {
      void this.router.navigateByUrl(target);
    }
  }

  async markAllAsRead(event: Event): Promise<void> {
    event.stopPropagation();
    await this.store.markAllAsRead();
  }

  iconFor(n: NotificationDto): string {
    switch (n.type) {
      case 'OrderAssigned': return 'assignment_ind';
      case 'RoundAssigned': return 'tour';
      case 'ResultsReceived': return 'fact_check';
      case 'NonConformResult': return 'warning';
      default: return 'notifications';
    }
  }

  private resolveTarget(n: NotificationDto): string | null {
    if (!n.relatedEntityType || !n.relatedEntityId) return null;
    switch (n.relatedEntityType) {
      case 'Order': return `/orders/${n.relatedEntityId}`;
      case 'SamplingRound': return `/sampling-rounds/${n.relatedEntityId}`;
      default: return null;
    }
  }
}
