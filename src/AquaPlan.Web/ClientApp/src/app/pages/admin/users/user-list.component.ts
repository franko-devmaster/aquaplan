import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { UserDatastore } from '../../../datastore/user.datastore';
import { UserListDto } from '../../../models/user.model';
import { UserFormDialogComponent } from './user-form-dialog.component';
import { RoleApiService } from '../../../services/role-api.service';
import { DistributorApiService } from '../../../services/distributor-api.service';
import { RoleDto } from '../../../models/role.model';
import { DistributorListDto } from '../../../models/distributor.model';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    MatFormFieldModule, MatSelectModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'users.title' | translate }}</h2>
      <button mat-raised-button color="primary" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon>
        {{ 'users.createUser' | translate }}
      </button>
    </div>

    <div class="filters">
      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'users.status' | translate }}</mat-label>
        <mat-select [(ngModel)]="filterStatus" (selectionChange)="applyFilters()">
          <mat-option [value]="'all'">{{ 'common.all' | translate }}</mat-option>
          <mat-option [value]="'active'">{{ 'common.active' | translate }}</mat-option>
          <mat-option [value]="'inactive'">{{ 'common.inactive' | translate }}</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'users.role' | translate }}</mat-label>
        <mat-select [(ngModel)]="filterRole" (selectionChange)="applyFilters()">
          <mat-option [value]="''">{{ 'common.all' | translate }}</mat-option>
          @for (role of roles(); track role.id) {
            <mat-option [value]="role.name">{{ role.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="filter-field">
        <mat-label>{{ 'users.distributor' | translate }}</mat-label>
        <mat-select [(ngModel)]="filterDistributorId" (selectionChange)="applyFilters()">
          <mat-option [value]="''">{{ 'common.all' | translate }}</mat-option>
          @for (dist of distributors(); track dist.id) {
            <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.users()" class="full-width">
          <ng-container matColumnDef="userNumber">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.userNumber' | translate }}</th>
            <td mat-cell *matCellDef="let user">{{ user.userNumber }}</td>
          </ng-container>

          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.lastName' | translate }}</th>
            <td mat-cell *matCellDef="let user">{{ user.lastName }}, {{ user.firstName }}</td>
          </ng-container>

          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.email' | translate }}</th>
            <td mat-cell *matCellDef="let user">{{ user.email }}</td>
          </ng-container>

          <ng-container matColumnDef="distributors">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.distributor' | translate }}</th>
            <td mat-cell *matCellDef="let user">{{ user.distributorName ?? '-' }}</td>
          </ng-container>

          <ng-container matColumnDef="roles">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.role' | translate }}</th>
            <td mat-cell *matCellDef="let user">
              @if (user.role) {
                <mat-chip>{{ user.role }}</mat-chip>
              } @else {
                -
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.status' | translate }}</th>
            <td mat-cell *matCellDef="let user">
              <mat-chip class="status-chip" [highlighted]="user.isActive" [class.inactive]="!user.isActive">
                {{ (user.isActive ? 'common.active' : 'common.inactive') | translate }}
              </mat-chip>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"
              class="clickable-row" (click)="openEditDialog(row)"></tr>
        </table>
      </div>

      @if (store.users().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .filters { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
    .filter-field { min-width: 160px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .inactive { opacity: 0.6; }
    mat-chip { margin: 2px; }
  `],
})
export class UserListComponent implements OnInit {
  readonly store = inject(UserDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly roleApi = inject(RoleApiService);
  private readonly distributorApi = inject(DistributorApiService);

  readonly roles = signal<RoleDto[]>([]);
  readonly distributors = signal<DistributorListDto[]>([]);

  readonly displayedColumns = ['userNumber', 'name', 'email', 'distributors', 'roles', 'status'];

  filterStatus = 'all';
  filterRole = '';
  filterDistributorId = '';

  async ngOnInit(): Promise<void> {
    const [allRoles, allDistributors] = await Promise.all([
      firstValueFrom(this.roleApi.getAll()),
      firstValueFrom(this.distributorApi.getAll()),
    ]);
    this.roles.set(allRoles);
    this.distributors.set(allDistributors);
    this.applyFilters();
  }

  applyFilters(): void {
    const filter: { role?: string; distributorId?: string; isActive?: boolean } = {};
    if (this.filterStatus === 'active') filter.isActive = true;
    else if (this.filterStatus === 'inactive') filter.isActive = false;
    if (this.filterRole) filter.role = this.filterRole;
    if (this.filterDistributorId) filter.distributorId = this.filterDistributorId;
    this.store.loadAll(filter);
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(UserFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'create' },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.applyFilters();
      }
    });
  }

  openEditDialog(user: UserListDto): void {
    const dialogRef = this.dialog.open(UserFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'edit', userId: user.id },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.applyFilters();
      }
    });
  }
}
