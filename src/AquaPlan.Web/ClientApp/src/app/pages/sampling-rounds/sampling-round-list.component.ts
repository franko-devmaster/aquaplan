import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { DatePipe, NgClass } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { SamplingRoundDatastore } from '../../datastore/sampling-round.datastore';
import { DistributorApiService } from '../../services/distributor-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import {
  SamplingRoundListDto,
  SamplingRoundDetailDto,
  SamplingRoundStatus,
  SamplingRoundStatusLabels,
} from '../../models/sampling-round.model';
import { SamplingRoundCreateDialogComponent } from './sampling-round-create-dialog.component';

@Component({
  selector: 'app-sampling-round-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatPaginatorModule, MatSortModule, MatDialogModule,
    DatePipe, NgClass, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'samplingRounds.title' | translate }}</h2>
      <button mat-raised-button color="primary" (click)="createRound()">
        <mat-icon>add</mat-icon>
        {{ 'samplingRounds.createRound' | translate }}
      </button>
    </div>

    <div class="filters-row">
      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingRounds.status.label' | translate }}</mat-label>
        <mat-select multiple [ngModel]="store.statusFilter()" (ngModelChange)="store.setStatusFilter($event)">
          @for (status of availableStatuses; track status.value) {
            <mat-option [value]="status.value">{{ status.label | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingRounds.distributor' | translate }}</mat-label>
        <mat-select [ngModel]="store.distributorFilter()" (ngModelChange)="store.setDistributorFilter($event || undefined)">
          <mat-option [value]="''">{{ 'common.all' | translate }}</mat-option>
          @for (dist of distributors(); track dist.id) {
            <mat-option [value]="dist.id">{{ dist.shortName ?? dist.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [ngModel]="searchValue()" (ngModelChange)="onSearchChange($event)"
               [placeholder]="'samplingRounds.searchPlaceholder' | translate">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.rounds()" matSort (matSortChange)="onSortChange($event)"
               class="full-width">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="name">{{ 'samplingRounds.name' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.name' | translate">{{ round.name }}</td>
          </ng-container>

          <ng-container matColumnDef="deadline">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="deadline">{{ 'samplingRounds.deadline' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.deadline' | translate">{{ round.deadline | date:'shortDate' }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="status">{{ 'samplingRounds.status.label' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.status.label' | translate">
              <span class="status-badge-round" [ngClass]="getStatusClass(round.status)">
                {{ getStatusLabel(round) | translate }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="sampler">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.sampler' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.sampler' | translate">{{ round.samplerName ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="distributor">{{ 'samplingRounds.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.distributor' | translate" [matTooltip]="round.distributorName">{{ round.distributorShortName ?? round.distributorName }}</td>
          </ng-container>

          <ng-container matColumnDef="orders">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.orderCount' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.orderCount' | translate">{{ round.orderCount }}</td>
          </ng-container>

          <ng-container matColumnDef="progress">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.progress' | translate }}</th>
            <td mat-cell *matCellDef="let round" [attr.data-label]="'samplingRounds.progress' | translate">
              {{ round.completedOrderCount }}/{{ round.orderCount }}
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" (click)="viewDetail(row)"></tr>
        </table>
      </div>

      @if (store.rounds().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }

      <mat-paginator [length]="store.totalCount()"
                     [pageSize]="store.pageSize()"
                     [pageIndex]="store.currentPage() - 1"
                     [pageSizeOptions]="[10, 20, 50]"
                     (page)="onPageChange($event)"
                     showFirstLastButtons>
      </mat-paginator>
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .filters-row { display: flex; gap: 16px; align-items: center; margin-bottom: 16px; flex-wrap: wrap; }
    .filter-field { min-width: 200px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background-color: rgba(0, 0, 0, 0.04); }
  `],
})
export class SamplingRoundListComponent implements OnInit {
  readonly store = inject(SamplingRoundDatastore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly distributorApi = inject(DistributorApiService);

  readonly distributors = signal<DistributorListDto[]>([]);
  readonly searchValue = signal('');
  private searchTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly displayedColumns = ['name', 'deadline', 'status', 'sampler', 'distributor', 'orders', 'progress'];

  readonly availableStatuses = [
    { value: SamplingRoundStatus.Draft, label: SamplingRoundStatusLabels[SamplingRoundStatus.Draft] },
    { value: SamplingRoundStatus.Assigned, label: SamplingRoundStatusLabels[SamplingRoundStatus.Assigned] },
    { value: SamplingRoundStatus.InProgress, label: SamplingRoundStatusLabels[SamplingRoundStatus.InProgress] },
    { value: SamplingRoundStatus.Completed, label: SamplingRoundStatusLabels[SamplingRoundStatus.Completed] },
    { value: SamplingRoundStatus.Cancelled, label: SamplingRoundStatusLabels[SamplingRoundStatus.Cancelled] },
  ];

  async ngOnInit(): Promise<void> {
    this.applyStatusFromQueryParams();
    const allDistributors = await firstValueFrom(this.distributorApi.getAll());
    this.distributors.set(allDistributors);
    this.store.loadFiltered();
  }

  private applyStatusFromQueryParams(): void {
    const raw = this.route.snapshot.queryParamMap.get('status');
    if (!raw) {
      return;
    }
    const validValues = new Set<string>(Object.values(SamplingRoundStatus));
    const parsed = raw
      .split(',')
      .map((s) => s.trim())
      .filter((s) => validValues.has(s)) as SamplingRoundStatus[];
    if (parsed.length > 0) {
      this.store.statusFilter.set(parsed);
      this.store.currentPage.set(1);
    }
  }

  getStatusLabel(round: SamplingRoundListDto): string {
    return SamplingRoundStatusLabels[round.status] ?? 'samplingRounds.status.draft';
  }

  getStatusClass(status: SamplingRoundStatus): string {
    const map: Record<string, string> = {
      'Draft': 'status-round-draft',
      'Assigned': 'status-round-assigned',
      'InProgress': 'status-round-inprogress',
      'Completed': 'status-round-completed',
      'Cancelled': 'status-round-cancelled',
    };
    return map[status] ?? 'status-round-draft';
  }

  onSearchChange(value: string): void {
    this.searchValue.set(value);
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    this.searchTimeout = setTimeout(() => {
      this.store.setSearch(value);
    }, 300);
  }

  onSortChange(sort: Sort): void {
    if (sort.direction) {
      this.store.sortBy.set(sort.active);
      this.store.sortDescending.set(sort.direction === 'desc');
      this.store.loadFiltered();
    } else {
      this.store.sortBy.set(undefined);
      this.store.loadFiltered();
    }
  }

  onPageChange(event: PageEvent): void {
    if (event.pageSize !== this.store.pageSize()) {
      this.store.pageSize.set(event.pageSize);
      this.store.currentPage.set(1);
    } else {
      this.store.currentPage.set(event.pageIndex + 1);
    }
    this.store.loadFiltered();
  }

  createRound(): void {
    const dialogRef = this.dialog.open(SamplingRoundCreateDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
    });
    dialogRef.afterClosed().subscribe((result: SamplingRoundDetailDto | undefined) => {
      if (result) {
        this.router.navigate(['/sampling-rounds', result.id]);
      }
    });
  }

  viewDetail(round: SamplingRoundListDto): void {
    this.router.navigate(['/sampling-rounds', round.id]);
  }
}
