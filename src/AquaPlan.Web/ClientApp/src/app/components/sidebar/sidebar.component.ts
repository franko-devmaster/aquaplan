import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [MatListModule, MatIconModule, RouterModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-nav-list>
      <a mat-list-item routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
        <mat-icon matListItemIcon>home</mat-icon>
        <span matListItemTitle>{{ 'nav.home' | translate }}</span>
      </a>
      <a mat-list-item routerLink="/orders" routerLinkActive="active">
        <mat-icon matListItemIcon>assignment</mat-icon>
        <span matListItemTitle>{{ 'nav.orders' | translate }}</span>
      </a>
      <a mat-list-item routerLink="/sampling-locations" routerLinkActive="active">
        <mat-icon matListItemIcon>place</mat-icon>
        <span matListItemTitle>{{ 'nav.samplingLocations' | translate }}</span>
      </a>
      @if (isAdmin()) {
        <mat-divider></mat-divider>
        <a mat-list-item routerLink="/admin/users" routerLinkActive="active">
          <mat-icon matListItemIcon>people</mat-icon>
          <span matListItemTitle>{{ 'nav.users' | translate }}</span>
        </a>
        <a mat-list-item routerLink="/admin/roles" routerLinkActive="active">
          <mat-icon matListItemIcon>admin_panel_settings</mat-icon>
          <span matListItemTitle>{{ 'nav.roles' | translate }}</span>
        </a>
      }
    </mat-nav-list>
  `,
  styles: [`
    .active { background-color: rgba(0, 0, 0, 0.04); }
  `],
})
export class SidebarComponent {
  private readonly authService = inject(AuthService);

  isAdmin(): boolean {
    const user = this.authService.currentUser();
    return user?.roles.includes('Administrator') ?? false;
  }
}
