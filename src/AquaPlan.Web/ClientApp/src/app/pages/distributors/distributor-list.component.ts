import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
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
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { DistributorDatastore } from '../../datastore/distributor.datastore';
import { DistributorListDto } from '../../models/distributor.model';
import { DistributorFormDialogComponent } from './distributor-form-dialog.component';
import { StatusChipComponent } from '../../components/status-chip/status-chip.component';

@Component({
  selector: 'app-distributor-list',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, FormsModule,
    TranslateModule, StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'distributors.title' | translate }}</h2>
      <button mat-raised-button color="primary" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon>
        {{ 'distributors.createDistributor' | translate }}
      </button>
    </div>

    <div class="filters filters-row">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'distributors.name' | translate }}</mat-label>
        <input matInput [(ngModel)]="filterName" (keyup.enter)="applyFilter()">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'distributors.status' | translate }}</mat-label>
        <mat-select [(ngModel)]="filterStatus" (selectionChange)="applyFilter()">
          <mat-option>{{ 'distributors.allStatuses' | translate }}</mat-option>
          <mat-option [value]="true">{{ 'common.active' | translate }}</mat-option>
          <mat-option [value]="false">{{ 'common.inactive' | translate }}</mat-option>
        </mat-select>
      </mat-form-field>
      <button mat-icon-button [matTooltip]="'common.search' | translate" (click)="applyFilter()">
        <mat-icon>search</mat-icon>
      </button>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.distributors()" class="full-width">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'distributors.name' | translate }}</th>
            <td mat-cell *matCellDef="let d" [attr.data-label]="'distributors.name' | translate">{{ d.name }}</td>
          </ng-container>

          <ng-container matColumnDef="cantonRegion">
            <th mat-header-cell *matHeaderCellDef>{{ 'distributors.cantonRegion' | translate }}</th>
            <td mat-cell *matCellDef="let d" [attr.data-label]="'distributors.cantonRegion' | translate">{{ d.cantonRegion ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="distributionNetwork">
            <th mat-header-cell *matHeaderCellDef>{{ 'distributors.distributionNetwork' | translate }}</th>
            <td mat-cell *matCellDef="let d" [attr.data-label]="'distributors.distributionNetwork' | translate">{{ d.distributionNetwork ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>{{ 'distributors.status' | translate }}</th>
            <td mat-cell *matCellDef="let d" [attr.data-label]="'distributors.status' | translate">
              <app-status-chip [variant]="d.isActive ? 'success' : 'draft'"
                               [label]="((d.isActive ? 'common.active' : 'common.inactive') | translate)"></app-status-chip>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" (click)="openEditDialog(row)"></tr>
        </table>
      </div>

      @if (store.distributors().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .filters { display: flex; gap: 16px; align-items: center; margin-bottom: 16px; flex-wrap: wrap; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .inactive { opacity: 0.6; }
  `],
})
export class DistributorListComponent implements OnInit {
  readonly store = inject(DistributorDatastore);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = ['name', 'cantonRegion', 'distributionNetwork', 'status'];

  filterName = '';
  filterStatus: boolean | undefined;

  ngOnInit(): void {
    this.store.loadAll();
  }

  applyFilter(): void {
    this.store.loadAll({
      name: this.filterName || undefined,
      isActive: this.filterStatus,
    });
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(DistributorFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'create' },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll();
      }
    });
  }

  openEditDialog(distributor: DistributorListDto): void {
    const dialogRef = this.dialog.open(DistributorFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'edit', distributorId: distributor.id },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll();
      }
    });
  }

}
