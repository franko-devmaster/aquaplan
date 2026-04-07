import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { UserDatastore } from '../../../datastore/user.datastore';
import { UserListDto } from '../../../models/user.model';
import { UserFormDialogComponent } from './user-form-dialog.component';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatDialogModule, MatProgressSpinnerModule, MatTooltipModule,
    TranslateModule,
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

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="responsive-table-container">
        <table mat-table [dataSource]="store.users()" class="full-width">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.lastName' | translate }}</th>
            <td mat-cell *matCellDef="let user" [attr.data-label]="'users.lastName' | translate">{{ user.lastName }}, {{ user.firstName }}</td>
          </ng-container>

          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.email' | translate }}</th>
            <td mat-cell *matCellDef="let user" [attr.data-label]="'users.email' | translate">{{ user.email }}</td>
          </ng-container>

          <ng-container matColumnDef="roles">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.roles' | translate }}</th>
            <td mat-cell *matCellDef="let user" [attr.data-label]="'users.roles' | translate">
              @for (role of user.roles; track role) {
                <mat-chip>{{ role }}</mat-chip>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>{{ 'users.status' | translate }}</th>
            <td mat-cell *matCellDef="let user" [attr.data-label]="'users.status' | translate">
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

  readonly displayedColumns = ['name', 'email', 'roles', 'status'];

  ngOnInit(): void {
    this.store.loadAll();
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(UserFormDialogComponent, {
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

  openEditDialog(user: UserListDto): void {
    const dialogRef = this.dialog.open(UserFormDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { mode: 'edit', userId: user.id },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.store.loadAll();
      }
    });
  }

}
