import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { SamplingLocationDatastore, DistributorOption } from '../../datastore/sampling-location.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { SectorDto } from '../../models/sector.model';
import { firstValueFrom } from 'rxjs';

export interface SamplingLocationFormDialogData {
  mode: 'create' | 'edit';
  locationId?: string;
}

@Component({
  selector: 'app-sampling-location-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatSelectModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.mode === 'create' ? 'samplingLocations.createLocation' : 'samplingLocations.editLocation') | translate }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.name' | translate }}</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.locationCode' | translate }}</mat-label>
          <input matInput formControlName="locationCode">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.distributor' | translate }}</mat-label>
          <mat-select formControlName="distributorId">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.sector' | translate }}</mat-label>
          <mat-select formControlName="sectorId">
            @for (sector of sectors(); track sector.id) {
              <mat-option [value]="sector.id">{{ sector.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <div class="coordinates-row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'samplingLocations.latitude' | translate }}</mat-label>
            <input matInput formControlName="latitude" type="number" step="0.0001">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'samplingLocations.longitude' | translate }}</mat-label>
            <input matInput formControlName="longitude" type="number" step="0.0001">
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.description' | translate }}</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.address' | translate }}</mat-label>
          <input matInput formControlName="address">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingLocations.accessDescription' | translate }}</mat-label>
          <textarea matInput formControlName="accessDescription" rows="2"></textarea>
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
    .form-container { display: flex; flex-direction: column; min-width: 450px; }
    .full-width { width: 100%; }
    .coordinates-row { display: flex; gap: 16px; }
    .coordinates-row mat-form-field { flex: 1; }
  `],
})
export class SamplingLocationFormDialogComponent implements OnInit {
  readonly data = inject<SamplingLocationFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SamplingLocationFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly store = inject(SamplingLocationDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);

  readonly distributors = signal<DistributorOption[]>([]);
  readonly sectors = signal<SectorDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      locationCode: ['', [Validators.required, Validators.maxLength(50)]],
      distributorId: ['', Validators.required],
      latitude: [null as number | null],
      longitude: [null as number | null],
      description: ['', Validators.maxLength(1000)],
      address: ['', Validators.maxLength(500)],
      accessDescription: ['', Validators.maxLength(1000)],
      sectorId: ['', Validators.required],
    });

    if (this.data.mode === 'edit') {
      this.form.get('distributorId')!.disable();
    }
  }

  async ngOnInit(): Promise<void> {
    await this.store.loadAll();
    this.distributors.set(this.store.distributors());

    // Listen for distributor changes to filter sectors
    this.form.get('distributorId')!.valueChanges.subscribe((distributorId: string) => {
      this.loadSectorsForDistributor(distributorId);
      // Reset sector selection when distributor changes
      this.form.get('sectorId')!.setValue('');
    });

    if (this.data.mode === 'edit' && this.data.locationId) {
      const location = await firstValueFrom(this.locationApi.getById(this.data.locationId));
      // Load sectors for the location's distributor before patching
      await this.loadSectorsForDistributor(location.distributorId);
      this.form.patchValue({
        name: location.name,
        locationCode: location.locationCode,
        distributorId: location.distributorId,
        latitude: location.latitude,
        longitude: location.longitude,
        description: location.description,
        address: location.address ?? '',
        accessDescription: location.accessDescription ?? '',
        sectorId: location.sectorId ?? '',
      });
    }
  }

  private async loadSectorsForDistributor(distributorId: string): Promise<void> {
    if (!distributorId) {
      this.sectors.set([]);
      return;
    }
    const sectorList = await firstValueFrom(this.sectorApi.getAll({ isActive: true, distributorId }));
    this.sectors.set(Array.isArray(sectorList) ? sectorList as unknown as SectorDto[] : []);
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const val = this.form.getRawValue();
      if (this.data.mode === 'create') {
        await this.store.create({
          name: val.name,
          locationCode: val.locationCode,
          distributorId: val.distributorId,
          latitude: val.latitude || null,
          longitude: val.longitude || null,
          description: val.description || null,
          address: val.address || null,
          accessDescription: val.accessDescription || null,
          sectorId: val.sectorId || null,
        });
      } else {
        await this.store.update(this.data.locationId!, {
          name: val.name,
          locationCode: val.locationCode,
          latitude: val.latitude || null,
          longitude: val.longitude || null,
          description: val.description || null,
          address: val.address || null,
          accessDescription: val.accessDescription || null,
          isActive: true,
          sectorId: val.sectorId || null,
        });
      }
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
