import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingApiService } from '../../services/sampling-api.service';
import { SamplingDto, SamplingCreateDto, WEATHER_OPTIONS, WeatherOption } from '../../models/sampling.model';

export interface SamplingFormDialogData {
  orderId: string;
  sampling: SamplingDto | null;
  orderNumber: string;
  locationName: string;
}

@Component({
  selector: 'app-sampling-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatIconModule, MatCheckboxModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'sampling.title' | translate }} — {{ data.orderNumber }}</h2>
    <p class="location-subtitle">{{ data.locationName }}</p>

    <mat-dialog-content>
      <form [formGroup]="form" class="sampling-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sampling.samplingDateTime' | translate }}</mat-label>
          <input matInput type="datetime-local" formControlName="samplingDateTime">
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline" class="half-width">
            <mat-label>{{ 'sampling.temperature' | translate }}</mat-label>
            <input matInput type="number" formControlName="temperature" step="0.1">
            <span matSuffix>&deg;C</span>
          </mat-form-field>

          <mat-form-field appearance="outline" class="half-width">
            <mat-label>{{ 'sampling.weather3days' | translate }}</mat-label>
            <mat-select formControlName="weather">
              @for (option of weatherOptions; track option) {
                <mat-option [value]="option">{{ 'sampling.weatherOptions.' + option | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sampling.sampleBarcode' | translate }}</mat-label>
          <input matInput formControlName="sampleBarcode">
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sampling.notes' | translate }}</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
        </mat-form-field>

        <div class="checkbox-row">
          <mat-checkbox formControlName="isChlorinated">
            {{ 'sampling.isChlorinated' | translate }}
          </mat-checkbox>
          <mat-checkbox formControlName="hasWaterSoftener">
            {{ 'sampling.hasWaterSoftener' | translate }}
          </mat-checkbox>
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="saving()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          <mat-icon>save</mat-icon>
          {{ 'common.save' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .location-subtitle { margin: -8px 24px 8px; color: #666; font-size: 14px; }
    .sampling-form { display: flex; flex-direction: column; min-width: 400px; }
    .row { display: flex; gap: 16px; }
    .half-width { flex: 1; }
    .full-width { width: 100%; }
    .checkbox-row { display: flex; gap: 24px; margin: 8px 0 16px; }
    mat-dialog-content { max-height: 70vh; }
  `],
})
export class SamplingFormDialogComponent implements OnInit {
  readonly data = inject<SamplingFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SamplingFormDialogComponent>);
  private readonly samplingApi = inject(SamplingApiService);

  readonly saving = signal(false);
  readonly isEditMode = signal(false);
  readonly weatherOptions: readonly WeatherOption[] = WEATHER_OPTIONS;

  readonly form = new FormGroup({
    samplingDateTime: new FormControl<string>(''),
    temperature: new FormControl<number | null>(null, Validators.required),
    weather: new FormControl<string | null>(null, Validators.required),
    sampleBarcode: new FormControl<string>('', Validators.required),
    notes: new FormControl<string | null>(null),
    hasWaterSoftener: new FormControl<boolean>(false),
    isChlorinated: new FormControl<boolean>(false),
  });

  ngOnInit(): void {
    const sampling = this.data.sampling;
    if (sampling) {
      this.isEditMode.set(true);
      this.form.patchValue({
        samplingDateTime: this.toDatetimeLocalValue(sampling.samplingDateTime),
        temperature: sampling.temperature,
        weather: sampling.weather,
        sampleBarcode: sampling.sampleBarcode ?? '',
        notes: sampling.notes,
        hasWaterSoftener: sampling.hasWaterSoftener ?? false,
        isChlorinated: sampling.isChlorinated,
      });
    } else {
      // Default to current date/time
      this.form.patchValue({
        samplingDateTime: this.toDatetimeLocalValue(new Date().toISOString()),
      });
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      const formValue = this.form.getRawValue();
      const dto: SamplingCreateDto = {
        orderId: this.data.orderId,
        samplingDateTime: formValue.samplingDateTime
          ? new Date(formValue.samplingDateTime).toISOString()
          : new Date().toISOString(),
        temperature: formValue.temperature,
        weather: formValue.weather,
        notes: formValue.notes,
        hasWaterSoftener: formValue.hasWaterSoftener || null,
        isChlorinated: formValue.isChlorinated ?? false,
        sampleBarcode: formValue.sampleBarcode || null,
      };

      let result: SamplingDto;
      if (this.isEditMode()) {
        result = await firstValueFrom(this.samplingApi.update(this.data.orderId, dto));
      } else {
        result = await firstValueFrom(this.samplingApi.create(this.data.orderId, dto));
      }
      this.dialogRef.close(result);
    } catch {
      // Error handled by global error handler
      this.saving.set(false);
    }
  }

  private toDatetimeLocalValue(isoString: string): string {
    const date = new Date(isoString);
    const pad = (n: number): string => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }
}
