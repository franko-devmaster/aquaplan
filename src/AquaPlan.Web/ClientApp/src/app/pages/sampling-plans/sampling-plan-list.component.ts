import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingPlanDatastore } from '../../datastore/sampling-plan.datastore';
import { AuthService } from '../../services/auth.service';
import { DistributorApiService } from '../../services/distributor-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import { SamplingPlanListDto, SamplingPlanStatus, SamplingPlanStatusLabels } from '../../models/sampling-plan.model';
import { SamplingPlanCreateDialogComponent } from './sampling-plan-create-dialog.component';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';
import { planStatusVariant } from '../../utils/status-variant';
import { debouncedSearch } from '../../utils/debounced-search';

@Component({
  selector: 'app-sampling-plan-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatPaginatorModule, MatSortModule,
    DatePipe, TranslateModule, StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'samplingPlans.title' | translate }}</h2>
      @if (canCreate()) {
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon>
          {{ 'samplingPlans.createPlan' | translate }}
        </button>
      }
    </div>

    <div class="filters-row">
      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingPlans.year' | translate }}</mat-label>
        <mat-select [ngModel]="store.yearFilter()" (ngModelChange)="onYearChange($event)">
          <mat-option [value]="undefined">{{ 'common.all' | translate }}</mat-option>
          @for (y of availableYears; track y) {
            <mat-option [value]="y">{{ y }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingPlans.status.label' | translate }}</mat-label>
        <mat-select multiple [ngModel]="store.statusFilter()" (ngModelChange)="store.setStatusFilter($event)">
          @for (status of availableStatuses; track status.value) {
            <mat-option [value]="status.value">{{ status.label | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (isAdmin()) {
        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>{{ 'samplingPlans.distributor' | translate }}</mat-label>
          <mat-select [ngModel]="store.distributorFilter()" (ngModelChange)="onDistributorChange($event)">
            <mat-option [value]="undefined">{{ 'common.all' | translate }}</mat-option>
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [ngModel]="searchValue()" (ngModelChange)="onSearchChange($event)"
               [placeholder]="'samplingPlans.searchPlaceholder' | translate">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.plans()" matSort (matSortChange)="onSortChange($event)"
               class="full-width">
          <ng-container matColumnDef="year">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="year">{{ 'samplingPlans.year' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'samplingPlans.year' | translate">{{ plan.year }}</td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="distributor">{{ 'samplingPlans.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'samplingPlans.distributor' | translate">{{ plan.distributorName }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="status">{{ 'samplingPlans.status.label' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'samplingPlans.status.label' | translate">
              <app-status-chip [variant]="getStatusVariant(plan.status)"
                               [label]="(getStatusLabel(plan) | translate)"></app-status-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="itemCount">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingPlans.itemCount' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'samplingPlans.itemCount' | translate">{{ plan.itemCount }}</td>
          </ng-container>

          <ng-container matColumnDef="createdBy">
            <th mat-header-cell *matHeaderCellDef>{{ 'common.createdBy' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'common.createdBy' | translate">{{ plan.createdByName ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef>{{ 'common.createdAt' | translate }}</th>
            <td mat-cell *matCellDef="let plan" [attr.data-label]="'common.createdAt' | translate">{{ plan.createdAt | date:'shortDate' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" (click)="viewDetail(row)"></tr>
        </table>
      </div>

      @if (store.plans().length === 0) {
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
  `],
})
export class SamplingPlanListComponent implements OnInit {
  readonly store = inject(SamplingPlanDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly distributorApi = inject(DistributorApiService);

  private readonly destroyRef = inject(DestroyRef);

  // F-012 — centralised role check.
  readonly isAdmin = this.authService.isAdmin;
  readonly canCreate = this.authService.canCreateOrders;
  readonly distributors = signal<DistributorListDto[]>([]);
  readonly searchValue = signal('');
  // F-015 — debounced search cleaned up on destroy.
  private readonly debouncedSearch = debouncedSearch<string>(
    value => this.store.setSearch(value),
    this.destroyRef
  );

  readonly displayedColumns = ['year', 'distributor', 'status', 'itemCount', 'createdBy', 'createdAt'];

  readonly currentYear = new Date().getFullYear();
  readonly availableYears = [this.currentYear - 1, this.currentYear, this.currentYear + 1, this.currentYear + 2];

  readonly availableStatuses = [
    { value: SamplingPlanStatus.Draft, label: SamplingPlanStatusLabels[SamplingPlanStatus.Draft] },
    { value: SamplingPlanStatus.Submitted, label: SamplingPlanStatusLabels[SamplingPlanStatus.Submitted] },
    { value: SamplingPlanStatus.Validated, label: SamplingPlanStatusLabels[SamplingPlanStatus.Validated] },
    { value: SamplingPlanStatus.Rejected, label: SamplingPlanStatusLabels[SamplingPlanStatus.Rejected] },
  ];

  async ngOnInit(): Promise<void> {
    this.store.loadFiltered();
    if (this.isAdmin()) {
      const dists = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
      this.distributors.set(dists);
    }
  }

  getStatusVariant(status: SamplingPlanStatus): StatusChipVariant {
    return planStatusVariant(status);
  }

  getStatusLabel(plan: SamplingPlanListDto): string {
    return SamplingPlanStatusLabels[plan.status] ?? 'samplingPlans.status.draft';
  }

  onSearchChange(value: string): void {
    this.searchValue.set(value);
    this.debouncedSearch(value);
  }

  onYearChange(year: number | undefined): void {
    this.store.setYearFilter(year);
  }

  onDistributorChange(distributorId: string | undefined): void {
    this.store.distributorFilter.set(distributorId || undefined);
    this.store.currentPage.set(1);
    this.store.loadFiltered();
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

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(SamplingPlanCreateDialogComponent, { width: '450px', panelClass: 'responsive-dialog' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.router.navigate(['/sampling-plans', result.id]);
      }
    });
  }

  viewDetail(plan: SamplingPlanListDto): void {
    this.router.navigate(['/sampling-plans', plan.id]);
  }
}
