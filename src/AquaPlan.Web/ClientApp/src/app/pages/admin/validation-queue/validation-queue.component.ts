import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { ChangeRequestDatastore } from '../../../datastore/change-request.datastore';
import { ChangeRequestDto } from '../../../models/change-request.model';
import { RejectDialogComponent, RejectDialogResult } from './reject-dialog.component';

@Component({
  selector: 'app-validation-queue',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    DatePipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'changeRequests.title' | translate }}</h2>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="store.pendingRequests()" class="full-width">
        <ng-container matColumnDef="requestType">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
          <td mat-cell *matCellDef="let r">
            <mat-chip>
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

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
      </table>

      @if (store.pendingRequests().length === 0) {
        <p class="no-data">{{ 'changeRequests.noRequestsPending' | translate }}</p>
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
export class ValidationQueueComponent implements OnInit {
  readonly store = inject(ChangeRequestDatastore);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'requestType', 'distributorName', 'proposedName', 'proposedLocationCode',
    'requestedByName', 'requestedAt', 'actions',
  ];

  ngOnInit(): void {
    this.store.loadPendingRequests();
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
    });
    dialogRef.afterClosed().subscribe((result: RejectDialogResult | undefined) => {
      if (result?.comment) {
        this.store.reject(request.id, result.comment);
      }
    });
  }
}
