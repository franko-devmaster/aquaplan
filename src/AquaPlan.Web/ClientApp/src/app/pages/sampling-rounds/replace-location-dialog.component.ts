import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { SectorListDto } from '../../models/sector.model';
import { SamplingLocationDto } from '../../models/sampling-location.model';

export interface ReplaceLocationDialogData {
  orderId: string;
  distributorId: string;
}

@Component({
  selector: 'app-replace-location-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.replaceLocation' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="replace-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.sector' | translate }}</mat-label>
          <mat-select formControlName="sectorId" (selectionChange)="onSectorChange()">
            @for (sector of sectors(); track sector.id) {
              <mat-option [value]="sector.id">{{ sector.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.location' | translate }}</mat-label>
          <mat-select formControlName="newSamplingLocationId">
            @for (location of locations(); track location.id) {
              <mat-option [value]="location.id">{{ location.locationCode }} — {{ location.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.replacementReason' | translate }}</mat-label>
          <textarea matInput formControlName="reason" rows="3"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="saving() || form.invalid">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.save' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .replace-form { display: flex; flex-direction: column; min-width: 400px; }
    .full-width { width: 100%; }
  `],
})
export class ReplaceLocationDialogComponent implements OnInit {
  readonly data = inject<ReplaceLocationDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ReplaceLocationDialogComponent>);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);

  readonly saving = signal(false);
  readonly sectors = signal<SectorListDto[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);

  readonly form = new FormGroup({
    sectorId: new FormControl<string | null>(null, Validators.required),
    newSamplingLocationId: new FormControl<string | null>(null, Validators.required),
    reason: new FormControl<string>('', Validators.required),
  });

  async ngOnInit(): Promise<void> {
    const allSectors = await firstValueFrom(
      this.sectorApi.getAll({ distributorId: this.data.distributorId, isActive: true })
    );
    this.sectors.set(allSectors);
  }

  async onSectorChange(): Promise<void> {
    const sectorId = this.form.get('sectorId')!.value;
    this.form.get('newSamplingLocationId')!.reset();
    this.locations.set([]);

    if (sectorId) {
      const result = await firstValueFrom(
        this.locationApi.getFiltered({
          distributorId: this.data.distributorId,
          sectorId,
          isActive: true,
        })
      );
      this.locations.set(result.items ?? []);
    }
  }

  async save(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await firstValueFrom(this.roundApi.replaceLocation(this.data.orderId, {
        newSamplingLocationId: this.form.value.newSamplingLocationId!,
        reason: this.form.value.reason!,
      }));
      this.dialogRef.close(true);
    } catch {
      this.saving.set(false);
    }
  }
}
