import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { SectorApiService } from '../../services/sector-api.service';
import { AnalysisProgramApiService } from '../../services/analysis-program-api.service';
import { SamplingLocationDto } from '../../models/sampling-location.model';
import { SectorListDto } from '../../models/sector.model';
import { AnalysisProgramListDto } from '../../models/analysis-program.model';
import { OrderDetailDto } from '../../models/order.model';
import { OrderLinkRoundDialogComponent } from './order-link-round-dialog.component';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-order-edit-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatDatepickerModule,
    MatProgressSpinnerModule, MatIconModule, TranslateModule,
  ],
  providers: [provideNativeDateAdapter()],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'orders.editOrder' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.sector' | translate }}</mat-label>
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
          <textarea matInput formControlName="notes" rows="3"></textarea>
        </mat-form-field>

        <div class="round-section">
          <label class="section-label">{{ 'orders.round' | translate }}</label>
          @if (roundName()) {
            <div class="round-info">
              <mat-icon>route</mat-icon>
              <span>{{ roundName() }}</span>
            </div>
          } @else {
            <button mat-stroked-button type="button" (click)="openLinkRoundDialog()">
              <mat-icon>add</mat-icon>
              {{ 'orders.linkToRound' | translate }}
            </button>
          }
        </div>
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
    .round-section { border: 1px solid #e0e0e0; border-radius: 8px; padding: 12px; }
    .section-label { font-size: 12px; font-weight: 500; color: #666; text-transform: uppercase; margin-bottom: 8px; display: block; }
    .round-info { display: flex; align-items: center; gap: 8px; color: #1976D2; }
  `],
})
export class OrderEditDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderEditDialogComponent>);
  private readonly data: OrderDetailDto = inject(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);
  private readonly sectorApi = inject(SectorApiService);
  private readonly programApi = inject(AnalysisProgramApiService);

  private readonly dialog = inject(MatDialog);

  readonly sectors = signal<SectorListDto[]>([]);
  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly filteredLocations = signal<SamplingLocationDto[]>([]);
  readonly analysisPrograms = signal<AnalysisProgramListDto[]>([]);
  readonly saving = signal(false);
  readonly roundName = signal<string | null>(null);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      sectorId: [null],
      samplingLocationId: [null],
      preleveurId: [null],
      plannedDate: [null],
      analysisProgramIds: [[]],
      notes: [''],
    });
  }

  async ngOnInit(): Promise<void> {
    this.form.patchValue({
      samplingLocationId: this.data.samplingLocationId,
      preleveurId: this.data.preleveurId,
      plannedDate: this.data.plannedDate ? new Date(this.data.plannedDate) : null,
      analysisProgramIds: this.data.analysisPrograms.map(p => p.analysisProgramId),
      notes: this.data.notes ?? '',
    });

    const [allSectors, locs, programs] = await Promise.all([
      firstValueFrom(this.sectorApi.getAll({ distributorId: this.data.distributorId, isActive: true })),
      firstValueFrom(this.locationApi.getByDistributor(this.data.distributorId)),
      firstValueFrom(this.programApi.getAll({ isActive: true })),
    ]);

    this.sectors.set(allSectors);
    this.analysisPrograms.set(programs);

    const activeLocs = locs.filter(l => l.isActive && l.isValidated);
    this.locations.set(activeLocs);

    if (this.data.samplingLocationId) {
      const currentLoc = activeLocs.find(l => l.id === this.data.samplingLocationId);
      if (currentLoc) {
        this.form.patchValue({ sectorId: currentLoc.sectorId });
        this.filteredLocations.set(activeLocs.filter(l => l.sectorId === currentLoc.sectorId));
      } else {
        this.filteredLocations.set(activeLocs);
      }
    } else {
      this.filteredLocations.set(activeLocs);
    }
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

  openLinkRoundDialog(): void {
    const dialogRef = this.dialog.open(OrderLinkRoundDialogComponent, {
      width: '500px',
      data: { orderId: this.data.id, distributorId: this.data.distributorId },
    });
    dialogRef.afterClosed().subscribe((result: unknown) => {
      if (result) {
        const round = result as { name: string };
        this.roundName.set(round.name);
      }
    });
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
        analysisProgramIds: formValue.analysisProgramIds?.length > 0 ? formValue.analysisProgramIds : null,
        notes: formValue.notes || null,
      });
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
