import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { AnalysisProgramApiService } from '../../services/analysis-program-api.service';
import { UnplannedReason, UnplannedReasonLabels } from '../../models/order.model';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { SectorListDto } from '../../models/sector.model';
import { AnalysisProgramListDto } from '../../models/analysis-program.model';
import { SamplingRoundDetailDto } from '../../models/sampling-round.model';

export interface RoundAddOrderDialogData {
  roundId: string;
  distributorId: string;
  distributorName: string;
}

@Component({
  selector: 'app-round-add-order-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatCheckboxModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.addOrderTitle' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.sector' | translate }}</mat-label>
          <mat-select formControlName="sectorId" (selectionChange)="onSectorChange()">
            <mat-option [value]="null">{{ 'common.all' | translate }}</mat-option>
            @for (sector of sectors(); track sector.id) {
              <mat-option [value]="sector.id">{{ sector.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.samplingLocation' | translate }}</mat-label>
          <mat-select formControlName="samplingLocationId">
            <mat-option [value]="null">-</mat-option>
            @for (loc of filteredLocations(); track loc.id) {
              <mat-option [value]="loc.id">{{ loc.locationCode }} — {{ loc.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.analysisPrograms' | translate }}</mat-label>
          <mat-select formControlName="analysisProgramIds" multiple>
            @for (program of analysisPrograms(); track program.id) {
              <mat-option [value]="program.id">{{ program.code }} — {{ program.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.notes' | translate }}</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>

        <mat-checkbox formControlName="isUnplanned">
          {{ 'orders.isUnplanned' | translate }}
        </mat-checkbox>

        @if (form.value.isUnplanned) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'orders.unplannedReason.label' | translate }}</mat-label>
            <mat-select formControlName="unplannedReason">
              @for (reason of unplannedReasons; track reason.value) {
                <mat-option [value]="reason.value">{{ reason.label | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </form>

      @if (error()) {
        <p class="error-text">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button color="primary" (click)="onSubmit()"
              [disabled]="saving()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.create' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .form-container { display: flex; flex-direction: column; min-width: 450px; gap: 8px; }
    .full-width { width: 100%; }
    .error-text { color: #f44336; font-size: 13px; margin-top: 8px; }
  `],
})
export class RoundAddOrderDialogComponent implements OnInit {
  readonly data = inject<RoundAddOrderDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RoundAddOrderDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);
  private readonly programApi = inject(AnalysisProgramApiService);

  readonly sectors = signal<SectorListDto[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly filteredLocations = signal<SamplingLocationDto[]>([]);
  readonly analysisPrograms = signal<AnalysisProgramListDto[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  readonly form: FormGroup;

  readonly unplannedReasons = [
    { value: UnplannedReason.Pollution, label: UnplannedReasonLabels[UnplannedReason.Pollution] },
    { value: UnplannedReason.Urgency, label: UnplannedReasonLabels[UnplannedReason.Urgency] },
    { value: UnplannedReason.ComplementaryControl, label: UnplannedReasonLabels[UnplannedReason.ComplementaryControl] },
  ];

  constructor() {
    this.form = this.fb.group({
      sectorId: [null],
      samplingLocationId: [null],
      analysisProgramIds: [[]],
      notes: [''],
      isUnplanned: [false],
      unplannedReason: [null],
    });
  }

  async ngOnInit(): Promise<void> {
    const [allSectors, locs, programs] = await Promise.all([
      firstValueFrom(this.sectorApi.getAll({ distributorId: this.data.distributorId, isActive: true })),
      firstValueFrom(this.locationApi.getByDistributor(this.data.distributorId)),
      firstValueFrom(this.programApi.getAll({ isActive: true })),
    ]);

    this.sectors.set(allSectors);
    this.analysisPrograms.set(programs);

    const activeLocs = locs.filter(l => l.isActive && l.isValidated);
    this.locations.set(activeLocs);
    this.filteredLocations.set(activeLocs);
  }

  onSectorChange(): void {
    const sectorId = this.form.get('sectorId')!.value;
    this.form.get('samplingLocationId')!.reset();
    if (sectorId) {
      this.filteredLocations.set(this.locations().filter(l => l.sectorId === sectorId));
    } else {
      this.filteredLocations.set(this.locations());
    }
  }

  async onSubmit(): Promise<void> {
    this.saving.set(true);
    this.error.set('');

    try {
      const val = this.form.value;
      const order = await this.orderStore.create({
        distributorId: this.data.distributorId,
        samplingLocationId: val.samplingLocationId || null,
        preleveurId: null,
        plannedDate: null,
        analysisProgramIds: val.analysisProgramIds?.length > 0 ? val.analysisProgramIds : null,
        notes: val.notes || null,
        isUnplanned: val.isUnplanned,
        unplannedReason: val.isUnplanned ? val.unplannedReason : null,
      });
      const updatedRound: SamplingRoundDetailDto = await firstValueFrom(
        this.roundApi.addOrder(this.data.roundId, order.id)
      );

      this.dialogRef.close(updatedRound);
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.error.set(apiError?.error?.error ?? 'An error occurred');
    } finally {
      this.saving.set(false);
    }
  }
}
