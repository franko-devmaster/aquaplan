import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { OrderListDto, OrderStatusLabels } from '../../models/order.model';
import { OrderCreateDialogComponent } from './order-create-dialog.component';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
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

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="store.orders()" class="full-width">
        <ng-container matColumnDef="orderNumber">
          <th mat-header-cell *matHeaderCellDef>{{ 'orders.orderNumber' | translate }}</th>
          <td mat-cell *matCellDef="let order">{{ order.orderNumber }}</td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'orders.status.label' | translate }}</th>
          <td mat-cell *matCellDef="let order">
            <mat-chip>{{ getStatusLabel(order) | translate }}</mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="distributor">
          <th mat-header-cell *matHeaderCellDef>{{ 'orders.distributor' | translate }}</th>
          <td mat-cell *matCellDef="let order">{{ order.distributorName }}</td>
        </ng-container>

        <ng-container matColumnDef="preleveur">
          <th mat-header-cell *matHeaderCellDef>{{ 'orders.preleveur' | translate }}</th>
          <td mat-cell *matCellDef="let order">{{ order.preleveurName ?? '-' }}</td>
        </ng-container>

        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>{{ 'orders.createdAt' | translate }}</th>
          <td mat-cell *matCellDef="let order">{{ order.createdAt | date:'shortDate' }}</td>
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

      @if (store.orders().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
  `],
})
export class OrderListComponent implements OnInit {
  readonly store = inject(OrderDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly displayedColumns = ['orderNumber', 'status', 'distributor', 'preleveur', 'createdAt', 'actions'];

  ngOnInit(): void {
    this.store.loadAll();
  }

  getStatusLabel(order: OrderListDto): string {
    return OrderStatusLabels[order.status] ?? 'orders.status.draft';
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(OrderCreateDialogComponent, { width: '450px' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll();
      }
    });
  }

  viewDetail(order: OrderListDto): void {
    this.router.navigate(['/orders', order.id]);
  }
}
