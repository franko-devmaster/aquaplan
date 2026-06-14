import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import {
  DelegationApiService,
  DistributorDelegationDto,
  DistributorDelegationCreateDto,
} from '../../../services/delegation-api.service';
import { DistributorApiService } from '../../../services/distributor-api.service';
import { DistributorListDto } from '../../../models/distributor.model';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog.component';

@Component({
  selector: 'app-delegation-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatDatepickerModule, MatSnackBarModule,
    DatePipe, TranslateModule,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'delegations.title' | translate }}</h2>
    </div>

    <div class="create-form">
      <h3>{{ 'delegations.createDelegation' | translate }}</h3>
      <div class="form-row">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'delegations.delegatingDistributor' | translate }}</mat-label>
          <mat-select [(ngModel)]="newDelegation.delegatingDistributorId">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'delegations.delegatedToDistributor' | translate }}</mat-label>
          <mat-select [(ngModel)]="newDelegation.delegatedToDistributorId">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'delegations.validFrom' | translate }}</mat-label>
          <input matInput [matDatepicker]="pickerFrom" [(ngModel)]="newDelegation.validFrom">
          <mat-datepicker-toggle matIconSuffix [for]="pickerFrom"></mat-datepicker-toggle>
          <mat-datepicker #pickerFrom></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'delegations.validTo' | translate }}</mat-label>
          <input matInput [matDatepicker]="pickerTo" [(ngModel)]="newDelegation.validTo">
          <mat-datepicker-toggle matIconSuffix [for]="pickerTo"></mat-datepicker-toggle>
          <mat-datepicker #pickerTo></mat-datepicker>
        </mat-form-field>

        <button mat-flat-button color="primary" (click)="createDelegation()"
                [disabled]="!canCreate()">
          <mat-icon>add</mat-icon>
          {{ 'common.create' | translate }}
        </button>
      </div>
    </div>

    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="delegations()" class="full-width">
        <ng-container matColumnDef="delegatingDistributor">
          <th mat-header-cell *matHeaderCellDef>{{ 'delegations.delegatingDistributor' | translate }}</th>
          <td mat-cell *matCellDef="let d">{{ d.delegatingDistributorName }}</td>
        </ng-container>

        <ng-container matColumnDef="delegatedToDistributor">
          <th mat-header-cell *matHeaderCellDef>{{ 'delegations.delegatedToDistributor' | translate }}</th>
          <td mat-cell *matCellDef="let d">{{ d.delegatedToDistributorName }}</td>
        </ng-container>

        <ng-container matColumnDef="validFrom">
          <th mat-header-cell *matHeaderCellDef>{{ 'delegations.validFrom' | translate }}</th>
          <td mat-cell *matCellDef="let d">{{ d.validFrom | date:'shortDate' }}</td>
        </ng-container>

        <ng-container matColumnDef="validTo">
          <th mat-header-cell *matHeaderCellDef>{{ 'delegations.validTo' | translate }}</th>
          <td mat-cell *matCellDef="let d">{{ d.validTo ? (d.validTo | date:'shortDate') : '-' }}</td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
          <td mat-cell *matCellDef="let d">
            <mat-chip [class.active-chip]="d.isActive">
              {{ (d.isActive ? 'delegations.active' : 'delegations.inactive') | translate }}
            </mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.actions' | translate }}</th>
          <td mat-cell *matCellDef="let d">
            <button mat-icon-button color="warn" (click)="confirmDelete(d)">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
      </table>

      @if (delegations().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .create-form { margin-bottom: 24px; padding: 16px; border: 1px solid #e0e0e0; border-radius: 8px; }
    .create-form h3 { margin: 0 0 16px; }
    .form-row { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    .form-row mat-form-field { flex: 1; min-width: 180px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .active-chip { background-color: #e8f5e9 !important; }
  `],
})
export class DelegationListComponent implements OnInit {
  private readonly delegationApi = inject(DelegationApiService);
  private readonly distributorApi = inject(DistributorApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  readonly delegations = signal<DistributorDelegationDto[]>([]);
  readonly distributors = signal<DistributorListDto[]>([]);
  readonly loading = signal(false);

  readonly displayedColumns = [
    'delegatingDistributor', 'delegatedToDistributor',
    'validFrom', 'validTo', 'status', 'actions',
  ];

  newDelegation: { delegatingDistributorId: string; delegatedToDistributorId: string; validFrom: Date | null; validTo: Date | null } = {
    delegatingDistributorId: '',
    delegatedToDistributorId: '',
    validFrom: null,
    validTo: null,
  };

  async ngOnInit(): Promise<void> {
    await this.loadData();
  }

  canCreate(): boolean {
    return !!this.newDelegation.delegatingDistributorId
      && !!this.newDelegation.delegatedToDistributorId
      && this.newDelegation.delegatingDistributorId !== this.newDelegation.delegatedToDistributorId
      && this.newDelegation.validFrom !== null;
  }

  async createDelegation(): Promise<void> {
    if (!this.canCreate()) return;

    const dto: DistributorDelegationCreateDto = {
      delegatingDistributorId: this.newDelegation.delegatingDistributorId,
      delegatedToDistributorId: this.newDelegation.delegatedToDistributorId,
      validFrom: this.newDelegation.validFrom!.toISOString(),
      validTo: this.newDelegation.validTo?.toISOString() ?? null,
    };

    await firstValueFrom(this.delegationApi.create(dto));
    this.resetForm();
    await this.loadDelegations();
  }

  confirmDelete(delegation: DistributorDelegationDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: this.translate.instant('delegations.deleteDelegation'),
        message: this.translate.instant('delegations.confirmDelete'),
      },
    });
    dialogRef.afterClosed().subscribe(async (confirmed) => {
      if (confirmed) {
        await firstValueFrom(this.delegationApi.delete(delegation.id));
        await this.loadDelegations();
      }
    });
  }

  private async loadData(): Promise<void> {
    this.loading.set(true);
    try {
      const [delegations, distributors] = await Promise.all([
        firstValueFrom(this.delegationApi.getAll()),
        firstValueFrom(this.distributorApi.getAll({ isActive: true })),
      ]);
      this.delegations.set(delegations);
      this.distributors.set(distributors);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadDelegations(): Promise<void> {
    const delegations = await firstValueFrom(this.delegationApi.getAll());
    this.delegations.set(delegations);
  }

  private resetForm(): void {
    this.newDelegation = {
      delegatingDistributorId: '',
      delegatedToDistributorId: '',
      validFrom: null,
      validTo: null,
    };
  }
}
