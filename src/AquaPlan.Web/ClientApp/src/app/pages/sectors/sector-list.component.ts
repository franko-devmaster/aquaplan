import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
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
import { SectorDatastore } from '../../datastore/sector.datastore';
import { SectorListDto } from '../../models/sector.model';
import { SectorFormDialogComponent } from './sector-form-dialog.component';
import { AuthService } from '../../services/auth.service';
import { StatusChipComponent } from '../../components/status-chip/status-chip.component';

@Component({
  selector: 'app-sector-list',
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
      <h2>{{ 'sectors.title' | translate }}</h2>
      @if (isAdmin()) {
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon>
          {{ 'sectors.addSector' | translate }}
        </button>
      }
    </div>

    @if (isAdmin()) {
      <div class="filters filters-row">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'sectors.name' | translate }}</mat-label>
          <input matInput [(ngModel)]="filterName" (keyup.enter)="applyFilter()">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'sectors.status' | translate }}</mat-label>
          <mat-select [(ngModel)]="filterStatus" (selectionChange)="applyFilter()">
            <mat-option>{{ 'sectors.allStatuses' | translate }}</mat-option>
            <mat-option [value]="true">{{ 'common.active' | translate }}</mat-option>
            <mat-option [value]="false">{{ 'common.inactive' | translate }}</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-icon-button [matTooltip]="'common.search' | translate" (click)="applyFilter()">
          <mat-icon>search</mat-icon>
        </button>
      </div>
    }

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.sectors()" class="full-width">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'sectors.name' | translate }}</th>
            <td mat-cell *matCellDef="let s" [attr.data-label]="'sectors.name' | translate">{{ s.name }}</td>
          </ng-container>

          <ng-container matColumnDef="code">
            <th mat-header-cell *matHeaderCellDef>{{ 'sectors.code' | translate }}</th>
            <td mat-cell *matCellDef="let s" [attr.data-label]="'sectors.code' | translate">{{ s.code }}</td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef>{{ 'sectors.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let s" [attr.data-label]="'sectors.distributor' | translate">{{ s.distributorName ?? '\u2014' }}</td>
          </ng-container>

          <ng-container matColumnDef="description">
            <th mat-header-cell *matHeaderCellDef>{{ 'sectors.description' | translate }}</th>
            <td mat-cell *matCellDef="let s" [attr.data-label]="'sectors.description' | translate">{{ s.description ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>{{ 'sectors.status' | translate }}</th>
            <td mat-cell *matCellDef="let s" [attr.data-label]="'sectors.status' | translate">
              <app-status-chip [variant]="s.isActive ? 'success' : 'draft'"
                               [label]="((s.isActive ? 'common.active' : 'common.inactive') | translate)"></app-status-chip>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              [class.clickable-row]="isAdmin()" (click)="openEditDialog(row)"></tr>
        </table>
      </div>

      @if (store.sectors().length === 0) {
        <p class="no-data">{{ 'sectors.noSectors' | translate }}</p>
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
export class SectorListComponent implements OnInit {
  readonly store = inject(SectorDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly authService = inject(AuthService);

  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );

  readonly displayedColumns = ['name', 'code', 'distributor', 'description', 'status'];

  filterName = '';
  filterStatus: boolean | undefined;

  ngOnInit(): void {
    const user = this.authService.currentUser();
    if (this.isAdmin()) {
      this.store.loadAll();
    } else {
      // Non-admin: only load sectors for their distributor
      this.store.loadAll({ distributorId: user?.distributorId ?? undefined });
    }
  }

  applyFilter(): void {
    this.store.loadAll({
      name: this.filterName || undefined,
      isActive: this.filterStatus,
    });
  }

  openCreateDialog(): void {
    if (!this.isAdmin()) return;
    const dialogRef = this.dialog.open(SectorFormDialogComponent, {
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

  openEditDialog(sector: SectorListDto): void {
    if (!this.isAdmin()) return;
    const dialogRef = this.dialog.open(SectorFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'edit', sectorId: sector.id },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll();
      }
    });
  }
}
