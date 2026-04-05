import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { AnalysisProfileApiService } from '../../services/analysis-profile-api.service';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { AnalysisProfileListDto } from '../../models/analysis-profile.model';
import { OrderDetailDto } from '../../models/order.model';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-order-edit-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatDatepickerModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'orders.editOrder' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.samplingLocation' | translate }}</mat-label>
          <mat-select formControlName="samplingLocationId">
            <mat-option [value]="null">-</mat-option>
            @for (loc of locations(); track loc.id) {
              <mat-option [value]="loc.id">{{ loc.name }} ({{ loc.locationCode }})</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.plannedDate' | translate }}</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="plannedDate">
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.analysisProfiles' | translate }}</mat-label>
          <mat-select formControlName="analysisProfileIds" multiple>
            @for (profile of analysisProfiles(); track profile.id) {
              <mat-option [value]="profile.id">{{ profile.code }} — {{ profile.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.notes' | translate }}</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
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
    .form-container { display: flex; flex-direction: column; min-width: 400px; gap: 8px; }
    .full-width { width: 100%; }
  `],
})
export class OrderEditDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderEditDialogComponent>);
  private readonly data: OrderDetailDto = inject(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly profileApi = inject(AnalysisProfileApiService);

  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly analysisProfiles = signal<AnalysisProfileListDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      samplingLocationId: [null],
      preleveurId: [null],
      plannedDate: [null],
      analysisProfileIds: [[]],
      notes: [''],
    });
  }

  async ngOnInit(): Promise<void> {
    // Pre-fill form with current order data
    this.form.patchValue({
      samplingLocationId: this.data.samplingLocationId,
      preleveurId: this.data.preleveurId,
      plannedDate: this.data.plannedDate ? new Date(this.data.plannedDate) : null,
      analysisProfileIds: this.data.analysisProfiles.map(p => p.analysisProfileId),
      notes: this.data.notes ?? '',
    });

    // Load locations for the order's distributor
    const locs = await firstValueFrom(this.locationApi.getByDistributor(this.data.distributorId));
    this.locations.set(locs.filter(l => l.isActive));

    // Load analysis profiles (active only)
    const profiles = await firstValueFrom(this.profileApi.getAll({ isActive: true }));
    this.analysisProfiles.set(profiles);
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const formValue = this.form.value;
      await this.orderStore.update(this.data.id, {
        samplingLocationId: formValue.samplingLocationId || null,
        preleveurId: formValue.preleveurId || null,
        plannedDate: formValue.plannedDate ? new Date(formValue.plannedDate).toISOString() : null,
        analysisProfileIds: formValue.analysisProfileIds?.length > 0 ? formValue.analysisProfileIds : null,
        notes: formValue.notes || null,
      });
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
