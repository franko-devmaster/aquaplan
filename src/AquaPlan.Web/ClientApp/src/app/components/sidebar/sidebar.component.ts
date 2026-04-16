import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [MatListModule, MatIconModule, MatExpansionModule, RouterModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="Main navigation">
      <mat-accordion multi>
        <!-- Group 1: Principal -->
        <mat-expansion-panel [expanded]="true">
          <mat-expansion-panel-header>
            <mat-panel-title>{{ 'nav.groupPrincipal' | translate }}</mat-panel-title>
          </mat-expansion-panel-header>
          <mat-nav-list>
            <a mat-list-item routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>home</mat-icon>
              <span matListItemTitle>{{ 'nav.home' | translate }}</span>
            </a>
            <a mat-list-item routerLink="/sampling-plans" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>calendar_month</mat-icon>
              <span matListItemTitle>{{ 'nav.samplingPlans' | translate }}</span>
            </a>
            <a mat-list-item routerLink="/sampling-rounds" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>route</mat-icon>
              <span matListItemTitle>{{ 'nav.samplingRounds' | translate }}</span>
            </a>
          </mat-nav-list>
        </mat-expansion-panel>

        <!-- Group 2: Reseau -->
        <mat-expansion-panel>
          <mat-expansion-panel-header>
            <mat-panel-title>{{ 'nav.groupReseau' | translate }}</mat-panel-title>
          </mat-expansion-panel-header>
          <mat-nav-list>
            <a mat-list-item routerLink="/sampling-locations" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>place</mat-icon>
              <span matListItemTitle>{{ 'nav.samplingLocations' | translate }}</span>
            </a>
            <a mat-list-item routerLink="/sectors" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>map</mat-icon>
              <span matListItemTitle>{{ 'nav.sectors' | translate }}</span>
            </a>
            @if (isAdmin()) {
              <a mat-list-item routerLink="/distributors" routerLinkActive="active"
                 (click)="navigated.emit()">
                <mat-icon matListItemIcon>water_drop</mat-icon>
                <span matListItemTitle>{{ 'nav.distributors' | translate }}</span>
              </a>
            }
          </mat-nav-list>
        </mat-expansion-panel>

        <!-- Group 3: Catalogue d'analyses -->
        <mat-expansion-panel>
          <mat-expansion-panel-header>
            <mat-panel-title>{{ 'nav.groupCatalog' | translate }}</mat-panel-title>
          </mat-expansion-panel-header>
          <mat-nav-list>
            <a mat-list-item routerLink="/analysis-programs" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>playlist_add_check</mat-icon>
              <span matListItemTitle>{{ 'nav.analysisPrograms' | translate }}</span>
            </a>
            <a mat-list-item routerLink="/analysis-profiles" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>science</mat-icon>
              <span matListItemTitle>{{ 'nav.analysisProfiles' | translate }}</span>
            </a>
            <a mat-list-item routerLink="/analysis-containers" routerLinkActive="active"
               (click)="navigated.emit()">
              <mat-icon matListItemIcon>category</mat-icon>
              <span matListItemTitle>{{ 'nav.analysisContainers' | translate }}</span>
            </a>
          </mat-nav-list>
        </mat-expansion-panel>

        <!-- Group 4: Administration (admin only) -->
        @if (isAdmin()) {
          <mat-expansion-panel>
            <mat-expansion-panel-header>
              <mat-panel-title>{{ 'nav.groupAdmin' | translate }}</mat-panel-title>
            </mat-expansion-panel-header>
            <mat-nav-list>
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
              <a mat-list-item routerLink="/admin/delegations" routerLinkActive="active"
                 (click)="navigated.emit()">
                <mat-icon matListItemIcon>swap_horiz</mat-icon>
                <span matListItemTitle>{{ 'nav.delegations' | translate }}</span>
              </a>
            </mat-nav-list>
          </mat-expansion-panel>
        }
      </mat-accordion>
    </nav>
  `,
  styles: [`
    .active { background-color: rgba(0, 0, 0, 0.04); }
    mat-expansion-panel {
      box-shadow: none !important;
      background: transparent;
    }
    ::ng-deep .mat-expansion-panel-body {
      padding: 0 !important;
    }
    mat-expansion-panel-header {
      padding: 0 16px !important;
      height: 36px !important;
      min-height: 36px !important;
    }
    ::ng-deep .mat-expansion-panel-header-title {
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      color: #888;
    }
    mat-nav-list {
      padding-top: 0;
    }
  `],
})
export class SidebarComponent {
  private readonly authService = inject(AuthService);
  readonly navigated = output();

  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );

  readonly isPreleveur = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return false;
    return user.roles.some(r => r.toLowerCase().includes('réleveur')) && !user.roles.includes('Administrator');
  });
}
