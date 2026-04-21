import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { LocaleService } from '../../services/locale.service';
import { SyncService } from '../../services/sync.service';
import { NotificationsBellComponent } from '../notifications-bell/notifications-bell.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule,
    MatTooltipModule, MatProgressSpinnerModule, TranslateModule,
    NotificationsBellComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar color="primary" class="header">
      <button mat-icon-button (click)="menuToggle.emit()" aria-label="Toggle navigation menu">
        <mat-icon>menu</mat-icon>
      </button>
      <span class="logo">{{ 'app.title' | translate }}</span>
      <span class="spacer"></span>

      <!-- AQ-378 — offline / pending / syncing chips. Order is fixed to avoid
           layout jitter when chips appear and disappear. -->
      @if (syncService.isSyncing()) {
        <span class="sync-chip sync-chip--syncing"
              [attr.aria-label]="'header.syncing' | translate"
              [matTooltip]="'header.syncing' | translate">
          <mat-spinner diameter="16"></mat-spinner>
          @if (!isMobile()) {
            <span class="sync-chip__label">{{ 'header.syncing' | translate }}</span>
          }
        </span>
      }
      @if (!syncService.onlineStatus()) {
        <span class="sync-chip sync-chip--offline"
              [attr.aria-label]="'header.offline' | translate"
              [matTooltip]="'header.offlineTooltip' | translate">
          <mat-icon>cloud_off</mat-icon>
          @if (!isMobile()) {
            <span class="sync-chip__label">{{ 'header.offline' | translate }}</span>
          }
        </span>
      }
      @if (syncService.onlineStatus() && syncService.pendingCount() > 0) {
        <span class="sync-chip sync-chip--pending"
              [attr.aria-label]="pendingAriaLabel()"
              [matTooltip]="'header.pendingActionsTooltip' | translate:{ count: syncService.pendingCount() }">
          <mat-icon>sync_problem</mat-icon>
          @if (!isMobile()) {
            <span class="sync-chip__label">
              {{ syncService.pendingCount() }} {{ 'header.pendingActions' | translate }}
            </span>
          } @else {
            <span class="sync-chip__count">{{ syncService.pendingCount() }}</span>
          }
        </span>
      }

      @if (!isMobile()) {
        <button mat-button [matMenuTriggerFor]="langMenu" aria-label="Change language">
          {{ localeService.currentLang().toUpperCase() }}
        </button>
      }
      <mat-menu #langMenu="matMenu">
        @for (lang of localeService.getSupportedLanguages(); track lang) {
          <button mat-menu-item (click)="localeService.switchLanguage(lang)">
            {{ lang.toUpperCase() }}
          </button>
        }
      </mat-menu>

      @if (authService.isAuthenticated()) {
        <!-- AQ-43 — Notifications bell with unread badge + dropdown -->
        <app-notifications-bell />
        <button mat-button [matMenuTriggerFor]="userMenu" class="user-chip" aria-label="User menu">
          <span class="user-name">{{ userDisplayName() }}</span>
          <mat-icon>arrow_drop_down</mat-icon>
        </button>
        <mat-menu #userMenu="matMenu">
          @if (isMobile()) {
            <button mat-menu-item disabled>
              {{ userDisplayName() }}
            </button>
            <mat-divider></mat-divider>
            @for (lang of localeService.getSupportedLanguages(); track lang) {
              <button mat-menu-item (click)="localeService.switchLanguage(lang)">
                {{ lang.toUpperCase() }}
              </button>
            }
            <mat-divider></mat-divider>
          }
          <button mat-menu-item (click)="authService.logout()">
            <mat-icon>logout</mat-icon>
            {{ 'auth.logout' | translate }}
          </button>
        </mat-menu>
      } @else if (isMobile()) {
        <button mat-button [matMenuTriggerFor]="langMenu" aria-label="Change language">
          {{ localeService.currentLang().toUpperCase() }}
        </button>
      }
    </mat-toolbar>
  `,
  styles: [`
    .header { position: fixed; top: 0; left: 0; right: 0; z-index: 1000; }
    .logo { margin-left: 8px; font-weight: 500; }
    .spacer { flex: 1 1 auto; }
    .user-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      border-radius: 20px;
      padding: 4px 12px;
      color: white;
      border: 1px solid rgba(0, 0, 0, 0.2);
    }
    .user-name {
      font-size: 14px;
      font-weight: 500;
    }
    /* AQ-378 — sync chips sit between spacer and language button. Fixed height
       to prevent toolbar layout shift. */
    .sync-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 28px;
      padding: 0 10px;
      margin-right: 6px;
      border-radius: 14px;
      font-size: 12px;
      font-weight: 500;
      color: #fff;
      white-space: nowrap;
    }
    .sync-chip mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      line-height: 18px;
    }
    .sync-chip__label { line-height: 1; }
    .sync-chip__count {
      font-weight: 600;
      font-size: 12px;
      margin-left: 2px;
    }
    .sync-chip--offline {
      background-color: #C62828; /* WCAG AA contrast on primary toolbar */
    }
    .sync-chip--pending {
      background-color: #EF6C00;
    }
    .sync-chip--syncing {
      background-color: rgba(255, 255, 255, 0.18);
    }
    @media (max-width: 767px) {
      .logo { font-size: 16px; }
      .sync-chip { padding: 0 8px; margin-right: 4px; }
    }
  `],
})
export class HeaderComponent {
  readonly authService = inject(AuthService);
  readonly localeService = inject(LocaleService);
  readonly syncService = inject(SyncService);
  readonly isMobile = input(false);
  readonly menuToggle = output();

  readonly userDisplayName = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '';
    const initial = user.firstName?.charAt(0).toUpperCase() ?? '';
    return `${initial}. ${user.lastName}`;
  });

  readonly pendingAriaLabel = computed(() => {
    const count = this.syncService.pendingCount();
    return `${count} actions en attente`;
  });
}
