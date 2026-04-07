import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
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
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { OrderListDto, OrderStatus, OrderStatusLabels } from '../../models/order.model';
import { OrderCreateDialogComponent } from './order-create-dialog.component';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule,
    MatPaginatorModule, MatSortModule,
    DatePipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'orders.title' | translate }}</h2>
      <button mat-raised-button color="primary" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon>
        {{ 'orders.createOrder' | translate }}
      </button>
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
              <mat-chip>{{ getStatusLabel(order) | translate }}</mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef mat-sort-header="distributor">{{ 'orders.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let order" [attr.data-label]="'orders.distributor' | translate">{{ order.distributorName }}</td>
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

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>{{ 'common.actions' | translate }}</th>
            <td mat-cell *matCellDef="let order">
              <button mat-icon-button [matTooltip]="'orders.details' | translate"
                      (click)="viewDetail(order)">
                <mat-icon>visibility</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
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
    .filters-row { display: flex; gap: 16px; align-items: center; margin-bottom: 16px; flex-wrap: wrap; }
    .filter-field { min-width: 200px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
  `],
})
export class OrderListComponent implements OnInit {
  readonly store = inject(OrderDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly searchValue = signal('');
  private searchTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly displayedColumns = [
    'orderNumber', 'status', 'distributor', 'samplingLocation',
    'preleveur', 'plannedDate', 'createdAt', 'actions',
  ];

  readonly availableStatuses = [
    { value: OrderStatus.Draft, label: OrderStatusLabels[OrderStatus.Draft] },
    { value: OrderStatus.Assigned, label: OrderStatusLabels[OrderStatus.Assigned] },
    { value: OrderStatus.InProgress, label: OrderStatusLabels[OrderStatus.InProgress] },
    { value: OrderStatus.SamplingCompleted, label: OrderStatusLabels[OrderStatus.SamplingCompleted] },
    { value: OrderStatus.Validated, label: OrderStatusLabels[OrderStatus.Validated] },
    { value: OrderStatus.SentToLims, label: OrderStatusLabels[OrderStatus.SentToLims] },
    { value: OrderStatus.ResultsReceived, label: OrderStatusLabels[OrderStatus.ResultsReceived] },
    { value: OrderStatus.Completed, label: OrderStatusLabels[OrderStatus.Completed] },
    { value: OrderStatus.Cancelled, label: OrderStatusLabels[OrderStatus.Cancelled] },
  ];

  ngOnInit(): void {
    this.store.loadFiltered();
  }

  getStatusLabel(order: OrderListDto): string {
    return OrderStatusLabels[order.status] ?? 'orders.status.draft';
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
}
