import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';
import { LocaleService } from '../../services/locale.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar color="primary" class="header">
      <button mat-icon-button (click)="toggleSidebar()">
        <mat-icon>menu</mat-icon>
      </button>
      <span class="logo">{{ 'app.title' | translate }}</span>
      <span class="spacer"></span>

      <button mat-button [matMenuTriggerFor]="langMenu">
        {{ localeService.currentLang().toUpperCase() }}
      </button>
      <mat-menu #langMenu="matMenu">
        @for (lang of localeService.getSupportedLanguages(); track lang) {
          <button mat-menu-item (click)="localeService.switchLanguage(lang)">
            {{ lang.toUpperCase() }}
          </button>
        }
      </mat-menu>

      @if (authService.isAuthenticated()) {
        <button mat-button [matMenuTriggerFor]="userMenu">
          <mat-icon>person</mat-icon>
          {{ authService.currentUser()?.firstName }}
        </button>
        <mat-menu #userMenu="matMenu">
          <button mat-menu-item (click)="authService.logout()">
            <mat-icon>logout</mat-icon>
            {{ 'auth.logout' | translate }}
          </button>
        </mat-menu>
      }
    </mat-toolbar>
  `,
  styles: [`
    .header { position: fixed; top: 0; left: 0; right: 0; z-index: 1000; }
    .logo { margin-left: 8px; font-weight: 500; }
    .spacer { flex: 1 1 auto; }
  `],
})
export class HeaderComponent {
  readonly authService = inject(AuthService);
  readonly localeService = inject(LocaleService);
  onToggleSidebar?: () => void;

  toggleSidebar(): void {
    this.onToggleSidebar?.();
  }
}
