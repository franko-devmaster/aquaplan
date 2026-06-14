import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [MatIconModule, MatExpansionModule, RouterModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // AQ-423 — dark-navy sidebar (--color-primary-800), 240px fixed width,
  // logo-on-dark header, teal accent on active items. Replaces former
  // mat-nav-list / white sidebar pattern. Navigation markup stays the same
  // (<a routerLink> anchors) so guards, active detection, and click behaviour
  // are preserved.
  template: `
    <nav class="ap-sidebar" [attr.aria-label]="'a11y.mainNavigation' | translate">
      <div class="ap-sidebar__brand">
        <img src="assets/brand/logo-on-dark.svg" alt="AquaPlan" height="32"/>
      </div>

      <div class="ap-sidebar__scroll">
        <mat-accordion multi displayMode="flat" class="ap-sidebar__accordion">
          <!-- Group 1: Principal -->
          <mat-expansion-panel [expanded]="true" hideToggle="false" class="ap-sidebar__group">
            <mat-expansion-panel-header>
              <mat-panel-title>{{ 'nav.groupPrincipal' | translate }}</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="ap-sidebar__items">
              <a routerLink="/" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>home</mat-icon>
                <span>{{ 'nav.home' | translate }}</span>
              </a>
              <a routerLink="/sampling-plans" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>calendar_month</mat-icon>
                <span>{{ 'nav.samplingPlans' | translate }}</span>
              </a>
              <a routerLink="/sampling-rounds" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>route</mat-icon>
                <span>{{ 'nav.samplingRounds' | translate }}</span>
              </a>
              <!-- AQ-426 — Mandats list entry between Tournées et Résultats. -->
              <a routerLink="/orders" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>assignment</mat-icon>
                <span>{{ 'nav.orders' | translate }}</span>
              </a>
              <!-- AQ-415 — Results screen entry. Préleveur-only users are filtered out by the backend authorize roles. -->
              @if (!isPreleveurOnly()) {
                <a routerLink="/results" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>science</mat-icon>
                  <span>{{ 'nav.results' | translate }}</span>
                </a>
              }
            </div>
          </mat-expansion-panel>

          <!-- Group 2: Reseau -->
          <mat-expansion-panel class="ap-sidebar__group">
            <mat-expansion-panel-header>
              <mat-panel-title>{{ 'nav.groupReseau' | translate }}</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="ap-sidebar__items">
              <a routerLink="/sampling-locations" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>place</mat-icon>
                <span>{{ 'nav.samplingLocations' | translate }}</span>
              </a>
              <a routerLink="/sectors" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>map</mat-icon>
                <span>{{ 'nav.sectors' | translate }}</span>
              </a>
              @if (isAdmin()) {
                <a routerLink="/distributors" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>water_drop</mat-icon>
                  <span>{{ 'nav.distributors' | translate }}</span>
                </a>
              }
            </div>
          </mat-expansion-panel>

          <!-- Group 3: Catalogue d'analyses -->
          <mat-expansion-panel class="ap-sidebar__group">
            <mat-expansion-panel-header>
              <mat-panel-title>{{ 'nav.groupCatalog' | translate }}</mat-panel-title>
            </mat-expansion-panel-header>
            <div class="ap-sidebar__items">
              <a routerLink="/analysis-programs" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>playlist_add_check</mat-icon>
                <span>{{ 'nav.analysisPrograms' | translate }}</span>
              </a>
              <a routerLink="/analysis-profiles" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>science</mat-icon>
                <span>{{ 'nav.analysisProfiles' | translate }}</span>
              </a>
              <a routerLink="/analysis-containers" routerLinkActive="is-active"
                 (click)="navigated.emit()" class="ap-sidebar__item">
                <mat-icon>category</mat-icon>
                <span>{{ 'nav.analysisContainers' | translate }}</span>
              </a>
            </div>
          </mat-expansion-panel>

          <!-- Group 4: Administration (admin only) -->
          @if (isAdmin()) {
            <mat-expansion-panel class="ap-sidebar__group">
              <mat-expansion-panel-header>
                <mat-panel-title>{{ 'nav.groupAdmin' | translate }}</mat-panel-title>
              </mat-expansion-panel-header>
              <div class="ap-sidebar__items">
                <a routerLink="/admin/users" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>people</mat-icon>
                  <span>{{ 'nav.users' | translate }}</span>
                </a>
                <a routerLink="/admin/roles" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>admin_panel_settings</mat-icon>
                  <span>{{ 'nav.roles' | translate }}</span>
                </a>
                <a routerLink="/admin/order-status" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>swap_horiz</mat-icon>
                  <span>{{ 'nav.orderStatus' | translate }}</span>
                </a>
                <a routerLink="/admin/delegations" routerLinkActive="is-active"
                   (click)="navigated.emit()" class="ap-sidebar__item">
                  <mat-icon>swap_horiz</mat-icon>
                  <span>{{ 'nav.delegations' | translate }}</span>
                </a>
              </div>
            </mat-expansion-panel>
          }
        </mat-accordion>
      </div>
    </nav>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
      background: var(--color-primary-800);
    }

    .ap-sidebar {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--color-primary-800);
      color: var(--color-fg-on-dark);
    }

    .ap-sidebar__brand {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--space-4) var(--space-3);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      flex-shrink: 0;
    }
    .ap-sidebar__brand img {
      display: block;
      height: 32px;
      width: auto;
      max-width: 100%;
    }

    .ap-sidebar__scroll {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: var(--space-2) 0;
    }

    /* Override Material expansion-panel chrome for the dark-navy aesthetic */
    .ap-sidebar__accordion ::ng-deep .mat-expansion-panel {
      box-shadow: none !important;
      background: transparent;
      border-radius: 0;
      color: inherit;
    }
    .ap-sidebar__accordion ::ng-deep .mat-expansion-panel-header {
      padding: 0 var(--space-4) !important;
      height: 40px !important;
      min-height: 40px !important;
      background: transparent !important;
    }
    .ap-sidebar__accordion ::ng-deep .mat-expansion-panel-header:hover {
      background: rgba(255, 255, 255, 0.04) !important;
    }
    .ap-sidebar__accordion ::ng-deep .mat-expansion-panel-header-title {
      font-family: var(--font-family-base);
      font-size: var(--font-size-12);
      font-weight: var(--font-weight-semibold);
      letter-spacing: var(--letter-spacing-wide);
      text-transform: uppercase;
      color: rgba(255, 255, 255, 0.6);
      margin-right: 0;
    }
    .ap-sidebar__accordion ::ng-deep .mat-expansion-indicator::after {
      color: rgba(255, 255, 255, 0.6);
    }
    .ap-sidebar__accordion ::ng-deep .mat-expansion-panel-body {
      padding: 0 !important;
    }

    .ap-sidebar__items {
      display: flex;
      flex-direction: column;
      padding: var(--space-1) 0 var(--space-2);
    }

    .ap-sidebar__item {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      padding: 0 var(--space-4);
      height: 40px;
      color: rgba(255, 255, 255, 0.78);
      text-decoration: none;
      font-family: var(--font-family-base);
      font-size: var(--font-size-14);
      font-weight: var(--font-weight-medium);
      border-left: 3px solid transparent;
      transition: background var(--duration-fast) var(--easing-standard),
                  color var(--duration-fast) var(--easing-standard);
      box-sizing: border-box;
    }
    .ap-sidebar__item mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      line-height: 20px;
      color: inherit;
    }
    .ap-sidebar__item:hover {
      background: rgba(255, 255, 255, 0.08);
      color: var(--color-fg-on-dark);
      text-decoration: none;
    }
    .ap-sidebar__item.is-active {
      background: rgba(255, 255, 255, 0.14);
      color: var(--color-fg-on-dark);
      border-left-color: var(--color-accent-500);
    }

    /* Mobile: taller touch targets (>=44px) */
    @media (max-width: 767px) {
      .ap-sidebar__item { height: 44px; }
    }
  `],
})
export class SidebarComponent {
  private readonly authService = inject(AuthService);
  readonly navigated = output();

  // F-012 — centralised role checks (AuthService is the single source of truth).
  // F-034 — the previously dead `isPreleveur` computed was removed (only
  // isAdmin / isPreleveurOnly are consumed by the template).
  readonly isAdmin = this.authService.isAdmin;

  /** AQ-415 — Préleveur seul (sans Requérant/RequérantPréleveur) : pas d'accès à /results. */
  readonly isPreleveurOnly = this.authService.isPreleveurOnly;
}
