import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../services/auth.service';
import { OrderApiService } from '../../services/order-api.service';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { OrderStatus, ResultsStatus } from '../../models/order.model';
import {
    SamplingRoundStatus,
    SamplingRoundStatusLabels,
    SamplingRoundListDto,
} from '../../models/sampling-round.model';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';
import { roundStatusVariant } from '../../utils/status-variant';
import { RecentResultsZoneComponent } from '../results/recent-results-zone.component';
import { DashboardRefreshService } from '../../services/dashboard-refresh.service';

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [
        CommonModule,
        TranslateModule,
        MatCardModule,
        MatIconModule,
        MatButtonModule,
        StatusChipComponent,
        RecentResultsZoneComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        <div class="dashboard">
            <!-- Header (AQ-424) -->
            <header class="dashboard-header">
                <h1 class="dashboard-title">{{ 'dashboard.title' | translate }}</h1>
                @if (greetingName()) {
                    <p class="dashboard-subtitle">{{ 'dashboard.welcome' | translate:{ name: greetingName() } }}</p>
                }
            </header>

            <!-- Stats Cards -->
            <div class="stats-grid">
                <button type="button" class="stat-card" (click)="openOrdersToFinalize()">
                    <span class="stat-icon primary" aria-hidden="true">
                        <mat-icon>assignment</mat-icon>
                    </span>
                    <span class="stat-info">
                        <span class="stat-value">{{ ordersToFinalizeCount() }}</span>
                        <span class="stat-label">{{ 'dashboard.ordersToFinalize' | translate }}</span>
                    </span>
                </button>

                <!-- AQ-411 — "Mes tournées": scoped to current préleveur. -->
                <button type="button" class="stat-card" (click)="openMyRounds()">
                    <span class="stat-icon info" aria-hidden="true">
                        <mat-icon>event</mat-icon>
                    </span>
                    <span class="stat-info">
                        <span class="stat-value">{{ plannedRoundsCount() }}</span>
                        <span class="stat-label">{{ 'dashboard.myRounds' | translate }}</span>
                    </span>
                </button>

                <button type="button" class="stat-card" (click)="openConformOrders()">
                    <span class="stat-icon success" aria-hidden="true">
                        <mat-icon>check_circle</mat-icon>
                    </span>
                    <span class="stat-info">
                        <span class="stat-value">{{ conformCount() }}</span>
                        <span class="stat-label">{{ 'dashboard.conform' | translate }}</span>
                    </span>
                </button>

                <button type="button" class="stat-card" (click)="openNonConformOrders()">
                    <span class="stat-icon danger" aria-hidden="true">
                        <mat-icon>error</mat-icon>
                    </span>
                    <span class="stat-info">
                        <span class="stat-value">{{ nonConformCount() }}</span>
                        <span class="stat-label">{{ 'dashboard.nonConform' | translate }}</span>
                    </span>
                </button>
            </div>

            <!-- Main Content Grid -->
            <div class="widgets-grid">
                <!-- Prochaines tournées -->
                <section class="widget widget-rounds">
                    <header class="widget-header">
                        <h2 class="widget-title">
                            <mat-icon aria-hidden="true">route</mat-icon>
                            <span>{{ 'dashboard.upcomingRounds' | translate }}</span>
                        </h2>
                        <button mat-button color="primary" (click)="navigateTo('/sampling-rounds')">
                            {{ 'dashboard.viewAll' | translate }}
                        </button>
                    </header>
                    <div class="widget-body">
                        @if (upcomingRounds().length === 0) {
                            <div class="empty-state">
                                <mat-icon>event_busy</mat-icon>
                                <span>{{ 'dashboard.noUpcomingRounds' | translate }}</span>
                            </div>
                        } @else {
                            <table class="rounds-table">
                                <thead>
                                    <tr>
                                        <th>{{ 'samplingRounds.deadline' | translate }}</th>
                                        <th>{{ 'samplingRounds.name' | translate }}</th>
                                        <th>{{ 'samplingRounds.orderCount' | translate }}</th>
                                        <th>{{ 'common.status' | translate }}</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (round of upcomingRounds(); track round.id) {
                                        <tr class="clickable-row" (click)="navigateTo('/sampling-rounds/' + round.id)">
                                            <td class="mono">{{ round.deadline | date:'dd.MM.yyyy' }}</td>
                                            <td>{{ round.name }}</td>
                                            <td>{{ round.orderCount }}</td>
                                            <td>
                                                <app-status-chip
                                                    [variant]="getRoundStatusVariant(round.status)"
                                                    [label]="(getRoundStatusLabel(round.status) | translate)"></app-status-chip>
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                </section>

                <!-- Admin Panel -->
                @if (isAdmin()) {
                    <section class="widget widget-admin">
                        <header class="widget-header">
                            <h2 class="widget-title">
                                <mat-icon aria-hidden="true">admin_panel_settings</mat-icon>
                                <span>{{ 'dashboard.pendingActions' | translate }}</span>
                            </h2>
                        </header>
                        <div class="widget-body">
                            <div class="admin-list">
                                <button type="button" class="admin-item"
                                        (click)="navigateTo('/sampling-locations', { validation: 'pending' })">
                                    <mat-icon class="icon-warn">pending_actions</mat-icon>
                                    <span class="admin-count">{{ ldpToValidateCount() }}</span>
                                    <span class="admin-label">{{ 'dashboard.ldpToValidate' | translate }}</span>
                                </button>
                                <div class="admin-item stub">
                                    <mat-icon class="icon-primary">comment</mat-icon>
                                    <span class="admin-count">0</span>
                                    <span class="admin-label">{{ 'dashboard.ordersWithComments' | translate }}</span>
                                </div>
                            </div>
                        </div>
                    </section>
                }
            </div>

            <!-- Results Widget (full width) — AQ-417 -->
            <section class="widget widget-results">
                <header class="widget-header">
                    <h2 class="widget-title">
                        <mat-icon aria-hidden="true">science</mat-icon>
                        <span>{{ 'dashboard.recentResults' | translate }}</span>
                    </h2>
                    <button mat-button color="primary" (click)="navigateTo('/results')">
                        {{ 'dashboard.viewAll' | translate }}
                    </button>
                </header>
                <div class="widget-body">
                    <app-recent-results-zone></app-recent-results-zone>
                </div>
            </section>
        </div>
    `,
    styles: [`
        :host { display: block; background: var(--color-bg-page); }
        .dashboard { padding: var(--space-6); max-width: 1440px; margin: 0 auto; display: flex; flex-direction: column; gap: var(--space-6); }
        .dashboard-header { display: flex; flex-direction: column; gap: var(--space-1); }
        .dashboard-title { margin: 0; font-family: var(--font-family-base); font-size: var(--font-size-32); font-weight: var(--font-weight-semibold); color: var(--color-fg-default); letter-spacing: var(--letter-spacing-tight); }
        .dashboard-subtitle { margin: 0; font-size: var(--font-size-14); color: var(--color-fg-muted); }
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: var(--space-4); }
        .stat-card { display: flex; align-items: center; gap: var(--space-4); padding: var(--space-5); background: var(--color-bg-surface); border: 1px solid var(--color-border-default); border-radius: var(--radius-md); cursor: pointer; text-align: left; font: inherit; color: inherit; transition: box-shadow var(--duration-fast), border-color var(--duration-fast); }
        .stat-card:hover { box-shadow: var(--elevation-2); border-color: var(--color-primary-300); }
        .stat-card:focus-visible { outline: none; box-shadow: var(--focus-ring); }
        .stat-icon { width: 44px; height: 44px; border-radius: var(--radius-md); display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
        .stat-icon mat-icon { color: #fff; font-size: 22px; width: 22px; height: 22px; }
        .stat-icon.primary { background: var(--color-primary-500); }
        .stat-icon.info    { background: var(--color-info-500); }
        .stat-icon.danger  { background: var(--color-error-500); }
        .stat-icon.success { background: var(--color-success-500); }
        .stat-info { display: flex; flex-direction: column; gap: var(--space-1); }
        .stat-value { font-family: var(--font-family-mono); font-size: var(--font-size-26); font-weight: var(--font-weight-semibold); line-height: var(--line-height-tight); color: var(--color-fg-default); }
        .stat-label { font-size: var(--font-size-13); color: var(--color-fg-muted); }
        .widgets-grid { display: grid; grid-template-columns: 2fr 1fr; gap: var(--space-4); }
        @media (max-width: 1024px) { .widgets-grid { grid-template-columns: 1fr; } }
        .widget { background: var(--color-bg-surface); border: 1px solid var(--color-border-default); border-radius: var(--radius-md); overflow: hidden; display: flex; flex-direction: column; }
        .widget-header { display: flex; justify-content: space-between; align-items: center; gap: var(--space-3); padding: var(--space-4) var(--space-5); border-bottom: 1px solid var(--color-border-default); }
        .widget-title { margin: 0; display: flex; align-items: center; gap: var(--space-2); font-family: var(--font-family-base); font-size: var(--font-size-16); font-weight: var(--font-weight-semibold); color: var(--color-fg-default); }
        .widget-title mat-icon { color: var(--color-fg-muted); font-size: 20px; width: 20px; height: 20px; }
        .widget-body { padding: var(--space-4) var(--space-5); }
        .rounds-table { width: 100%; border-collapse: collapse; }
        .rounds-table th { text-align: left; padding: var(--space-2) var(--space-3); font-size: var(--font-size-12); font-weight: var(--font-weight-semibold); color: var(--color-fg-muted); text-transform: uppercase; letter-spacing: var(--letter-spacing-wide); border-bottom: 1px solid var(--color-border-default); background: var(--color-bg-sunken); }
        .rounds-table td { padding: var(--space-3); font-size: var(--font-size-14); color: var(--color-fg-default); border-bottom: 1px solid var(--color-border-subtle); }
        .rounds-table td.mono { font-family: var(--font-family-mono); }
        .clickable-row { cursor: pointer; transition: background-color var(--duration-fast); }
        .clickable-row:hover { background: var(--color-row-hover); }
        .empty-state { display: flex; flex-direction: column; align-items: center; gap: var(--space-2); padding: var(--space-8); color: var(--color-fg-subtle); text-align: center; }
        .empty-state mat-icon { font-size: 32px; width: 32px; height: 32px; color: var(--color-fg-subtle); }
        .admin-list { display: flex; flex-direction: column; gap: var(--space-2); }
        .admin-item { display: flex; align-items: center; gap: var(--space-3); padding: var(--space-3); border-radius: var(--radius-sm); background: transparent; border: none; cursor: pointer; text-align: left; font: inherit; color: inherit; width: 100%; transition: background-color var(--duration-fast); }
        .admin-item:hover { background: var(--color-row-hover); }
        .admin-item.stub, .admin-item.stub:hover { cursor: default; background: transparent; }
        .admin-item .icon-warn { color: var(--color-warning-600); }
        .admin-item .icon-primary { color: var(--color-primary-600); }
        .admin-count { font-family: var(--font-family-mono); font-size: var(--font-size-18); font-weight: var(--font-weight-semibold); min-width: 28px; color: var(--color-fg-default); }
        .admin-label { font-size: var(--font-size-14); color: var(--color-fg-default); }
        @media (max-width: 768px) {
            .dashboard { padding: var(--space-3); gap: var(--space-4); }
            .dashboard-title { font-size: var(--font-size-26); }
            .stats-grid { grid-template-columns: 1fr; gap: var(--space-3); }
            .widget-header, .widget-body { padding: var(--space-3) var(--space-4); }
        }
    `],
})
export class HomeComponent implements OnInit {
    private readonly authService = inject(AuthService);
    private readonly orderApi = inject(OrderApiService);
    private readonly roundApi = inject(SamplingRoundApiService);
    private readonly samplingLocationApi = inject(SamplingLocationApiService);
    private readonly router = inject(Router);
    private readonly dashboardRefresh = inject(DashboardRefreshService);

    readonly ordersToFinalizeCount = signal(0);
    readonly plannedRoundsCount = signal(0);
    readonly conformCount = signal(0);
    readonly nonConformCount = signal(0);
    readonly upcomingRounds = signal<SamplingRoundListDto[]>([]);
    readonly ldpToValidateCount = signal(0);

    // F-012 — centralised role check.
    readonly isAdmin = this.authService.isAdmin;

    // AQ-424 — display "Bienvenue, Prénom Nom" above the dashboard title.
    readonly greetingName = computed(() => {
        const u = this.authService.currentUser();
        if (!u) {
            return '';
        }
        const first = (u.firstName ?? '').trim();
        const last = (u.lastName ?? '').trim();
        if (first || last) {
            return [first, last].filter(Boolean).join(' ');
        }
        return u.email ?? '';
    });

    constructor() {
        // F-035 — reload the dashboard counters whenever a bulk action (validate /
        // transmit / finalize) reports it changed order statuses, so the tiles never
        // stay frozen on stale numbers. Skips the very first emission: ngOnInit already
        // performs the initial load.
        let firstRun = true;
        effect(() => {
            this.dashboardRefresh.version();
            if (firstRun) {
                firstRun = false;
                return;
            }
            void this.loadDashboardData();
        });
    }

    ngOnInit(): void {
        this.loadDashboardData();
    }

    navigateTo(path: string, queryParams?: Record<string, string>): void {
        this.router.navigate([path], queryParams ? { queryParams } : undefined);
    }

    openOrdersToFinalize(): void {
        this.router.navigate(['/orders'], {
            queryParams: { statuses: `${OrderStatus.InProgress},${OrderStatus.Completed}` },
        });
    }

    openMyRounds(): void {
        // AQ-411 — deep-link into the rounds list pre-filtered by current user + Assigned/InProgress statuses.
        const userId = this.authService.currentUser()?.id;
        const queryParams: Record<string, string> = {
            status: `${SamplingRoundStatus.Assigned},${SamplingRoundStatus.InProgress}`,
        };
        if (userId) {
            queryParams['preleveurId'] = userId;
        }
        this.router.navigate(['/sampling-rounds'], { queryParams });
    }

    openConformOrders(): void {
        this.router.navigate(['/orders'], {
            queryParams: { resultsStatus: ResultsStatus.Conform },
        });
    }

    openNonConformOrders(): void {
        this.router.navigate(['/orders'], {
            queryParams: { resultsStatus: ResultsStatus.NonConform },
        });
    }

    getRoundStatusVariant(status: SamplingRoundStatus): StatusChipVariant {
        return roundStatusVariant(status);
    }

    getRoundStatusLabel(status: SamplingRoundStatus): string {
        return SamplingRoundStatusLabels[status] ?? '';
    }

    private async loadDashboardData(): Promise<void> {
        const tasks: Promise<void>[] = [
            this.loadOrdersToFinalize(),
            this.loadUpcomingRounds(),
            this.loadDashboardSummary(),
        ];
        if (this.isAdmin()) {
            tasks.push(this.loadLdpToValidate());
        }
        await Promise.all(tasks);
    }

    private async loadDashboardSummary(): Promise<void> {
        try {
            const summary = await firstValueFrom(this.orderApi.getDashboardSummary());
            this.conformCount.set(summary.conformCount);
            this.nonConformCount.set(summary.nonConformCount);
        } catch {
            // Silently handle error
        }
    }

    private async loadOrdersToFinalize(): Promise<void> {
        try {
            const result = await firstValueFrom(
                this.orderApi.getFiltered({
                    statuses: [OrderStatus.InProgress, OrderStatus.Completed],
                    page: 1,
                    pageSize: 1,
                })
            );
            this.ordersToFinalizeCount.set(result.totalCount);
        } catch {
            // Silently handle error — dashboard shows 0
        }
    }

    private async loadUpcomingRounds(): Promise<void> {
        try {
            // AQ-411 — "Mes tournées": scope dashboard tile to rounds assigned to the current user
            // (Assigned + InProgress). Admins still see only their own assignments here; they have
            // the dedicated distributor/global view via the list page.
            const userId = this.authService.currentUser()?.id;
            const result = await firstValueFrom(
                this.roundApi.getFiltered({
                    statuses: [SamplingRoundStatus.Assigned, SamplingRoundStatus.InProgress],
                    preleveurId: userId,
                    page: 1,
                    pageSize: 10,
                    sortBy: 'deadline',
                    sortDescending: false,
                })
            );
            this.upcomingRounds.set(result.items);
            this.plannedRoundsCount.set(result.totalCount);
        } catch {
            // Silently handle error
        }
    }

    private async loadLdpToValidate(): Promise<void> {
        try {
            const result = await firstValueFrom(this.samplingLocationApi.getUnvalidated());
            this.ldpToValidateCount.set(result.length);
        } catch {
            // Silently handle error — fallback 0
        }
    }
}
