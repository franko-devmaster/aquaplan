import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { DistributorDatastore } from '../../datastore/distributor.datastore';
import { DistributorApiService } from '../../services/distributor-api.service';
import { firstValueFrom } from 'rxjs';

export interface DistributorFormDialogData {
  mode: 'create' | 'edit';
  distributorId?: string;
}

@Component({
  selector: 'app-distributor-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.mode === 'create' ? 'distributors.createDistributor' : 'distributors.editDistributor') | translate }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'distributors.name' | translate }}</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'distributors.cantonRegion' | translate }}</mat-label>
          <input matInput formControlName="cantonRegion">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'distributors.distributionNetwork' | translate }}</mat-label>
          <input matInput formControlName="distributionNetwork">
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
export class DistributorFormDialogComponent implements OnInit {
  readonly data = inject<DistributorFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<DistributorFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly store = inject(DistributorDatastore);
  private readonly api = inject(DistributorApiService);

  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      cantonRegion: [''],
      distributionNetwork: [''],
    });
  }

  async ngOnInit(): Promise<void> {
    if (this.data.mode === 'edit' && this.data.distributorId) {
      const distributor = await firstValueFrom(this.api.getById(this.data.distributorId));
      this.form.patchValue({
        name: distributor.name,
        cantonRegion: distributor.cantonRegion,
        distributionNetwork: distributor.distributionNetwork,
      });
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const val = this.form.value;
      const dto = {
        name: val.name,
        cantonRegion: val.cantonRegion || null,
        distributionNetwork: val.distributionNetwork || null,
      };

      if (this.data.mode === 'create') {
        await this.store.create(dto);
      } else {
        await this.store.update(this.data.distributorId!, dto);
      }
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
