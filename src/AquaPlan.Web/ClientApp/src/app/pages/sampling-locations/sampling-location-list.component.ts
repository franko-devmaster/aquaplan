import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { SamplingLocationDatastore, DistributorOption } from '../../datastore/sampling-location.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { AuthService } from '../../services/auth.service';
import { SamplingLocationDto, SamplingLocationFilteringInputDto } from '../../models/sampling-location.model';
import { SamplingLocationFormDialogComponent } from './sampling-location-form-dialog.component';

@Component({
  selector: 'app-sampling-location-list',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTooltipModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatPaginatorModule, MatDialogModule, DecimalPipe, NgClass, FormsModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'samplingLocations.title' | translate }}</h2>
      <div class="header-actions">
        <button mat-stroked-button (click)="exportPdf()">
          <mat-icon>picture_as_pdf</mat-icon>
          {{ 'samplingLocations.exportPdf' | translate }}
        </button>
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon>
          {{ 'samplingLocations.createLocation' | translate }}
        </button>
      </div>
    </div>

    <div class="filters-row">
      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [(ngModel)]="searchText" (keyup.enter)="applyFilters()"
               [placeholder]="'samplingLocations.searchPlaceholder' | translate">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingLocations.distributor' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedDistributorId" (selectionChange)="applyFilters()">
          <mat-option value="">{{ 'samplingLocations.allDistributors' | translate }}</mat-option>
          @for (dist of store.distributors(); track dist.id) {
            <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'samplingLocations.status' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedStatus" (selectionChange)="applyFilters()">
          <mat-option value="">{{ 'samplingLocations.allStatuses' | translate }}</mat-option>
          <mat-option value="true">{{ 'common.active' | translate }}</mat-option>
          <mat-option value="false">{{ 'common.inactive' | translate }}</mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="filteredLocations()" class="full-width">
          <ng-container matColumnDef="locationCode">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.locationCode' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.locationCode' | translate">{{ loc.locationCode }}</td>
          </ng-container>

          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.name' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.name' | translate">{{ loc.name }}</td>
          </ng-container>

          <ng-container matColumnDef="distributor">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.distributor' | translate">{{ loc.distributorName }}</td>
          </ng-container>

          <ng-container matColumnDef="sector">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.sector' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.sector' | translate">{{ loc.sectorName ?? '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="coordinates">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.latitude' | translate }} / {{ 'samplingLocations.longitude' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.coordinates' | translate">
              @if (loc.latitude && loc.longitude) {
                {{ loc.latitude | number:'1.4-4' }}, {{ loc.longitude | number:'1.4-4' }}
              } @else {
                -
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.status' | translate }}</th>
            <td mat-cell *matCellDef="let loc" [attr.data-label]="'samplingLocations.status' | translate">
              <span class="status-badge" [ngClass]="loc.isActive ? 'status-active' : 'status-inactive'">
                {{ (loc.isActive ? 'common.active' : 'common.inactive') | translate }}
              </span>
              @if (!loc.isValidated) {
                <span class="status-badge status-to-validate" style="margin-left: 4px;">
                  {{ 'samplingLocations.toValidate' | translate }}
                </span>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" [class.inactive-row]="!row.isActive"
              (click)="openViewOrEditDialog(row)"></tr>
        </table>
      </div>

      @if (filteredLocations().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }

      @if (store.locations().length > pageSize) {
        <mat-paginator
          [length]="totalFilteredCount()"
          [pageSize]="pageSize"
          [pageSizeOptions]="[10, 25, 50]"
          (page)="onPageChange($event)"
          showFirstLastButtons>
        </mat-paginator>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .header-actions { display: flex; gap: 8px; }
    .filters-row { display: flex; gap: 16px; margin-bottom: 16px; flex-wrap: wrap; }
    .filter-field { min-width: 200px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .inactive { opacity: 0.6; }
    .inactive-row { opacity: 0.6; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background-color: rgba(0, 0, 0, 0.04); }
  `],
})
export class SamplingLocationListComponent implements OnInit {
  readonly store = inject(SamplingLocationDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly apiService = inject(SamplingLocationApiService);
  private readonly authService = inject(AuthService);

  readonly displayedColumns = ['locationCode', 'name', 'distributor', 'sector', 'coordinates', 'status'];

  searchText = '';
  selectedDistributorId = '';
  selectedStatus = '';
  pageSize = 25;
  currentPage = 0;

  readonly filteredLocations = signal<SamplingLocationDto[]>([]);
  readonly totalFilteredCount = signal(0);

  isAdmin(): boolean {
    const user = this.authService.currentUser();
    return user?.roles.includes('Administrator') ?? false;
  }

  ngOnInit(): void {
    this.store.loadAll().then(() => this.applyFilters());
  }

  applyFilters(): void {
    let locations = this.store.locations();

    if (this.searchText.trim()) {
      const search = this.searchText.toLowerCase();
      locations = locations.filter(loc =>
        loc.name.toLowerCase().includes(search) ||
        loc.locationCode.toLowerCase().includes(search)
      );
    }

    if (this.selectedDistributorId) {
      locations = locations.filter(loc => loc.distributorId === this.selectedDistributorId);
    }

    if (this.selectedStatus !== '') {
      const isActive = this.selectedStatus === 'true';
      locations = locations.filter(loc => loc.isActive === isActive);
    }

    this.totalFilteredCount.set(locations.length);
    this.currentPage = 0;

    const start = this.currentPage * this.pageSize;
    this.filteredLocations.set(locations.slice(start, start + this.pageSize));
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;

    let locations = this.getFilteredAll();
    const start = this.currentPage * this.pageSize;
    this.filteredLocations.set(locations.slice(start, start + this.pageSize));
  }

  openCreateDialog(): void {
    const user = this.authService.currentUser();
    const dialogRef = this.dialog.open(SamplingLocationFormDialogComponent, {
      width: '550px',
      panelClass: 'responsive-dialog',
      data: {
        mode: 'create',
        readonly: false,
        userDistributorId: user?.distributorId ?? null,
        isAdmin: this.isAdmin(),
      },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll().then(() => this.applyFilters());
      }
    });
  }

  openViewOrEditDialog(location: SamplingLocationDto): void {
    const readonly = !this.isAdmin();
    const dialogRef = this.dialog.open(SamplingLocationFormDialogComponent, {
      width: '550px',
      panelClass: 'responsive-dialog',
      data: {
        mode: 'edit',
        locationId: location.id,
        readonly,
        userDistributorId: this.authService.currentUser()?.distributorId ?? null,
        isAdmin: this.isAdmin(),
      },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll().then(() => this.applyFilters());
      }
    });
  }

  exportPdf(): void {
    const distributorId = this.selectedDistributorId || undefined;
    this.apiService.exportPdf(distributorId).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `lieux-prelevement-${new Date().toISOString().slice(0, 10)}.pdf`;
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }

  private getFilteredAll(): SamplingLocationDto[] {
    let locations = this.store.locations();

    if (this.searchText.trim()) {
      const search = this.searchText.toLowerCase();
      locations = locations.filter(loc =>
        loc.name.toLowerCase().includes(search) ||
        loc.locationCode.toLowerCase().includes(search)
      );
    }

    if (this.selectedDistributorId) {
      locations = locations.filter(loc => loc.distributorId === this.selectedDistributorId);
    }

    if (this.selectedStatus !== '') {
      const isActive = this.selectedStatus === 'true';
      locations = locations.filter(loc => loc.isActive === isActive);
    }

    return locations;
  }
}
