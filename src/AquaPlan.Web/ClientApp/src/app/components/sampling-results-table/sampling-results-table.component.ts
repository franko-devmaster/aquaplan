import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../../services/order-api.service';
import { SamplingResultDto, SamplingResultListDto } from '../../models/order.model';
import { StatusChipComponent } from '../status-chip/status-chip.component';
import { AuthService } from '../../services/auth.service';

/**
 * AQ-400 — Tableau de consultation des résultats d'analyse d'un mandat.
 * Surligne les lignes non-conformes en rouge, affiche des badges Conforme/Non conforme,
 * et expose un bouton admin "Synchroniser maintenant" qui appelle POST /pull-results.
 */
@Component({
  selector: 'app-sampling-results-table',
  standalone: true,
  imports: [
    DecimalPipe,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    TranslateModule,
    StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="results-section">
      <header class="results-header">
        <h3>{{ 'orders.results.title' | translate }}</h3>
        @if (hasResults()) {
          <span class="results-counter">
            {{ 'orders.results.counter' | translate: {
              total: results()!.totalCount,
              nonConform: results()!.nonConformCount
            } }}
          </span>
        }
        @if (isAdmin()) {
          <button mat-stroked-button
                  type="button"
                  (click)="pullNow()"
                  [disabled]="loading() || pulling()">
            <mat-icon>sync</mat-icon>
            {{ 'orders.results.pullNow' | translate }}
          </button>
        }
      </header>

      @if (loading()) {
        <div class="results-placeholder">
          <mat-spinner diameter="24"></mat-spinner>
        </div>
      } @else if (!hasResults()) {
        <div class="results-placeholder empty">
          <mat-icon>hourglass_empty</mat-icon>
          <p>{{ 'orders.results.empty' | translate }}</p>
        </div>
      } @else {
        <div class="results-table-wrapper">
          <table class="results-table">
            <thead>
              <tr>
                <th>{{ 'orders.results.columns.parameter' | translate }}</th>
                <th class="numeric">{{ 'orders.results.columns.value' | translate }}</th>
                <th>{{ 'orders.results.columns.unit' | translate }}</th>
                <th>{{ 'orders.results.columns.range' | translate }}</th>
                <th>{{ 'orders.results.columns.conformity' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of results()!.items; track row.id) {
                <tr [class.non-conform]="!row.isConform">
                  <td class="parameter-code">{{ row.parameterCode }}</td>
                  <td class="numeric">{{ row.value | number: '1.0-3' }}</td>
                  <td>{{ row.unit }}</td>
                  <td>{{ formatRange(row) }}</td>
                  <td>
                    @if (row.isConform) {
                      <app-status-chip variant="success"
                                       [label]="('orders.results.conform' | translate)">
                      </app-status-chip>
                    } @else {
                      <app-status-chip variant="danger"
                                       [label]="('orders.results.nonConform' | translate)">
                      </app-status-chip>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: [`
    .results-section {
      margin-top: 16px;
      padding: 16px;
      background: #fff;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
    }
    .results-header {
      display: flex;
      align-items: center;
      gap: 16px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .results-header h3 {
      margin: 0;
      font-size: 16px;
      text-transform: uppercase;
      letter-spacing: 0.4px;
      color: #424242;
      flex-shrink: 0;
    }
    .results-counter {
      font-size: 13px;
      color: #616161;
      flex-grow: 1;
    }
    .results-placeholder {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 32px 16px;
      color: #757575;
    }
    .results-placeholder.empty mat-icon {
      font-size: 36px;
      width: 36px;
      height: 36px;
      color: #9e9e9e;
    }
    .results-placeholder p {
      margin: 0;
      font-size: 14px;
    }
    .results-table-wrapper {
      overflow-x: auto;
    }
    .results-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
    }
    .results-table th {
      text-align: left;
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.3px;
      color: #616161;
      padding: 8px 12px;
      border-bottom: 1px solid #e0e0e0;
    }
    .results-table td {
      padding: 10px 12px;
      border-bottom: 1px solid #f0f0f0;
    }
    .results-table tr.non-conform td {
      background: #FFEBEE;
    }
    .results-table tr.non-conform td.parameter-code {
      font-weight: 600;
    }
    .results-table .numeric {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
  `],
})
export class SamplingResultsTableComponent {
  private readonly orderApi = inject(OrderApiService);
  private readonly authService = inject(AuthService);

  readonly orderId = input.required<string>();
  /** Optional list passed by the parent when it already has the data; disables the auto-fetch. */
  readonly initialResults = input<SamplingResultListDto | null>(null);

  readonly results = signal<SamplingResultListDto | null>(null);
  readonly loading = signal(false);
  readonly pulling = signal(false);

  readonly hasResults = computed(() => (this.results()?.totalCount ?? 0) > 0);

  constructor() {
    effect(() => {
      const provided = this.initialResults();
      if (provided) {
        this.results.set(provided);
        return;
      }
      const id = this.orderId();
      if (id) {
        void this.loadResults(id);
      }
    });
  }

  isAdmin(): boolean {
    return this.authService.currentUser()?.roles.includes('Administrator') ?? false;
  }

  formatRange(row: SamplingResultDto): string {
    const min = row.referenceMin;
    const max = row.referenceMax;
    if (min === null && max === null) {
      return '—';
    }
    if (min !== null && max !== null) {
      return `${min} – ${max}`;
    }
    if (max !== null) {
      return `≤ ${max}`;
    }
    return `≥ ${min}`;
  }

  async pullNow(): Promise<void> {
    const id = this.orderId();
    if (!id || this.pulling()) {
      return;
    }
    this.pulling.set(true);
    try {
      const data = await firstValueFrom(this.orderApi.pullResults(id));
      this.results.set(data);
    } finally {
      this.pulling.set(false);
    }
  }

  private async loadResults(orderId: string): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.orderApi.getResults(orderId));
      this.results.set(data);
    } catch {
      this.results.set({ items: [], conformCount: 0, nonConformCount: 0, totalCount: 0 });
    } finally {
      this.loading.set(false);
    }
  }
}
