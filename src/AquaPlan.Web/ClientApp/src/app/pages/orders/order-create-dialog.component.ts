import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { UnplannedReason, UnplannedReasonLabels } from '../../models/order.model';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { AnalysisProgramApiService } from '../../services/analysis-program-api.service';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { DelegationApiService } from '../../services/delegation-api.service';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { AnalysisProgramListDto } from '../../models/analysis-program.model';
import { SamplingRoundListDto, SamplingRoundStatus } from '../../models/sampling-round.model';
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
    MatSelectModule, MatButtonModule, MatCheckboxModule, MatRadioModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
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

        <div class="round-section">
          <label class="section-label">{{ 'orders.round' | translate }}</label>
          <mat-radio-group formControlName="roundMode" class="round-mode-group">
            <mat-radio-button value="none">{{ 'orders.noRound' | translate }}</mat-radio-button>
            <mat-radio-button value="existing">{{ 'orders.existingRound' | translate }}</mat-radio-button>
            <mat-radio-button value="new">{{ 'orders.newRound' | translate }}</mat-radio-button>
          </mat-radio-group>

          @if (form.value.roundMode === 'existing') {
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ 'orders.selectRound' | translate }}</mat-label>
              <mat-select formControlName="roundId">
                @for (round of availableRounds(); track round.id) {
                  <mat-option [value]="round.id">{{ round.name }} — {{ round.distributorName }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            @if (availableRounds().length === 0) {
              <p class="info-text">{{ 'orders.noRoundsAvailable' | translate }}</p>
            }
          }

          @if (form.value.roundMode === 'new') {
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ 'samplingRounds.name' | translate }}</mat-label>
              <input matInput formControlName="newRoundName">
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ 'samplingRounds.deadline' | translate }}</mat-label>
              <input matInput type="date" formControlName="newRoundDeadline">
            </mat-form-field>
          }
        </div>

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
          <textarea matInput formControlName="notes" rows="3"></textarea>
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

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'orders.unplannedReason.details' | translate }}</mat-label>
            <textarea matInput formControlName="unplannedReasonDetails" rows="2"></textarea>
          </mat-form-field>
        }
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
    .form-container { display: flex; flex-direction: column; min-width: 450px; gap: 8px; }
    .full-width { width: 100%; }
    .round-section { border: 1px solid #e0e0e0; border-radius: 8px; padding: 12px; margin-bottom: 8px; }
    .section-label { font-size: 12px; font-weight: 500; color: #666; text-transform: uppercase; margin-bottom: 8px; display: block; }
    .round-mode-group { display: flex; gap: 12px; margin-bottom: 12px; }
    .info-text { color: #666; font-size: 13px; font-style: italic; }
  `],
})
export class OrderCreateDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderCreateDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly programApi = inject(AnalysisProgramApiService);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly delegationApi = inject(DelegationApiService);

  readonly distributors = signal<DistributorOption[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly analysisPrograms = signal<AnalysisProgramListDto[]>([]);
  readonly availableRounds = signal<SamplingRoundListDto[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;
  readonly unplannedReasons = [
    { value: UnplannedReason.Pollution, label: UnplannedReasonLabels[UnplannedReason.Pollution] },
    { value: UnplannedReason.Urgency, label: UnplannedReasonLabels[UnplannedReason.Urgency] },
    { value: UnplannedReason.ComplementaryControl, label: UnplannedReasonLabels[UnplannedReason.ComplementaryControl] },
  ];

  constructor() {
    this.form = this.fb.group({
      distributorId: ['', Validators.required],
      samplingLocationId: [null],
      roundMode: ['none'],
      roundId: [null],
      newRoundName: [''],
      newRoundDeadline: [''],
      analysisProgramIds: [[], Validators.required],
      notes: [''],
      isUnplanned: [false],
      unplannedReason: [null],
      unplannedReasonDetails: [''],
    });
  }

  async ngOnInit(): Promise<void> {
    // AQ-369 — only distributors the user is authorized to create on
    const authorized = await firstValueFrom(this.delegationApi.getMyAuthorizedDistributors());
    this.distributors.set(authorized.map(d => ({ id: d.id, name: d.name })));

    if (authorized.length === 1) {
      this.form.patchValue({ distributorId: authorized[0].id });
      await this.onDistributorChange();
    }

    const programs = await firstValueFrom(this.programApi.getAll({ isActive: true }));
    this.analysisPrograms.set(programs);
  }

  async onDistributorChange(): Promise<void> {
    const distributorId = this.form.value.distributorId;
    this.form.patchValue({ samplingLocationId: null, roundId: null });
    if (distributorId) {
      const locs = await firstValueFrom(this.locationApi.getByDistributor(distributorId));
      this.locations.set(locs.filter(l => l.isActive));

      const result = await firstValueFrom(this.roundApi.getFiltered({
        statuses: [SamplingRoundStatus.Draft, SamplingRoundStatus.Assigned],
        pageSize: 100,
      }));
      this.availableRounds.set(result.items.filter(r => r.distributorId === distributorId));
    } else {
      this.locations.set([]);
      this.availableRounds.set([]);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      const val = this.form.value;

      const order = await this.orderStore.create({
        distributorId: val.distributorId,
        samplingLocationId: val.samplingLocationId || null,
        preleveurId: null,
        plannedDate: null,
        analysisProgramIds: val.analysisProgramIds,
        notes: val.notes || null,
        isUnplanned: val.isUnplanned,
        unplannedReason: val.isUnplanned ? val.unplannedReason : null,
        unplannedReasonDetails: val.isUnplanned ? (val.unplannedReasonDetails || null) : null,
      });

      if (val.roundMode === 'existing' && val.roundId) {
        await firstValueFrom(this.roundApi.addOrder(val.roundId, order.id));
      } else if (val.roundMode === 'new' && val.newRoundName) {
        const newRound = await firstValueFrom(this.roundApi.create({
          distributorId: val.distributorId,
          name: val.newRoundName,
          description: null,
          deadline: val.newRoundDeadline || new Date().toISOString().split('T')[0],
        }));
        await firstValueFrom(this.roundApi.addOrder(newRound.id, order.id));
      }

      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
