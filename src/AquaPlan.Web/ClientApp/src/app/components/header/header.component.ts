import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { LocaleService } from '../../services/locale.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar color="primary" class="header">
      <button mat-icon-button (click)="menuToggle.emit()" aria-label="Toggle navigation menu">
        <mat-icon>menu</mat-icon>
      </button>
      <span class="logo">{{ 'app.title' | translate }}</span>
      <span class="spacer"></span>

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
      border: 1px solid rgba(255, 255, 255, 0.5);
    }
    .user-name {
      font-size: 14px;
      font-weight: 500;
    }
    @media (max-width: 767px) {
      .logo { font-size: 16px; }
    }
  `],
})
export class HeaderComponent {
  readonly authService = inject(AuthService);
  readonly localeService = inject(LocaleService);
  readonly isMobile = input(false);
  readonly menuToggle = output();

  readonly userDisplayName = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '';
    const initial = user.firstName?.charAt(0).toUpperCase() ?? '';
    return `${initial}. ${user.lastName}`;
  });
}
