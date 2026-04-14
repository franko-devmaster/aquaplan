import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../../services/order-api.service';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { AnalysisProfileApiService } from '../../services/analysis-profile-api.service';
import { OrderListDto, OrderStatus, UnplannedReason, UnplannedReasonLabels } from '../../models/order.model';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { SectorListDto } from '../../models/sector.model';
import { AnalysisProfileListDto } from '../../models/analysis-profile.model';
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
    MatSelectModule, MatButtonModule, MatCheckboxModule, MatRadioModule,
    MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.addOrderTitle' | translate }}</h2>
    <mat-dialog-content>
      <mat-radio-group [value]="mode()" (change)="mode.set($event.value)" class="mode-group">
        <mat-radio-button value="link">{{ 'samplingRounds.linkExistingOrder' | translate }}</mat-radio-button>
        <mat-radio-button value="create">{{ 'samplingRounds.createNewOrder' | translate }}</mat-radio-button>
      </mat-radio-group>

      @if (mode() === 'link') {
        <div class="link-section">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'samplingRounds.selectOrder' | translate }}</mat-label>
            <mat-select [(value)]="selectedOrderId">
              @for (order of unassignedOrders(); track order.id) {
                <mat-option [value]="order.id">
                  {{ order.orderNumber }} — {{ order.samplingLocationName ?? order.distributorName }}
                </mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (unassignedOrders().length === 0 && !loadingOrders()) {
            <p class="info-text">{{ 'samplingRounds.noUnassignedOrders' | translate }}</p>
          }
        </div>
      }

      @if (mode() === 'create') {
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
            <mat-label>{{ 'orders.analysisProfiles' | translate }}</mat-label>
            <mat-select formControlName="analysisProfileIds" multiple>
              @for (profile of analysisProfiles(); track profile.id) {
                <mat-option [value]="profile.id">{{ profile.code }} — {{ profile.name }}</mat-option>
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
      }

      @if (error()) {
        <p class="error-text">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="onSubmit()"
              [disabled]="saving() || !canSubmit()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ (mode() === 'link' ? 'samplingRounds.addOrder' : 'common.create') | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .mode-group { display: flex; gap: 16px; margin-bottom: 16px; }
    .form-container { display: flex; flex-direction: column; min-width: 450px; gap: 8px; }
    .link-section { min-width: 450px; }
    .full-width { width: 100%; }
    .info-text { color: #666; font-size: 13px; font-style: italic; }
    .error-text { color: #f44336; font-size: 13px; margin-top: 8px; }
  `],
})
export class RoundAddOrderDialogComponent implements OnInit {
  readonly data = inject<RoundAddOrderDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<RoundAddOrderDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly orderApi = inject(OrderApiService);
  private readonly orderStore = inject(OrderDatastore);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);
  private readonly profileApi = inject(AnalysisProfileApiService);

  readonly mode = signal<'link' | 'create'>('link');
  readonly unassignedOrders = signal<OrderListDto[]>([]);
  readonly sectors = signal<SectorListDto[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly filteredLocations = signal<SamplingLocationDto[]>([]);
  readonly analysisProfiles = signal<AnalysisProfileListDto[]>([]);
  readonly loadingOrders = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');

  selectedOrderId: string | null = null;

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
      analysisProfileIds: [[]],
      notes: [''],
      isUnplanned: [false],
      unplannedReason: [null],
    });
  }

  canSubmit(): boolean {
    if (this.mode() === 'link') {
      return !!this.selectedOrderId;
    }
    return true; // create mode — always submittable
  }

  async ngOnInit(): Promise<void> {
    // Load unassigned orders for this distributor
    this.loadingOrders.set(true);
    try {
      const result = await firstValueFrom(this.orderApi.getFiltered({
        distributorId: this.data.distributorId,
        hasNoRound: true,
        statuses: [OrderStatus.New],
        pageSize: 200,
      }));
      this.unassignedOrders.set(result.items);
    } finally {
      this.loadingOrders.set(false);
    }

    // Load sectors, locations and profiles for create mode
    const allSectors = await firstValueFrom(
      this.sectorApi.getAll({ distributorId: this.data.distributorId, isActive: true })
    );
    this.sectors.set(allSectors);

    const locs = await firstValueFrom(this.locationApi.getByDistributor(this.data.distributorId));
    const activeLocs = locs.filter(l => l.isActive && l.isValidated);
    this.locations.set(activeLocs);
    this.filteredLocations.set(activeLocs);

    const profiles = await firstValueFrom(this.profileApi.getAll({ isActive: true }));
    this.analysisProfiles.set(profiles);
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
      let updatedRound: SamplingRoundDetailDto;

      if (this.mode() === 'link' && this.selectedOrderId) {
        updatedRound = await firstValueFrom(
          this.roundApi.addOrder(this.data.roundId, this.selectedOrderId)
        );
      } else {
        // Create new order then link to round
        const val = this.form.value;
        const order = await this.orderStore.create({
          distributorId: this.data.distributorId,
          samplingLocationId: val.samplingLocationId || null,
          preleveurId: null,
          plannedDate: null,
          analysisProfileIds: val.analysisProfileIds?.length > 0 ? val.analysisProfileIds : null,
          notes: val.notes || null,
          isUnplanned: val.isUnplanned,
          unplannedReason: val.isUnplanned ? val.unplannedReason : null,
        });
        updatedRound = await firstValueFrom(
          this.roundApi.addOrder(this.data.roundId, order.id)
        );
      }

      this.dialogRef.close(updatedRound);
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.error.set(apiError?.error?.error ?? 'An error occurred');
    } finally {
      this.saving.set(false);
    }
  }
}
