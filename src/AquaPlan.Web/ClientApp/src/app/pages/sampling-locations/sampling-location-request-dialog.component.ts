import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ChangeRequestDatastore } from '../../datastore/change-request.datastore';
import { DistributorDatastore } from '../../datastore/distributor.datastore';

export interface SamplingLocationRequestDialogData {
  mode: 'create' | 'update';
  samplingLocationId?: string;
  name?: string;
  locationCode?: string;
  latitude?: number | null;
  longitude?: number | null;
  description?: string | null;
  distributorId?: string;
}

@Component({
  selector: 'app-sampling-location-request-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatProgressSpinnerModule,
    FormsModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.mode === 'create' ? 'changeRequests.create' : 'changeRequests.update') | translate }}
    </h2>
    <mat-dialog-content>
      @if (data.mode === 'create') {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.distributor' | translate }}</mat-label>
          <mat-select [(ngModel)]="distributorId" required>
            @for (d of distributorStore.distributors(); track d.id) {
              <mat-option [value]="d.id">{{ d.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingLocations.name' | translate }}</mat-label>
        <input matInput [(ngModel)]="name" required maxlength="200">
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingLocations.locationCode' | translate }}</mat-label>
        <input matInput [(ngModel)]="locationCode" required maxlength="50">
      </mat-form-field>

      <div class="coordinate-row">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'samplingLocations.latitude' | translate }}</mat-label>
          <input matInput type="number" [(ngModel)]="latitude">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'samplingLocations.longitude' | translate }}</mat-label>
          <input matInput type="number" [(ngModel)]="longitude">
        </mat-form-field>
      </div>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingLocations.description' | translate }}</mat-label>
        <textarea matInput [(ngModel)]="description" rows="3" maxlength="1000"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary"
              [disabled]="!isValid() || submitting()"
              (click)="submit()">
        @if (submitting()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'changeRequests.submitRequest' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    .coordinate-row { display: flex; gap: 16px; }
    .coordinate-row mat-form-field { flex: 1; }
  `],
})
export class SamplingLocationRequestDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<SamplingLocationRequestDialogComponent>);
  private readonly changeRequestStore = inject(ChangeRequestDatastore);
  readonly distributorStore = inject(DistributorDatastore);
  readonly data: SamplingLocationRequestDialogData = inject(MAT_DIALOG_DATA);

  readonly submitting = signal(false);

  name = this.data.name ?? '';
  locationCode = this.data.locationCode ?? '';
  latitude = this.data.latitude ?? null;
  longitude = this.data.longitude ?? null;
  description = this.data.description ?? '';
  distributorId = this.data.distributorId ?? '';

  constructor() {
    if (this.data.mode === 'create') {
      this.distributorStore.loadAll();
    }
  }

  isValid(): boolean {
    const hasName = this.name.trim().length > 0;
    const hasCode = this.locationCode.trim().length > 0;
    if (this.data.mode === 'create') {
      return hasName && hasCode && this.distributorId.length > 0;
    }
    return hasName && hasCode;
  }

  async submit(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    this.submitting.set(true);
    try {
      if (this.data.mode === 'create') {
        await this.changeRequestStore.submitCreate({
          name: this.name.trim(),
          locationCode: this.locationCode.trim(),
          latitude: this.latitude,
          longitude: this.longitude,
          description: this.description?.trim() || null,
          distributorId: this.distributorId,
        });
      } else {
        await this.changeRequestStore.submitUpdate(this.data.samplingLocationId!, {
          name: this.name.trim(),
          locationCode: this.locationCode.trim(),
          latitude: this.latitude,
          longitude: this.longitude,
          description: this.description?.trim() || null,
        });
      }
      this.dialogRef.close(true);
    } finally {
      this.submitting.set(false);
    }
  }
}
