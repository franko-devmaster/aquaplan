import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
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
import { OrderStatus } from '../../models/order.model';
import {
    SamplingRoundStatus,
    SamplingRoundStatusLabels,
    SamplingRoundListDto,
} from '../../models/sampling-round.model';

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [
        CommonModule,
        TranslateModule,
        MatCardModule,
        MatIconModule,
        MatButtonModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        <div class="dashboard">
            <h1>{{ 'dashboard.title' | translate }}</h1>

            <!-- Stats Cards -->
            <div class="stats-grid">
                <mat-card class="stat-card clickable" (click)="openOrdersToFinalize()">
                    <mat-card-content>
                        <div class="stat-icon primary">
                            <mat-icon>assignment</mat-icon>
                        </div>
                        <div class="stat-info">
                            <span class="stat-value">{{ ordersToFinalizeCount() }}</span>
                            <span class="stat-label">{{ 'dashboard.ordersToFinalize' | translate }}</span>
                        </div>
                    </mat-card-content>
                </mat-card>

                <mat-card class="stat-card clickable" (click)="openPlannedRounds()">
                    <mat-card-content>
                        <div class="stat-icon accent">
                            <mat-icon>event</mat-icon>
                        </div>
                        <div class="stat-info">
                            <span class="stat-value">{{ plannedRoundsCount() }}</span>
                            <span class="stat-label">{{ 'dashboard.plannedRounds' | translate }}</span>
                        </div>
                    </mat-card-content>
                </mat-card>

                <mat-card class="stat-card stub">
                    <mat-card-content>
                        <div class="stat-icon danger">
                            <mat-icon>warning</mat-icon>
                        </div>
                        <div class="stat-info">
                            <span class="stat-value">{{ nonConformitiesCount() }}</span>
                            <span class="stat-label">{{ 'dashboard.nonConformities' | translate }}</span>
                        </div>
                    </mat-card-content>
                </mat-card>
            </div>

            <!-- Main Content Grid -->
            <div class="widgets-grid">
                <!-- Prochaines tournees -->
                <mat-card class="widget-rounds">
                    <mat-card-header>
                        <mat-card-title>
                            <mat-icon>route</mat-icon>
                            {{ 'dashboard.upcomingRounds' | translate }}
                        </mat-card-title>
                        <button mat-button color="primary" (click)="navigateTo('/sampling-rounds')">
                            {{ 'dashboard.viewAll' | translate }}
                        </button>
                    </mat-card-header>
                    <mat-card-content>
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
                                            <td>{{ round.deadline | date:'dd.MM.yyyy' }}</td>
                                            <td>{{ round.name }}</td>
                                            <td>{{ round.orderCount }}</td>
                                            <td>
                                                <span class="status-badge-round"
                                                      [ngClass]="getRoundStatusClass(round.status)">
                                                    {{ getRoundStatusLabel(round.status) | translate }}
                                                </span>
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </mat-card-content>
                </mat-card>

                <!-- Admin Panel -->
                @if (isAdmin()) {
                    <mat-card class="widget-admin">
                        <mat-card-header>
                            <mat-card-title>
                                <mat-icon>admin_panel_settings</mat-icon>
                                {{ 'dashboard.pendingActions' | translate }}
                            </mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <div class="admin-list">
                                <div class="admin-item" (click)="navigateTo('/sampling-locations', { validation: 'pending' })">
                                    <mat-icon color="warn">pending_actions</mat-icon>
                                    <span class="admin-count">{{ ldpToValidateCount() }}</span>
                                    <span class="admin-label">{{ 'dashboard.ldpToValidate' | translate }}</span>
                                </div>
                                <div class="admin-item stub">
                                    <mat-icon color="primary">comment</mat-icon>
                                    <span class="admin-count">0</span>
                                    <span class="admin-label">{{ 'dashboard.ordersWithComments' | translate }}</span>
                                </div>
                            </div>
                        </mat-card-content>
                    </mat-card>
                }
            </div>

            <!-- Results Widget (full width) -->
            <mat-card class="widget-results">
                <mat-card-header>
                    <mat-card-title>
                        <mat-icon>science</mat-icon>
                        {{ 'dashboard.recentResults' | translate }}
                    </mat-card-title>
                </mat-card-header>
                <mat-card-content>
                    <div class="info-box">
                        <mat-icon>info</mat-icon>
                        <span>{{ 'dashboard.resultsPlaceholder' | translate }}</span>
                    </div>
                </mat-card-content>
            </mat-card>
        </div>
    `,
    styles: [`
        .dashboard {
            padding: 24px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .dashboard h1 {
            margin-bottom: 24px;
            font-weight: 400;
            color: rgba(0, 0, 0, 0.87);
        }

        /* Stats Grid */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 16px;
            margin-bottom: 24px;
        }

        .stat-card mat-card-content {
            display: flex;
            align-items: center;
            gap: 16px;
            padding: 16px !important;
        }

        .stat-card.clickable {
            cursor: pointer;
            transition: transform 0.15s ease, box-shadow 0.15s ease;
        }

        .stat-card.clickable:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
        }

        .stat-card.stub {
            cursor: default;
        }

        .stat-icon {
            width: 48px;
            height: 48px;
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
        }

        .stat-icon mat-icon {
            color: white;
            font-size: 24px;
            width: 24px;
            height: 24px;
        }

        .stat-icon.primary { background-color: #1976d2; }
        .stat-icon.accent { background-color: #ff9800; }
        .stat-icon.danger { background-color: #d32f2f; }
        .stat-icon.success { background-color: #388e3c; }

        .stat-info {
            display: flex;
            flex-direction: column;
        }

        .stat-value {
            font-size: 28px;
            font-weight: 600;
            line-height: 1.2;
            color: rgba(0, 0, 0, 0.87);
        }

        .stat-label {
            font-size: 13px;
            color: rgba(0, 0, 0, 0.6);
        }

        /* Widgets Grid */
        .widgets-grid {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 16px;
            margin-bottom: 16px;
        }

        @media (max-width: 768px) {
            .widgets-grid {
                grid-template-columns: 1fr;
            }
        }

        mat-card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 16px 16px 0 16px;
        }

        mat-card-title {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 16px;
            font-weight: 500;
        }

        mat-card-title mat-icon {
            color: rgba(0, 0, 0, 0.54);
        }

        /* Rounds Table */
        .rounds-table {
            width: 100%;
            border-collapse: collapse;
        }

        .rounds-table th {
            text-align: left;
            padding: 8px 12px;
            font-size: 12px;
            font-weight: 500;
            color: rgba(0, 0, 0, 0.54);
            text-transform: uppercase;
            letter-spacing: 0.5px;
            border-bottom: 1px solid rgba(0, 0, 0, 0.12);
        }

        .rounds-table td {
            padding: 10px 12px;
            font-size: 14px;
            border-bottom: 1px solid rgba(0, 0, 0, 0.06);
        }

        .clickable-row {
            cursor: pointer;
            transition: background-color 0.15s;
        }

        .clickable-row:hover {
            background-color: rgba(0, 0, 0, 0.04);
        }


        /* Empty state */
        .empty-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 8px;
            padding: 32px;
            color: rgba(0, 0, 0, 0.38);
        }

        .empty-state mat-icon {
            font-size: 48px;
            width: 48px;
            height: 48px;
        }

        /* Admin Panel */
        .admin-list {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .admin-item {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 12px;
            border-radius: 8px;
            cursor: pointer;
            transition: background-color 0.15s;
        }

        .admin-item:hover {
            background-color: rgba(0, 0, 0, 0.04);
        }

        .admin-item.stub {
            cursor: default;
        }

        .admin-item.stub:hover {
            background-color: transparent;
        }

        .admin-count {
            font-size: 20px;
            font-weight: 600;
            min-width: 32px;
        }

        .admin-label {
            font-size: 14px;
            color: rgba(0, 0, 0, 0.7);
        }

        /* Info Box */
        .info-box {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 16px;
            background-color: #e3f2fd;
            border-radius: 8px;
            color: #1565c0;
        }

        .info-box mat-icon {
            color: #1976d2;
            flex-shrink: 0;
        }

        .info-box span {
            font-size: 14px;
        }
    `],
})
export class HomeComponent implements OnInit {
    private readonly authService = inject(AuthService);
    private readonly orderApi = inject(OrderApiService);
    private readonly roundApi = inject(SamplingRoundApiService);
    private readonly samplingLocationApi = inject(SamplingLocationApiService);
    private readonly router = inject(Router);

    readonly ordersToFinalizeCount = signal(0);
    readonly plannedRoundsCount = signal(0);
    readonly nonConformitiesCount = signal(0);
    readonly upcomingRounds = signal<SamplingRoundListDto[]>([]);
    readonly ldpToValidateCount = signal(0);

    readonly isAdmin = computed(() =>
        this.authService.currentUser()?.roles.includes('Administrator') ?? false
    );

    private readonly roundStatusColors: Record<string, string> = {
        [SamplingRoundStatus.Draft]: '#455A64',
        [SamplingRoundStatus.Assigned]: '#00695C',
        [SamplingRoundStatus.InProgress]: '#1565C0',
        [SamplingRoundStatus.Completed]: '#2E7D32',
        [SamplingRoundStatus.Cancelled]: '#C62828',
    };

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

    openPlannedRounds(): void {
        this.router.navigate(['/sampling-rounds'], {
            queryParams: { status: SamplingRoundStatus.Assigned },
        });
    }

    getRoundStatusColor(status: SamplingRoundStatus): string {
        return this.roundStatusColors[status] ?? '#9E9E9E';
    }

    getRoundStatusClass(status: SamplingRoundStatus): string {
        const map: Record<string, string> = {
            'Draft': 'status-round-draft',
            'Assigned': 'status-round-assigned',
            'InProgress': 'status-round-inprogress',
            'Completed': 'status-round-completed',
            'Cancelled': 'status-round-cancelled',
        };
        return map[status] ?? 'status-round-draft';
    }

    getRoundStatusLabel(status: SamplingRoundStatus): string {
        return SamplingRoundStatusLabels[status] ?? '';
    }

    private async loadDashboardData(): Promise<void> {
        const tasks: Promise<void>[] = [
            this.loadOrdersToFinalize(),
            this.loadUpcomingRounds(),
        ];
        if (this.isAdmin()) {
            tasks.push(this.loadLdpToValidate());
        }
        await Promise.all(tasks);
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
            const result = await firstValueFrom(
                this.roundApi.getFiltered({
                    statuses: [
                        SamplingRoundStatus.Draft,
                        SamplingRoundStatus.Assigned,
                        SamplingRoundStatus.InProgress,
                    ],
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
