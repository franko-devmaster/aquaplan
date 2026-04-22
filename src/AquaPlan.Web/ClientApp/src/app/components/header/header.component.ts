import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { LocaleService } from '../../services/locale.service';
import { SyncService } from '../../services/sync.service';
import { NotificationsBellComponent } from '../notifications-bell/notifications-bell.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule,
    MatTooltipModule, MatProgressSpinnerModule, RouterModule, TranslateModule,
    NotificationsBellComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // AQ-423 — Sprint B design refresh: 64px fixed white header with border-bottom,
  // brand logo + label, tokens everywhere. Replaces the former primary-blue
  // mat-toolbar chrome.
  template: `
    <header class="ap-header" role="banner">
      <button mat-icon-button class="ap-header__menu" (click)="menuToggle.emit()"
              aria-label="Toggle navigation menu">
        <mat-icon>menu</mat-icon>
      </button>

      <a class="ap-header__brand" routerLink="/" aria-label="AquaPlan home">
        <img src="assets/brand/mark.svg" alt="" class="ap-header__mark" width="24" height="24"/>
        <span class="ap-header__title">{{ 'app.title' | translate }}</span>
      </a>

      <span class="ap-header__spacer"></span>

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
        <button mat-button [matMenuTriggerFor]="langMenu" aria-label="Change language"
                class="ap-header__lang">
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
        <button mat-button [matMenuTriggerFor]="langMenu" aria-label="Change language"
                class="ap-header__lang">
          {{ localeService.currentLang().toUpperCase() }}
        </button>
      }
    </header>
  `,
  styles: [`
    /* AQ-423 — 64px fixed header, white surface with bottom border,
       no elevation. Uses border-over-shadow per design system rules. */
    :host { display: block; }

    .ap-header {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      height: 64px;
      display: flex;
      align-items: center;
      padding: 0 var(--space-6);
      gap: var(--space-2);
      background: var(--color-bg-surface);
      border-bottom: 1px solid var(--color-border-default);
      z-index: var(--z-header, 1100);
      box-sizing: border-box;
    }

    .ap-header__menu {
      color: var(--color-fg-default);
    }

    .ap-header__brand {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      text-decoration: none;
      color: var(--color-fg-default);
      margin-left: var(--space-1);
    }
    .ap-header__brand:hover { text-decoration: none; }

    .ap-header__mark {
      display: block;
      height: 24px;
      width: 24px;
    }

    .ap-header__title {
      font-family: var(--font-family-base);
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-16);
      color: var(--color-fg-default);
      letter-spacing: 0;
    }

    .ap-header__spacer { flex: 1 1 auto; }

    .ap-header__lang {
      color: var(--color-fg-muted) !important;
      --mdc-text-button-label-text-color: var(--color-fg-muted);
    }

    .user-chip {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      border-radius: var(--radius-sm);
      padding: 0 var(--space-3);
      color: var(--color-fg-default) !important;
      --mdc-text-button-label-text-color: var(--color-fg-default);
      border: 1px solid var(--color-border-default);
    }
    .user-chip:hover {
      background: var(--color-bg-subtle) !important;
    }
    .user-name {
      font-family: var(--font-family-base);
      font-size: var(--font-size-14);
      font-weight: var(--font-weight-medium);
    }

    /* AQ-378 — sync chips sit between spacer and language button. Fixed height
       to prevent toolbar layout shift. */
    .sync-chip {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      height: 28px;
      padding: 0 var(--space-3);
      margin-right: var(--space-1);
      border-radius: var(--radius-chip);
      font-size: var(--font-size-12);
      font-weight: var(--font-weight-medium);
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
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-12);
      margin-left: 2px;
    }
    .sync-chip--offline {
      background: var(--color-error-50);
      color: var(--color-error-700);
    }
    .sync-chip--pending {
      background: var(--color-warning-50);
      color: var(--color-warning-700);
    }
    .sync-chip--syncing {
      background: var(--color-info-50);
      color: var(--color-info-700);
    }

    @media (max-width: 767px) {
      .ap-header { padding: 0 var(--space-3); }
      .ap-header__title { font-size: var(--font-size-14); }
      .sync-chip { padding: 0 var(--space-2); margin-right: 2px; }
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
