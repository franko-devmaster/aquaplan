import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [MatListModule, MatIconModule, MatDividerModule, RouterModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="Main navigation">
    <mat-nav-list>
      <a mat-list-item routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"
         (click)="navigated.emit()">
        <mat-icon matListItemIcon>home</mat-icon>
        <span matListItemTitle>{{ 'nav.home' | translate }}</span>
      </a>
      <a mat-list-item routerLink="/orders" routerLinkActive="active"
         (click)="navigated.emit()">
        <mat-icon matListItemIcon>assignment</mat-icon>
        <span matListItemTitle>{{ 'nav.orders' | translate }}</span>
      </a>
      <a mat-list-item routerLink="/sampling-locations" routerLinkActive="active"
         (click)="navigated.emit()">
        <mat-icon matListItemIcon>place</mat-icon>
        <span matListItemTitle>{{ 'nav.samplingLocations' | translate }}</span>
      </a>
      <mat-divider></mat-divider>
      <div class="nav-section-label">{{ 'nav.analysisCatalog' | translate }}</div>
      <a mat-list-item routerLink="/analysis-profiles" routerLinkActive="active"
         (click)="navigated.emit()">
        <mat-icon matListItemIcon>science</mat-icon>
        <span matListItemTitle>{{ 'nav.analysisProfiles' | translate }}</span>
      </a>
      <a mat-list-item routerLink="/analysis-programs" routerLinkActive="active"
         (click)="navigated.emit()">
        <mat-icon matListItemIcon>playlist_add_check</mat-icon>
        <span matListItemTitle>{{ 'nav.analysisPrograms' | translate }}</span>
      </a>
      @if (isAdmin()) {
        <a mat-list-item routerLink="/distributors" routerLinkActive="active"
           (click)="navigated.emit()">
          <mat-icon matListItemIcon>water_drop</mat-icon>
          <span matListItemTitle>{{ 'nav.distributors' | translate }}</span>
        </a>
        <mat-divider></mat-divider>
        <a mat-list-item routerLink="/admin/users" routerLinkActive="active"
           (click)="navigated.emit()">
          <mat-icon matListItemIcon>people</mat-icon>
          <span matListItemTitle>{{ 'nav.users' | translate }}</span>
        </a>
        <a mat-list-item routerLink="/admin/roles" routerLinkActive="active"
           (click)="navigated.emit()">
          <mat-icon matListItemIcon>admin_panel_settings</mat-icon>
          <span matListItemTitle>{{ 'nav.roles' | translate }}</span>
        </a>
        <a mat-list-item routerLink="/admin/order-status" routerLinkActive="active"
           (click)="navigated.emit()">
          <mat-icon matListItemIcon>swap_horiz</mat-icon>
          <span matListItemTitle>{{ 'nav.orderStatus' | translate }}</span>
        </a>
        <a mat-list-item routerLink="/admin/validation-queue" routerLinkActive="active"
           (click)="navigated.emit()">
          <mat-icon matListItemIcon>fact_check</mat-icon>
          <span matListItemTitle>{{ 'nav.validationQueue' | translate }}</span>
        </a>
      }
    </mat-nav-list>
    </nav>
  `,
  styles: [`
    .active { background-color: rgba(0, 0, 0, 0.04); }
    .nav-section-label { padding: 8px 16px 4px; font-size: 12px; color: #888; text-transform: uppercase; letter-spacing: 0.5px; }
  `],
})
export class SidebarComponent {
  private readonly authService = inject(AuthService);
  readonly navigated = output();

  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );
}
