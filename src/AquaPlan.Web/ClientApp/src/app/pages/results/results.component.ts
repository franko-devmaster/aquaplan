import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { OrderApiService } from '../../services/order-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SamplingRoundDetailDto, SamplingRoundListDto } from '../../models/sampling-round.model';
import { OrderListDto, OrderStatus, OrderStatusLabels } from '../../models/order.model';
import { SamplingLocationDto } from '../../models/sampling-location.model';

@Component({
  selector: 'app-results',
  standalone: true,
  imports: [
    MatTabsModule, MatTableModule, MatCardModule, MatIconModule, MatButtonModule,
    MatSelectModule, MatFormFieldModule, MatChipsModule, MatProgressSpinnerModule,
    DatePipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2>{{ 'results.title' | translate }}</h2>

    <mat-tab-group animationDuration="200ms" (selectedTabChange)="onTabChange($event.index)">
      <!-- Tab 1: Par tournée -->
      <mat-tab [label]="'results.byRound' | translate">
        <div class="tab-content">
          <div class="filter-row">
            <mat-form-field appearance="outline" class="round-select">
              <mat-label>{{ 'results.selectRound' | translate }}</mat-label>
              <mat-select (selectionChange)="onRoundSelected($event.value)">
                @for (round of rounds(); track round.id) {
                  <mat-option [value]="round.id">
                    {{ round.name }} — {{ round.deadline | date:'dd.MM.yyyy' }}
                    ({{ round.orderCount }} {{ 'results.orders' | translate }})
                  </mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          @if (loadingRoundOrders()) {
            <div class="loading-container">
              <mat-spinner diameter="40"></mat-spinner>
            </div>
          } @else if (selectedRound()) {
            <mat-card class="results-card">
              <mat-card-header>
                <mat-card-title>{{ selectedRound()!.name }}</mat-card-title>
                <mat-card-subtitle>
                  {{ selectedRound()!.orders.length }} {{ 'results.orders' | translate }}
                  — {{ selectedRound()!.distributorName }}
                </mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                @if (roundOrders().length === 0) {
                  <p class="empty-message">{{ 'results.noOrdersInRound' | translate }}</p>
                } @else {
                  <table mat-table [dataSource]="roundOrders()" class="results-table">
                    <ng-container matColumnDef="orderNumber">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.orderNumber' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.orderNumber }}</td>
                    </ng-container>
                    <ng-container matColumnDef="samplingLocation">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.samplingLocation' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.samplingLocationName ?? '—' }}</td>
                    </ng-container>
                    <ng-container matColumnDef="status">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.status' | translate }}</th>
                      <td mat-cell *matCellDef="let row">
                        <span class="status-chip" [class]="'status-' + row.status">
                          {{ getStatusLabel(row.status) | translate }}
                        </span>
                      </td>
                    </ng-container>
                    <ng-container matColumnDef="plannedDate">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.date' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.plannedDate | date:'dd.MM.yyyy' }}</td>
                    </ng-container>
                    <ng-container matColumnDef="verdict">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.verdict' | translate }}</th>
                      <td mat-cell *matCellDef="let row">
                        @if (row.status >= 6) {
                          <span class="verdict-placeholder">
                            <mat-icon class="verdict-icon">pending</mat-icon>
                            {{ 'results.awaitingLims' | translate }}
                          </span>
                        } @else {
                          <span class="verdict-na">—</span>
                        }
                      </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="roundColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: roundColumns" class="clickable-row"
                        (click)="navigateToOrder(row.id)"></tr>
                  </table>
                }
              </mat-card-content>
            </mat-card>

            <div class="lims-info">
              <mat-icon>info</mat-icon>
              <span>{{ 'results.limsPlaceholder' | translate }}</span>
            </div>
          } @else {
            <div class="empty-state">
              <mat-icon class="empty-icon">science</mat-icon>
              <p>{{ 'results.selectRoundPrompt' | translate }}</p>
            </div>
          }
        </div>
      </mat-tab>

      <!-- Tab 2: Par LDP -->
      <mat-tab [label]="'results.byLocation' | translate">
        <div class="tab-content">
          <div class="filter-row">
            <mat-form-field appearance="outline" class="location-select">
              <mat-label>{{ 'results.selectLocation' | translate }}</mat-label>
              <mat-select (selectionChange)="onLocationSelected($event.value)">
                @for (loc of locations(); track loc.id) {
                  <mat-option [value]="loc.id">
                    {{ loc.locationCode }} — {{ loc.name }}
                    ({{ loc.distributorName }})
                  </mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          @if (loadingLocationOrders()) {
            <div class="loading-container">
              <mat-spinner diameter="40"></mat-spinner>
            </div>
          } @else if (selectedLocationId()) {
            <mat-card class="results-card">
              <mat-card-header>
                <mat-card-title>{{ selectedLocationName() }}</mat-card-title>
                <mat-card-subtitle>{{ 'results.orderHistory' | translate }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                @if (locationOrders().length === 0) {
                  <p class="empty-message">{{ 'results.noOrdersForLocation' | translate }}</p>
                } @else {
                  <table mat-table [dataSource]="locationOrders()" class="results-table">
                    <ng-container matColumnDef="orderNumber">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.orderNumber' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.orderNumber }}</td>
                    </ng-container>
                    <ng-container matColumnDef="plannedDate">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.date' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.plannedDate | date:'dd.MM.yyyy' }}</td>
                    </ng-container>
                    <ng-container matColumnDef="status">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.status' | translate }}</th>
                      <td mat-cell *matCellDef="let row">
                        <span class="status-chip" [class]="'status-' + row.status">
                          {{ getStatusLabel(row.status) | translate }}
                        </span>
                      </td>
                    </ng-container>
                    <ng-container matColumnDef="preleveur">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.preleveur' | translate }}</th>
                      <td mat-cell *matCellDef="let row">{{ row.preleveurName ?? '—' }}</td>
                    </ng-container>
                    <ng-container matColumnDef="verdict">
                      <th mat-header-cell *matHeaderCellDef>{{ 'results.verdict' | translate }}</th>
                      <td mat-cell *matCellDef="let row">
                        @if (row.status >= 6) {
                          <span class="verdict-placeholder">
                            <mat-icon class="verdict-icon">pending</mat-icon>
                            {{ 'results.awaitingLims' | translate }}
                          </span>
                        } @else {
                          <span class="verdict-na">—</span>
                        }
                      </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="locationColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: locationColumns" class="clickable-row"
                        (click)="navigateToOrder(row.id)"></tr>
                  </table>
                }
              </mat-card-content>
            </mat-card>

            <div class="lims-info">
              <mat-icon>info</mat-icon>
              <span>{{ 'results.limsPlaceholder' | translate }}</span>
            </div>
          } @else {
            <div class="empty-state">
              <mat-icon class="empty-icon">place</mat-icon>
              <p>{{ 'results.selectLocationPrompt' | translate }}</p>
            </div>
          }
        </div>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    h2 { margin-bottom: 16px; }
    .tab-content { padding: 16px 0; }
    .filter-row { margin-bottom: 16px; }
    .round-select, .location-select { width: 100%; max-width: 600px; }
    .results-card { margin-bottom: 16px; }
    .results-table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background-color: rgba(0, 0, 0, 0.04); }

    .status-chip {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 500;
    }
    .status-6 { background-color: #C8E6C9; color: #2E7D32; }
    .status-7 { background-color: #A5D6A7; color: #1B5E20; }
    .status-3, .status-4, .status-5 { background-color: #FFE0B2; color: #E65100; }

    .verdict-placeholder {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: #9E9E9E;
      font-size: 12px;
    }
    .verdict-icon { font-size: 16px; width: 16px; height: 16px; }
    .verdict-na { color: #BDBDBD; }

    .lims-info {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      background: #E3F2FD;
      border-radius: 8px;
      color: #1565C0;
      font-size: 13px;
      margin-top: 16px;
    }

    .empty-state {
      text-align: center;
      padding: 48px 16px;
      color: #9E9E9E;
    }
    .empty-icon { font-size: 48px; width: 48px; height: 48px; margin-bottom: 8px; }
    .empty-message { color: #9E9E9E; font-style: italic; padding: 16px; }

    .loading-container {
      display: flex;
      justify-content: center;
      padding: 32px;
    }
  `],
})
export class ResultsComponent implements OnInit {
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly router = inject(Router);

  readonly rounds = signal<SamplingRoundListDto[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);

  readonly selectedRound = signal<SamplingRoundDetailDto | null>(null);
  readonly roundOrders = signal<OrderListDto[]>([]);
  readonly loadingRoundOrders = signal(false);

  readonly selectedLocationId = signal<string | null>(null);
  readonly selectedLocationName = computed(() => {
    const id = this.selectedLocationId();
    if (!id) return '';
    const loc = this.locations().find(l => l.id === id);
    return loc ? `${loc.locationCode} — ${loc.name}` : '';
  });
  readonly locationOrders = signal<OrderListDto[]>([]);
  readonly loadingLocationOrders = signal(false);

  readonly roundColumns = ['orderNumber', 'samplingLocation', 'plannedDate', 'status', 'verdict'];
  readonly locationColumns = ['orderNumber', 'plannedDate', 'preleveur', 'status', 'verdict'];

  async ngOnInit(): Promise<void> {
    await this.loadRounds();
  }

  async onTabChange(index: number): Promise<void> {
    if (index === 1 && this.locations().length === 0) {
      await this.loadLocations();
    }
  }

  async loadRounds(): Promise<void> {
    const result = await firstValueFrom(this.roundApi.getFiltered({
      page: 1,
      pageSize: 100,
      sortBy: 'deadline',
      sortDescending: true,
    }));
    this.rounds.set(result.items);
  }

  async loadLocations(): Promise<void> {
    const locs = await firstValueFrom(this.locationApi.getForCurrentUser());
    this.locations.set(locs);
  }

  async onRoundSelected(roundId: string): Promise<void> {
    this.loadingRoundOrders.set(true);
    try {
      const detail = await firstValueFrom(this.roundApi.getById(roundId));
      this.selectedRound.set(detail);

      const orders = await firstValueFrom(this.orderApi.getFiltered({
        page: 1,
        pageSize: 100,
      }));
      // Filter orders belonging to this round
      const roundOrderIds = new Set(detail.orders?.map((o: { id: string }) => o.id) ?? []);
      this.roundOrders.set(orders.items.filter(o => roundOrderIds.has(o.id)));
    } finally {
      this.loadingRoundOrders.set(false);
    }
  }

  async onLocationSelected(locationId: string): Promise<void> {
    this.selectedLocationId.set(locationId);
    this.loadingLocationOrders.set(true);
    try {
      const orders = await firstValueFrom(this.orderApi.getFiltered({
        page: 1,
        pageSize: 100,
        sortDescending: true,
      }));
      // Filter orders for this location
      this.locationOrders.set(
        orders.items.filter(o => o.samplingLocationId === locationId)
      );
    } finally {
      this.loadingLocationOrders.set(false);
    }
  }

  getStatusLabel(status: OrderStatus): string {
    return OrderStatusLabels[status] ?? 'orders.status.new';
  }

  navigateToOrder(orderId: string): void {
    this.router.navigate(['/orders', orderId]);
  }
}
