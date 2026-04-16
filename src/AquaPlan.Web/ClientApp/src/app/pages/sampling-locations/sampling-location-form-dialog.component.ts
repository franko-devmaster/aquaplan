import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { SamplingLocationDatastore, DistributorOption } from '../../datastore/sampling-location.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { SectorDto } from '../../models/sector.model';
import { ConfirmDialogComponent } from '../../components/confirm-dialog.component';
import { firstValueFrom } from 'rxjs';

export interface SamplingLocationFormDialogData {
  mode: 'create' | 'edit';
  locationId?: string;
  readonly: boolean;
  userDistributorId: string | null;
  isAdmin: boolean;
  showValidateButton?: boolean;
}

@Component({
  selector: 'app-sampling-location-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatSelectModule, MatProgressSpinnerModule, MatSnackBarModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.readonly ? 'samplingLocations.viewLocation' : (data.mode === 'create' ? 'samplingLocations.createLocation' : 'samplingLocations.editLocation')) | translate }}
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
      <button mat-button mat-dialog-close>{{ (data.readonly ? 'common.close' : 'common.cancel') | translate }}</button>
      @if (canShowDelete()) {
        <button mat-raised-button color="warn" (click)="onDelete()" [disabled]="saving()">
          @if (saving()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            {{ 'samplingLocations.form.actions.delete' | translate }}
          }
        </button>
      }
      @if (canShowValidate()) {
        <button mat-raised-button color="accent" (click)="onValidate()" [disabled]="saving()">
          @if (saving()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            {{ 'samplingLocations.form.actions.validate' | translate }}
          }
        </button>
      }
      @if (!data.readonly) {
        <button mat-raised-button color="primary" (click)="onSubmit()"
                [disabled]="form.invalid || saving()">
          @if (saving()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            {{ 'samplingLocations.form.actions.save' | translate }}
          }
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: [`
    .form-container { display: flex; flex-direction: column; min-width: 450px; }
    .full-width { width: 100%; }
  `],
})
export class SamplingLocationFormDialogComponent implements OnInit {
  readonly data = inject<SamplingLocationFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SamplingLocationFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly store = inject(SamplingLocationDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  readonly distributors = signal<DistributorOption[]>([]);
  readonly sectors = signal<SectorDto[]>([]);
  readonly saving = signal(false);
  readonly isValidated = signal(true);
  readonly canDelete = signal(false);
  readonly form: FormGroup;

  canShowValidate(): boolean {
    return !this.data.readonly && this.data.mode === 'edit' && this.data.isAdmin && !this.isValidated();
  }

  canShowDelete(): boolean {
    return !this.data.readonly && this.data.mode === 'edit' && this.data.isAdmin && this.canDelete();
  }

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      locationCode: ['', [Validators.required, Validators.maxLength(50)]],
      distributorId: ['', Validators.required],
      description: ['', Validators.maxLength(1000)],
      address: ['', Validators.maxLength(500)],
      accessDescription: ['', Validators.maxLength(1000)],
      sectorId: ['', Validators.required],
    });

    // Disable distributor selection for edit mode or non-admin create (auto-assigned)
    if (this.data.mode === 'edit' || (!this.data.isAdmin && this.data.userDistributorId)) {
      this.form.get('distributorId')!.disable();
    }

    // Disable entire form for readonly mode
    if (this.data.readonly) {
      this.form.disable();
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

    if (this.data.mode === 'create' && !this.data.isAdmin && this.data.userDistributorId) {
      // Non-admin creation: auto-set distributor and load sectors for their distributor
      this.form.patchValue({ distributorId: this.data.userDistributorId });
      await this.loadSectorsForDistributor(this.data.userDistributorId);
    }

    if (this.data.mode === 'edit' && this.data.locationId) {
      const location = await firstValueFrom(this.locationApi.getById(this.data.locationId));
      // Load sectors for the location's distributor before patching
      await this.loadSectorsForDistributor(location.distributorId);
      this.isValidated.set(location.isValidated);
      this.canDelete.set(location.canDelete ?? false);
      this.form.patchValue({
        name: location.name,
        locationCode: location.locationCode,
        distributorId: location.distributorId,
        description: location.description,
        address: location.address ?? '',
        accessDescription: location.accessDescription ?? '',
        sectorId: location.sectorId ?? '',
      });
    }
  }

  async onDelete(): Promise<void> {
    if (!this.data.locationId) return;
    const confirmed = await firstValueFrom(
      this.dialog
        .open(ConfirmDialogComponent, {
          width: '400px',
          data: {
            title: this.translate.instant('samplingLocations.form.actions.delete'),
            message: this.translate.instant('samplingLocations.confirmDelete'),
          },
        })
        .afterClosed(),
    );
    if (!confirmed) return;

    this.saving.set(true);
    try {
      await firstValueFrom(this.locationApi.delete(this.data.locationId));
      this.snackBar.open(
        this.translate.instant('samplingLocations.deleteSuccess'),
        this.translate.instant('common.close'),
        { duration: 3000 },
      );
      this.dialogRef.close(true);
    } catch (err: unknown) {
      const apiError = err as { error?: { message?: string } };
      this.snackBar.open(
        apiError?.error?.message ?? this.translate.instant('common.error'),
        this.translate.instant('common.close'),
        { duration: 5000 },
      );
      this.saving.set(false);
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

  async onValidate(): Promise<void> {
    if (!this.data.locationId) return;
    this.saving.set(true);
    try {
      // Save changes first if form is dirty and valid
      if (this.form.dirty && this.form.valid) {
        const val = this.form.getRawValue();
        await this.store.update(this.data.locationId, {
          name: val.name,
          locationCode: val.locationCode,
          description: val.description || null,
          address: val.address || null,
          accessDescription: val.accessDescription || null,
          isActive: true,
          sectorId: val.sectorId || null,
        });
      }
      await firstValueFrom(this.locationApi.validate(this.data.locationId));
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
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
          description: val.description || null,
          address: val.address || null,
          accessDescription: val.accessDescription || null,
          sectorId: val.sectorId || null,
        });
      } else {
        await this.store.update(this.data.locationId!, {
          name: val.name,
          locationCode: val.locationCode,
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
