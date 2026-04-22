import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ResultsApiService } from '../../services/results-api.service';
import { DistributorApiService } from '../../services/distributor-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import { SectorListDto } from '../../models/sector.model';
import {
  ResultConformity,
  ResultsCellDto,
  ResultsMatrixDto,
  ResultsMatrixLocationDto,
} from '../../models/results.model';
import {
  SamplingResultsDialogComponent,
  SamplingResultsDialogData,
} from './sampling-results-dialog.component';

/**
 * AQ-415 — LDP × dates matrix with filters and shared results dialog on cell click.
 */
@Component({
  selector: 'app-results-matrix',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="matrix-section">
      <header class="matrix-header">
        <div class="filters">
          <mat-form-field appearance="outline" class="filter-field">
            <mat-label>{{ 'results.filters.distributor' | translate }}</mat-label>
            <mat-select [(ngModel)]="distributorId" (selectionChange)="reload()">
              <mat-option [value]="null">{{ 'results.filters.all' | translate }}</mat-option>
              @for (d of distributors(); track d.id) {
                <mat-option [value]="d.id">{{ d.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="filter-field">
            <mat-label>{{ 'results.filters.sector' | translate }}</mat-label>
            <mat-select [(ngModel)]="sectorId" (selectionChange)="reload()">
              <mat-option [value]="null">{{ 'results.filters.all' | translate }}</mat-option>
              @for (s of sectors(); track s.id) {
                <mat-option [value]="s.id">{{ s.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-checkbox class="filter-field anomalies-check" [(ngModel)]="anomaliesOnly" (change)="reload()">
            {{ 'results.filters.anomaliesOnly' | translate }}
          </mat-checkbox>
        </div>
      </header>

      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="28"></mat-spinner>
        </div>
      } @else if (isEmpty()) {
        <div class="empty">
          <mat-icon>table_view</mat-icon>
          <p>{{ 'results.matrix.noData' | translate }}</p>
        </div>
      } @else {
        <div class="matrix-wrapper">
          <table class="matrix-table">
            <thead>
              <tr>
                <th class="sticky-col header-cell">
                  {{ 'results.matrix.location' | translate }}
                </th>
                @for (d of matrix().dates; track d) {
                  <th class="header-cell">{{ d | date: 'dd.MM.yy' }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (loc of matrix().locations; track loc.id) {
                <tr>
                  <td class="sticky-col location-cell" [matTooltip]="loc.sectorName">
                    <div class="loc-code">{{ loc.code }}</div>
                    <div class="loc-name">{{ loc.name }}</div>
                  </td>
                  @for (date of matrix().dates; track date) {
                    @let cell = cellFor(loc.id, date);
                    @if (cell) {
                      <td class="cell"
                          [class]="'conformity-' + cell.conformity.toLowerCase()"
                          [matTooltip]="cellTooltip(cell) | translate"
                          (click)="openDialog(cell, loc)">
                        <span class="cell-dot"></span>
                      </td>
                    } @else {
                      <td class="cell empty-cell">&nbsp;</td>
                    }
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styles: [`
    .matrix-section {
      padding: 16px;
      background: #fff;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
    }
    .matrix-header {
      margin-bottom: 12px;
    }
    .filters {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .filter-field {
      min-width: 220px;
    }
    .anomalies-check {
      align-self: center;
      padding-bottom: 20px;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }
    .empty {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 20px;
      color: #9e9e9e;
    }
    .empty p { margin: 0; }
    .matrix-wrapper {
      overflow: auto;
      max-height: calc(100vh - 380px);
    }
    .matrix-table {
      border-collapse: separate;
      border-spacing: 0;
      font-size: 12px;
      min-width: 100%;
    }
    .matrix-table th, .matrix-table td {
      padding: 6px 8px;
      border-bottom: 1px solid #f0f0f0;
      border-right: 1px solid #f0f0f0;
      white-space: nowrap;
    }
    .header-cell {
      position: sticky;
      top: 0;
      background: #fafafa;
      z-index: 2;
      font-weight: 600;
      color: #424242;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }
    .sticky-col {
      position: sticky;
      left: 0;
      background: #fff;
      z-index: 1;
      min-width: 220px;
      text-align: left;
    }
    thead .sticky-col {
      z-index: 3;
      background: #fafafa;
    }
    .location-cell .loc-code {
      font-weight: 600;
      color: #212121;
    }
    .location-cell .loc-name {
      color: #616161;
      font-size: 11px;
    }
    .cell {
      text-align: center;
      cursor: pointer;
      width: 42px;
    }
    .cell.empty-cell { cursor: default; }
    .cell-dot {
      display: inline-block;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      background: #ccc;
    }
    .cell.conformity-green { background: #E8F5E9; }
    .cell.conformity-green .cell-dot { background: var(--conformity-green, #4caf50); }
    .cell.conformity-yellow { background: #FFF3E0; }
    .cell.conformity-yellow .cell-dot { background: var(--conformity-yellow, #ff9800); }
    .cell.conformity-red { background: #FFEBEE; }
    .cell.conformity-red .cell-dot { background: var(--conformity-red, #f44336); }
    .cell.conformity-pending { background: #ECEFF1; }
    .cell.conformity-pending .cell-dot { background: var(--conformity-pending, #9e9e9e); }
    .cell:hover:not(.empty-cell) { filter: brightness(0.96); }
  `],
})
export class ResultsMatrixComponent implements OnInit {
  private readonly api = inject(ResultsApiService);
  private readonly distributorApi = inject(DistributorApiService);
  private readonly sectorApi = inject(SectorApiService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(false);
  readonly matrix = signal<ResultsMatrixDto>({ locations: [], dates: [], cells: [] });
  readonly distributors = signal<DistributorListDto[]>([]);
  readonly sectors = signal<SectorListDto[]>([]);

  distributorId: string | null = null;
  sectorId: string | null = null;
  anomaliesOnly = false;

  readonly isEmpty = computed(() => this.matrix().locations.length === 0);

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadDistributors(), this.loadSectors(), this.reload()]);
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(
        this.api.getMatrix({
          distributorId: this.distributorId ?? undefined,
          sectorId: this.sectorId ?? undefined,
          anomaliesOnly: this.anomaliesOnly,
        }),
      );
      this.matrix.set(data ?? { locations: [], dates: [], cells: [] });
    } catch {
      this.matrix.set({ locations: [], dates: [], cells: [] });
    } finally {
      this.loading.set(false);
    }
  }

  cellFor(locationId: string, date: string): ResultsCellDto | undefined {
    return this.matrix().cells.find(
      c => c.locationId === locationId && c.date === date,
    );
  }

  cellTooltip(cell: ResultsCellDto): string {
    switch (cell.conformity) {
      case 'Green':
        return 'results.conformity.conform';
      case 'Yellow':
        return 'results.conformity.minorAnomaly';
      case 'Red':
        return 'results.conformity.criticalAnomaly';
      default:
        return 'results.conformity.pending';
    }
  }

  openDialog(cell: ResultsCellDto, loc: ResultsMatrixLocationDto): void {
    this.dialog.open<SamplingResultsDialogComponent, SamplingResultsDialogData>(
      SamplingResultsDialogComponent,
      {
        data: {
          orderId: cell.orderId,
          orderNumber: cell.orderNumber,
          locationName: `${loc.code} — ${loc.name}`,
        },
        width: '900px',
        maxWidth: '95vw',
        autoFocus: false,
      },
    );
  }

  // Expose for completeness; not used in template.
  conformityBadge(c: ResultConformity): string {
    return `pill-${c.toLowerCase()}`;
  }

  private async loadDistributors(): Promise<void> {
    try {
      const data = await firstValueFrom(this.distributorApi.getAll());
      this.distributors.set(data ?? []);
    } catch {
      this.distributors.set([]);
    }
  }

  private async loadSectors(): Promise<void> {
    try {
      const data = await firstValueFrom(this.sectorApi.getAll());
      this.sectors.set(data ?? []);
    } catch {
      this.sectors.set([]);
    }
  }
}
