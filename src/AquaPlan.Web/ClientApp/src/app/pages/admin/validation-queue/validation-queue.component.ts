import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ChangeRequestDatastore } from '../../../datastore/change-request.datastore';
import { ChangeRequestDto } from '../../../models/change-request.model';
import { SamplingLocationApiService } from '../../../services/sampling-location-api.service';
import { SamplingLocationDto } from '../../../models/sampling-location.model';
import { SamplingLocationFormDialogComponent } from '../../sampling-locations/sampling-location-form-dialog.component';
import { RejectDialogComponent, RejectDialogResult } from './reject-dialog.component';

@Component({
  selector: 'app-validation-queue',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatSnackBarModule, MatTabsModule,
    DatePipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'changeRequests.title' | translate }}</h2>
    </div>

    <mat-tab-group>
      <!-- Tab 1: Unvalidated LDP -->
      <mat-tab [label]="'changeRequests.tabLocations' | translate">
        @if (loadingLocations()) {
          <div class="loading-container">
            <mat-spinner diameter="40"></mat-spinner>
          </div>
        } @else {
          <div class="responsive-table-container">
            <table mat-table [dataSource]="unvalidatedLocations()" class="full-width">
              <ng-container matColumnDef="locationCode">
                <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.locationCode' | translate }}</th>
                <td mat-cell *matCellDef="let loc">{{ loc.locationCode }}</td>
              </ng-container>

              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.name' | translate }}</th>
                <td mat-cell *matCellDef="let loc">{{ loc.name }}</td>
              </ng-container>

              <ng-container matColumnDef="distributor">
                <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.distributor' | translate }}</th>
                <td mat-cell *matCellDef="let loc">{{ loc.distributorName }}</td>
              </ng-container>

              <ng-container matColumnDef="sector">
                <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.sector' | translate }}</th>
                <td mat-cell *matCellDef="let loc">{{ loc.sectorName ?? '—' }}</td>
              </ng-container>

              <ng-container matColumnDef="createdAt">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.createdAt' | translate }}</th>
                <td mat-cell *matCellDef="let loc">{{ loc.createdAt | date:'dd.MM.yyyy HH:mm' }}</td>
              </ng-container>

              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.actions' | translate }}</th>
                <td mat-cell *matCellDef="let loc">
                  <button mat-icon-button
                          [matTooltip]="'changeRequests.viewAndValidate' | translate"
                          (click)="openLocationForm(loc)">
                    <mat-icon>visibility</mat-icon>
                  </button>
                  <button mat-icon-button color="primary"
                          [matTooltip]="'changeRequests.validate' | translate"
                          (click)="validateLocation(loc)">
                    <mat-icon>check_circle</mat-icon>
                  </button>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="locationColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: locationColumns;"></tr>
            </table>
          </div>

          @if (unvalidatedLocations().length === 0) {
            <p class="no-data">{{ 'changeRequests.noLocationsPending' | translate }}</p>
          }
        }
      </mat-tab>

      <!-- Tab 2: Change requests -->
      <mat-tab [label]="'changeRequests.tabRequests' | translate">
        @if (store.loading()) {
          <div class="loading-container">
            <mat-spinner diameter="40"></mat-spinner>
          </div>
        } @else {
          <div class="responsive-table-container">
            <table mat-table [dataSource]="store.pendingRequests()" class="full-width">
              <ng-container matColumnDef="requestType">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
                <td mat-cell *matCellDef="let r">
                  <mat-chip class="status-chip">
                    {{ 'changeRequests.' + requestTypeKey(r.requestType) | translate }}
                  </mat-chip>
                </td>
              </ng-container>

              <ng-container matColumnDef="distributorName">
                <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.distributor' | translate }}</th>
                <td mat-cell *matCellDef="let r">{{ r.distributorName ?? '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="proposedName">
                <th mat-header-cell *matHeaderCellDef>{{ 'changeRequests.proposedName' | translate }}</th>
                <td mat-cell *matCellDef="let r">{{ r.proposedName ?? '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="proposedLocationCode">
                <th mat-header-cell *matHeaderCellDef>{{ 'changeRequests.proposedCode' | translate }}</th>
                <td mat-cell *matCellDef="let r">{{ r.proposedLocationCode ?? '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="requestedByName">
                <th mat-header-cell *matHeaderCellDef>{{ 'changeRequests.requestedBy' | translate }}</th>
                <td mat-cell *matCellDef="let r">{{ r.requestedByName ?? '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="requestedAt">
                <th mat-header-cell *matHeaderCellDef>{{ 'orders.createdAt' | translate }}</th>
                <td mat-cell *matCellDef="let r">{{ r.requestedAt | date:'dd.MM.yyyy HH:mm' }}</td>
              </ng-container>

              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.actions' | translate }}</th>
                <td mat-cell *matCellDef="let r">
                  <button mat-icon-button color="primary"
                          [matTooltip]="'changeRequests.approve' | translate"
                          (click)="approveRequest(r)">
                    <mat-icon>check_circle</mat-icon>
                  </button>
                  <button mat-icon-button color="warn"
                          [matTooltip]="'changeRequests.reject' | translate"
                          (click)="openRejectDialog(r)">
                    <mat-icon>cancel</mat-icon>
                  </button>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="requestColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: requestColumns;"></tr>
            </table>
          </div>

          @if (store.pendingRequests().length === 0) {
            <p class="no-data">{{ 'changeRequests.noRequestsPending' | translate }}</p>
          }
        }
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
  `],
})
export class ValidationQueueComponent implements OnInit {
  readonly store = inject(ChangeRequestDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  readonly unvalidatedLocations = signal<SamplingLocationDto[]>([]);
  readonly loadingLocations = signal(false);

  readonly locationColumns = [
    'locationCode', 'name', 'distributor', 'sector', 'createdAt', 'actions',
  ];

  readonly requestColumns = [
    'requestType', 'distributorName', 'proposedName', 'proposedLocationCode',
    'requestedByName', 'requestedAt', 'actions',
  ];

  ngOnInit(): void {
    this.store.loadPendingRequests();
    this.loadUnvalidatedLocations();
  }

  async loadUnvalidatedLocations(): Promise<void> {
    this.loadingLocations.set(true);
    try {
      const locations = await firstValueFrom(this.locationApi.getUnvalidated());
      this.unvalidatedLocations.set(locations);
    } finally {
      this.loadingLocations.set(false);
    }
  }

  openLocationForm(location: SamplingLocationDto): void {
    const dialogRef = this.dialog.open(SamplingLocationFormDialogComponent, {
      width: '550px',
      panelClass: 'responsive-dialog',
      data: {
        mode: 'edit',
        locationId: location.id,
        readonly: false,
        userDistributorId: null,
        isAdmin: true,
        showValidateButton: true,
      },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadUnvalidatedLocations();
      }
    });
  }

  async validateLocation(location: SamplingLocationDto): Promise<void> {
    try {
      await firstValueFrom(this.locationApi.validate(location.id));
      this.snackBar.open(
        this.translate.instant('changeRequests.locationValidated'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
      this.loadUnvalidatedLocations();
    } catch {
      this.snackBar.open(
        this.translate.instant('common.error'),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    }
  }

  requestTypeKey(type: string): string {
    switch (type) {
      case 'Create': return 'create';
      case 'Update': return 'update';
      case 'Deactivate': return 'deactivate';
      default: return type.toLowerCase();
    }
  }

  async approveRequest(request: ChangeRequestDto): Promise<void> {
    await this.store.approve(request.id, null);
  }

  openRejectDialog(request: ChangeRequestDto): void {
    const dialogRef = this.dialog.open(RejectDialogComponent, {
      width: '450px',
      panelClass: 'responsive-dialog',
    });
    dialogRef.afterClosed().subscribe((result: RejectDialogResult | undefined) => {
      if (result?.comment) {
        this.store.reject(request.id, result.comment);
      }
    });
  }
}
