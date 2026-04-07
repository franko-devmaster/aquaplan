import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { AnalysisProfileApiService } from '../../services/analysis-profile-api.service';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { AnalysisProfileListDto } from '../../models/analysis-profile.model';
import { firstValueFrom } from 'rxjs';

interface DistributorOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-order-create-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatCheckboxModule, MatDatepickerModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'orders.createOrder' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.distributor' | translate }}</mat-label>
          <mat-select formControlName="distributorId" (selectionChange)="onDistributorChange()">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

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

        <mat-checkbox formControlName="isUnplanned">
          {{ 'orders.isUnplanned' | translate }}
        </mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="onSubmit()"
              [disabled]="form.invalid || saving()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.create' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .form-container { display: flex; flex-direction: column; min-width: 400px; gap: 8px; }
    .full-width { width: 100%; }
  `],
})
export class OrderCreateDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderCreateDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly profileApi = inject(AnalysisProfileApiService);

  readonly distributors = signal<DistributorOption[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly analysisProfiles = signal<AnalysisProfileListDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      distributorId: ['', Validators.required],
      samplingLocationId: [null],
      plannedDate: [null],
      analysisProfileIds: [[]],
      notes: [''],
      isUnplanned: [false],
    });
  }

  async ngOnInit(): Promise<void> {
    // Load distributors from sampling locations
    const allLocations = await firstValueFrom(this.locationApi.getForCurrentUser());
    const uniqueDistributors = new Map<string, string>();
    for (const loc of allLocations) {
      if (!uniqueDistributors.has(loc.distributorId)) {
        uniqueDistributors.set(loc.distributorId, loc.distributorName ?? '');
      }
    }
    this.distributors.set(
      Array.from(uniqueDistributors, ([id, name]) => ({ id, name }))
    );

    // Load analysis profiles (active only)
    const profiles = await firstValueFrom(this.profileApi.getAll({ isActive: true }));
    this.analysisProfiles.set(profiles);
  }

  async onDistributorChange(): Promise<void> {
    const distributorId = this.form.value.distributorId;
    this.form.patchValue({ samplingLocationId: null });
    if (distributorId) {
      const locs = await firstValueFrom(this.locationApi.getByDistributor(distributorId));
      this.locations.set(locs.filter(l => l.isActive));
    } else {
      this.locations.set([]);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const formValue = this.form.value;
      await this.orderStore.create({
        distributorId: formValue.distributorId,
        samplingLocationId: formValue.samplingLocationId || null,
        preleveurId: null,
        plannedDate: formValue.plannedDate ? new Date(formValue.plannedDate).toISOString() : null,
        analysisProfileIds: formValue.analysisProfileIds?.length > 0 ? formValue.analysisProfileIds : null,
        notes: formValue.notes || null,
        isUnplanned: formValue.isUnplanned,
      });
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
