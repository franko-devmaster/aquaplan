import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { UserApiService } from '../../../services/user-api.service';
import { RoleApiService } from '../../../services/role-api.service';
import { DistributorApiService } from '../../../services/distributor-api.service';
import { RoleDto } from '../../../models/role.model';
import { DistributorListDto } from '../../../models/distributor.model';
import { AuthService } from '../../../services/auth.service';

export interface UserFormDialogData {
  mode: 'create' | 'edit';
  userId?: string;
}

@Component({
  selector: 'app-user-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatSelectModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.mode === 'create' ? 'users.createUser' : 'users.editUser') | translate }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        @if (data.mode === 'edit') {
          <div class="readonly-field">
            <span class="readonly-label">{{ 'users.userNumber' | translate }}</span>
            <span class="readonly-value">{{ userNumber() }}</span>
          </div>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.email' | translate }}</mat-label>
          <input matInput formControlName="email" type="email">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.firstName' | translate }}</mat-label>
          <input matInput formControlName="firstName">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.lastName' | translate }}</mat-label>
          <input matInput formControlName="lastName">
        </mat-form-field>

        @if (data.mode === 'create') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'users.distributor' | translate }}</mat-label>
            <mat-select formControlName="distributorId">
              <mat-option [value]="null">-</mat-option>
              @for (dist of distributors(); track dist.id) {
                <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        } @else {
          <div class="readonly-field">
            <span class="readonly-label">{{ 'users.distributor' | translate }}</span>
            <span class="readonly-value">{{ distributorName() ?? '-' }}</span>
          </div>
        }

        @if (data.mode === 'create') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'users.password' | translate }}</mat-label>
            <input matInput formControlName="password" type="password">
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.role' | translate }}</mat-label>
          <mat-select formControlName="role">
            <mat-option [value]="null">-</mat-option>
            @for (role of roles(); track role.id) {
              <mat-option [value]="role.name">{{ role.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button color="primary" (click)="onSubmit()"
              [disabled]="form.invalid || saving()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.save' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .form-container { display: flex; flex-direction: column; min-width: 400px; }
    .full-width { width: 100%; }
    .readonly-field { display: flex; justify-content: space-between; padding: 12px 0; border-bottom: 1px solid #e0e0e0; margin-bottom: 12px; }
    .readonly-label { font-weight: 500; color: #666; }
    .readonly-value { font-weight: 500; }
  `],
})
export class UserFormDialogComponent implements OnInit {
  readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly userApi = inject(UserApiService);
  private readonly roleApi = inject(RoleApiService);
  private readonly distributorApi = inject(DistributorApiService);
  private readonly authService = inject(AuthService);

  readonly roles = signal<RoleDto[]>([]);
  readonly distributors = signal<DistributorListDto[]>([]);
  readonly userNumber = signal<number>(0);
  readonly distributorName = signal<string | null>(null);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      distributorId: [null as string | null],
      password: ['', this.data.mode === 'create' ? Validators.required : []],
      role: [null as string | null],
    });
  }

  async ngOnInit(): Promise<void> {
    const allRoles = await firstValueFrom(this.roleApi.getAll());
    this.roles.set(allRoles);

    if (this.data.mode === 'create') {
      const allDistributors = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
      this.distributors.set(allDistributors);
    }

    if (this.data.mode === 'edit' && this.data.userId) {
      const user = await firstValueFrom(this.userApi.getById(this.data.userId));
      this.userNumber.set(user.userNumber);
      this.distributorName.set(user.distributorName);
      this.form.patchValue({
        email: user.email,
        firstName: user.firstName,
        lastName: user.lastName,
        role: user.role,
      });
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const val = this.form.value;
      if (this.data.mode === 'create') {
        const tenantId = this.authService.currentUser()?.tenantId ?? '';
        await firstValueFrom(this.userApi.create({
          email: val.email,
          firstName: val.firstName,
          lastName: val.lastName,
          password: val.password,
          tenantId,
          role: val.role,
          distributorId: val.distributorId,
        }));
      } else {
        await firstValueFrom(this.userApi.update(this.data.userId!, {
          email: val.email,
          firstName: val.firstName,
          lastName: val.lastName,
          role: val.role,
        }));
      }
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
