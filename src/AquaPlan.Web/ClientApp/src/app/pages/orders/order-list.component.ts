import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderDatastore } from '../../datastore/order.datastore';
import { OrderApiService } from '../../services/order-api.service';
import { AuthService } from '../../services/auth.service';
import { NetworkCheckService } from '../../services/network-check.service';
import { OrderListDto, OrderStatus, OrderStatusLabels } from '../../models/order.model';
import { DistributorApiService } from '../../services/distributor-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import { OrderCreateDialogComponent } from './order-create-dialog.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog.component';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule,
    MatDatepickerModule, MatPaginatorModule, MatSortModule,
    DatePipe, TranslateModule, StatusChipComponent,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'orders.title' | translate }}</h2>
      <div class="header-actions">
        @if (canBulkActions()) {
          <button mat-stroked-button
                  [disabled]="inProgressCount() === 0 || bulkLoading()"
                  [matTooltip]="'orders.bulkValidateTooltip' | translate"
                  (click)="confirmBulkValidate()">
            <mat-icon>done_all</mat-icon>
            {{ 'orders.bulkValidate' | translate }}
          </button>
          <button mat-stroked-button
                  [disabled]="completedCount() === 0 || bulkLoading()"
                  [matTooltip]="'orders.bulkTransmitTooltip' | translate"
                  (click)="confirmBulkTransmit()">
            <mat-icon>send</mat-icon>
            {{ 'orders.bulkTransmit' | translate }}
          </button>
        }
        @if (isAdmin()) {
          <button mat-stroked-button (click)="exportCsv()">
            <mat-icon>download</mat-icon>
            {{ 'orders.export' | translate }}
          </button>
        }
        @if (canCreate()) {
          <button mat-raised-button color="primary" (click)="openCreateDialog()">
            <mat-icon>add</mat-icon>
            {{ 'orders.createOrder' | translate }}
          </button>
        }
      </div>
    </div>

    <div class="filters-row">
      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [ngModel]="searchValue()" (ngModelChange)="onSearchChange($event)"
               [placeholder]="'orders.searchPlaceholder' | translate">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'orders.status.label' | translate }}</mat-label>
        <mat-select multiple [ngModel]="store.statusFilter()" (ngModelChange)="store.setStatusFilter($event)">
          @for (status of availableStatuses; track status.value) {
            <mat-option [value]="status.value">{{ status.label | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-checkbox [ngModel]="store.isUnassignedFilter() === true"
                    (ngModelChange)="onUnassignedChange($event)">
        {{ 'orders.unassignedOnly' | translate }}
      </mat-checkbox>
    </div>

    @if (isAdmin()) {
      <div class="filters-row">
        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>{{ 'orders.distributor' | translate }}</mat-label>
          <mat-select [ngModel]="store.distributorFilter()" (ngModelChange)="onDistributorChange($event)">
            <mat-option [value]="undefined">{{ 'common.all' | translate }}</mat-option>
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>{{ 'orders.dateFrom' | translate }}</mat-label>
          <input matInput [matDatepicker]="pickerFrom" [ngModel]="store.dateFromFilter()" (dateChange)="onDateFromChange($event.value)">
          <mat-datepicker-toggle matIconSuffix [for]="pickerFrom"></mat-datepicker-toggle>
          <mat-datepicker #pickerFrom></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline" class="filter-field">
          <mat-label>{{ 'orders.dateTo' | translate }}</mat-label>
          <input matInput [matDatepicker]="pickerTo" [ngModel]="store.dateToFilter()" (dateChange)="onDateToChange($event.value)">
          <mat-datepicker-toggle matIconSuffix [for]="pickerTo"></mat-datepicker-toggle>
          <mat-datepicker #pickerTo></mat-datepicker>
        </mat-form-field>
      </div>
    }

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.orders()" matSort (matSortChange)="onSortChange($event)"
               class="full-width">
          <ng-container matColumnDef="orderNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="ordernumber">{{ 'orders.orderNumber' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.orderNumber' | translate">{{ order.orderNumber }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="status">{{ 'orders.status.label' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.status.label' | translate">
              <app-status-chip [variant]="getStatusVariant(order.status)"
                               [label]="(getStatusLabel(order) | translate)"></app-status-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="distributor">{{ 'orders.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.distributor' | translate">
              {{ order.distributorName }}
              @if (order.isDelegated) {
                <mat-icon class="delegation-badge" matTooltip="{{ 'orders.delegated' | translate }}">swap_horiz</mat-icon>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="samplingLocation">
            <th mat-header-cell *matHeaderCellDef>{{ 'orders.samplingLocation' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.samplingLocation' | translate">{{ order.samplingLocationName ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="preleveur">
            <th mat-header-cell *matHeaderCellDef>{{ 'orders.preleveur' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.preleveur' | translate">{{ order.preleveurName ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="plannedDate">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="planneddate">{{ 'orders.plannedDate' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.plannedDate' | translate">{{ order.plannedDate ? (order.plannedDate | date:'shortDate') : '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef>{{ 'orders.createdAt' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.createdAt' | translate">{{ order.createdAt | date:'shortDate' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" (click)="viewDetail(row)"></tr>
        </table>
      </div>

      @if (store.orders().length === 0) {
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
    .header-actions { display: flex; gap: 8px; }
    .filters-row { display: flex; gap: 16px; align-items: center; margin-bottom: 16px; flex-wrap: wrap; }
    .filter-field { min-width: 200px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .delegation-badge { font-size: 16px; width: 16px; height: 16px; vertical-align: middle; margin-left: 4px; color: #1976d2; }
  `],
})
export class OrderListComponent implements OnInit {
  readonly store = inject(OrderDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly distributorApi = inject(DistributorApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly networkCheck = inject(NetworkCheckService);

  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );
  readonly isRequerant = computed(() => {
    const roles = this.authService.currentUser()?.roles ?? [];
    return roles.includes('Requérant') || roles.includes('Requérant-Préleveur');
  });
  readonly canBulkActions = computed(() => this.isAdmin() || this.isRequerant());
  readonly canCreate = this.authService.canCreateOrders;
  readonly distributors = signal<DistributorListDto[]>([]);
  readonly searchValue = signal('');
  readonly inProgressCount = signal(0);
  readonly completedCount = signal(0);
  readonly bulkLoading = signal(false);
  private searchTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly displayedColumns = [
    'orderNumber', 'status', 'distributor', 'samplingLocation',
    'preleveur', 'plannedDate', 'createdAt',
  ];

  readonly availableStatuses = [
    { value: OrderStatus.New, label: OrderStatusLabels[OrderStatus.New] },
    { value: OrderStatus.InProgress, label: OrderStatusLabels[OrderStatus.InProgress] },
    { value: OrderStatus.Completed, label: OrderStatusLabels[OrderStatus.Completed] },
    { value: OrderStatus.Transmitted, label: OrderStatusLabels[OrderStatus.Transmitted] },
    { value: OrderStatus.Done, label: OrderStatusLabels[OrderStatus.Done] },
    { value: OrderStatus.Cancelled, label: OrderStatusLabels[OrderStatus.Cancelled] },
  ];

  async ngOnInit(): Promise<void> {
    this.applyStatusesFromQueryParams();
    this.store.loadFiltered();
    if (this.isAdmin()) {
      const dists = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
      this.distributors.set(dists);
    }
    if (this.canBulkActions()) {
      this.refreshBulkCounts();
    }
  }

  private async refreshBulkCounts(): Promise<void> {
    try {
      const [inProgress, completed] = await Promise.all([
        firstValueFrom(this.orderApi.getFiltered({ statuses: [OrderStatus.InProgress], page: 1, pageSize: 1 })),
        firstValueFrom(this.orderApi.getFiltered({ statuses: [OrderStatus.Completed], page: 1, pageSize: 1 })),
      ]);
      this.inProgressCount.set(inProgress.totalCount);
      this.completedCount.set(completed.totalCount);
    } catch {
      // Silently handle — buttons stay disabled (count = 0)
    }
  }

  confirmBulkValidate(): void {
    const count = this.inProgressCount();
    if (count === 0) {
      return;
    }
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '450px',
      panelClass: 'responsive-dialog',
      data: {
        title: this.translate.instant('orders.bulkConfirmTitle'),
        message: this.translate.instant('orders.bulkValidateConfirmMessage', { count }),
      },
    });
    ref.afterClosed().subscribe(async (confirmed) => {
      if (!confirmed) {
        return;
      }
      this.bulkLoading.set(true);
      try {
        const result = await firstValueFrom(this.orderApi.bulkValidate());
        this.snackBar.open(
          this.translate.instant('orders.bulkSuccess', { count: result.affected }),
          this.translate.instant('common.close'),
          { duration: 4000 },
        );
        this.store.loadFiltered();
        await this.refreshBulkCounts();
      } finally {
        this.bulkLoading.set(false);
      }
    });
  }

  async confirmBulkTransmit(): Promise<void> {
    const count = this.completedCount();
    if (count === 0) {
      return;
    }

    // AQ-377 — bulk LIMS transmission must not be attempted offline; ping first,
    // skip the confirm dialog entirely if the server is unreachable.
    const online = await this.networkCheck.pingServer();
    if (!online) {
      this.snackBar.open(
        this.translate.instant('orders.bulkTransmitNoNetwork'),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
      return;
    }

    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '450px',
      panelClass: 'responsive-dialog',
      data: {
        title: this.translate.instant('orders.bulkConfirmTitle'),
        message: this.translate.instant('orders.bulkTransmitConfirmMessage', { count }),
      },
    });
    ref.afterClosed().subscribe(async (confirmed) => {
      if (!confirmed) {
        return;
      }
      this.bulkLoading.set(true);
      try {
        const result = await firstValueFrom(this.orderApi.bulkTransmit());
        this.snackBar.open(
          this.translate.instant('orders.bulkSuccess', { count: result.affected }),
          this.translate.instant('common.close'),
          { duration: 4000 },
        );
        this.store.loadFiltered();
        await this.refreshBulkCounts();
      } finally {
        this.bulkLoading.set(false);
      }
    });
  }

  private applyStatusesFromQueryParams(): void {
    const raw = this.route.snapshot.queryParamMap.get('statuses');
    if (!raw) {
      return;
    }
    const validValues = new Set<string>(Object.values(OrderStatus));
    const parsed = raw
      .split(',')
      .map((s) => s.trim())
      .filter((s) => validValues.has(s)) as OrderStatus[];
    if (parsed.length > 0) {
      this.store.statusFilter.set(parsed);
      this.store.currentPage.set(1);
    }
  }

  getStatusLabel(order: OrderListDto): string {
    return OrderStatusLabels[order.status] ?? 'orders.status.new';
  }

  getStatusVariant(status: string): StatusChipVariant {
    const map: Record<string, StatusChipVariant> = {
      'New': 'draft',
      'InProgress': 'info',
      'Completed': 'success',
      'Transmitted': 'success',
      'Done': 'success',
      'Cancelled': 'danger',
    };
    return map[status] ?? 'draft';
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

  onUnassignedChange(checked: boolean): void {
    this.store.setUnassignedFilter(checked ? true : undefined);
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
    const dialogRef = this.dialog.open(OrderCreateDialogComponent, { width: '550px', panelClass: 'responsive-dialog' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadFiltered();
      }
    });
  }

  viewDetail(order: OrderListDto): void {
    this.router.navigate(['/orders', order.id]);
  }

  onDistributorChange(distributorId: string | undefined): void {
    this.store.distributorFilter.set(distributorId || undefined);
    this.store.currentPage.set(1);
    this.store.loadFiltered();
  }

  onDateFromChange(date: Date | null): void {
    this.store.dateFromFilter.set(date ? date.toISOString() : undefined);
    this.store.currentPage.set(1);
    this.store.loadFiltered();
  }

  onDateToChange(date: Date | null): void {
    this.store.dateToFilter.set(date ? date.toISOString() : undefined);
    this.store.currentPage.set(1);
    this.store.loadFiltered();
  }

  async exportCsv(): Promise<void> {
    const blob = await firstValueFrom(this.orderApi.exportCsv({
      statuses: this.store.statusFilter().length > 0 ? this.store.statusFilter() : undefined,
      search: this.store.searchFilter() || undefined,
      distributorId: this.store.distributorFilter(),
      dateFrom: this.store.dateFromFilter(),
      dateTo: this.store.dateToFilter(),
    }));
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `orders-export-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
