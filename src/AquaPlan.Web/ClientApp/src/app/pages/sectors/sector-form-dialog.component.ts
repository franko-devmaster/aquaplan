import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { SectorDatastore } from '../../datastore/sector.datastore';
import { SectorApiService } from '../../services/sector-api.service';
import { DistributorApiService } from '../../services/distributor-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import { firstValueFrom } from 'rxjs';

export interface SectorFormDialogData {
  mode: 'create' | 'edit';
  sectorId?: string;
}

@Component({
  selector: 'app-sector-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatSelectModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.mode === 'create' ? 'sectors.addSector' : 'sectors.editSector') | translate }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sectors.name' | translate }}</mat-label>
          <input matInput formControlName="name">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sectors.code' | translate }}</mat-label>
          <input matInput formControlName="code">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sectors.distributor' | translate }}</mat-label>
          <mat-select formControlName="distributorId">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sectors.description' | translate }}</mat-label>
          <input matInput formControlName="description">
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
  `],
})
export class SectorFormDialogComponent implements OnInit {
  readonly data = inject<SectorFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SectorFormDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly store = inject(SectorDatastore);
  private readonly api = inject(SectorApiService);
  private readonly distributorApi = inject(DistributorApiService);

  readonly distributors = signal<DistributorListDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      code: ['', Validators.required],
      distributorId: ['', Validators.required],
      description: [''],
    });

    // distributorId is editable in both create and edit modes
  }

  async ngOnInit(): Promise<void> {
    const dists = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
    this.distributors.set(dists);

    if (this.data.mode === 'edit' && this.data.sectorId) {
      const sector = await firstValueFrom(this.api.getById(this.data.sectorId));
      this.form.patchValue({
        name: sector.name,
        code: sector.code,
        distributorId: sector.distributorId,
        description: sector.description,
      });
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
          code: val.code,
          description: val.description || null,
          distributorId: val.distributorId,
        });
      } else {
        await this.store.update(this.data.sectorId!, {
          name: val.name,
          code: val.code,
          description: val.description || null,
          distributorId: val.distributorId,
        });
      }
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
