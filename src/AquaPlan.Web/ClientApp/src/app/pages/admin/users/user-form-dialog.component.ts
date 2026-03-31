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
import { RoleDto } from '../../../models/role.model';
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
        @if (data.mode === 'create') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'users.email' | translate }}</mat-label>
            <input matInput formControlName="email" type="email">
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.firstName' | translate }}</mat-label>
          <input matInput formControlName="firstName">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.lastName' | translate }}</mat-label>
          <input matInput formControlName="lastName">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.organization' | translate }}</mat-label>
          <input matInput formControlName="organization">
        </mat-form-field>

        @if (data.mode === 'create') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'users.password' | translate }}</mat-label>
            <input matInput formControlName="password" type="password">
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'users.roles' | translate }}</mat-label>
          <mat-select formControlName="roles" multiple>
            @for (role of roles(); track role.id) {
              <mat-option [value]="role.name">{{ role.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="onSubmit()"
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
  `],
})
export class UserFormDialogComponent implements OnInit {
  readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly userApi = inject(UserApiService);
  private readonly roleApi = inject(RoleApiService);
  private readonly authService = inject(AuthService);

  readonly roles = signal<RoleDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      organization: [''],
      password: ['', this.data.mode === 'create' ? Validators.required : []],
      roles: [[] as string[]],
    });
  }

  async ngOnInit(): Promise<void> {
    const allRoles = await firstValueFrom(this.roleApi.getAll());
    this.roles.set(allRoles);

    if (this.data.mode === 'edit' && this.data.userId) {
      const user = await firstValueFrom(this.userApi.getById(this.data.userId));
      this.form.patchValue({
        firstName: user.firstName,
        lastName: user.lastName,
        organization: user.organization,
        roles: user.roles,
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
          organization: val.organization || null,
          password: val.password,
          tenantId,
          roles: val.roles,
          distributorIds: [],
        }));
      } else {
        await firstValueFrom(this.userApi.update(this.data.userId!, {
          firstName: val.firstName,
          lastName: val.lastName,
          organization: val.organization || null,
          roles: val.roles,
          distributorIds: [],
        }));
      }
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
