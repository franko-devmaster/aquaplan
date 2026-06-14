import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormGroup, FormControl, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingApiService } from '../../services/sampling-api.service';
import { OrderApiService } from '../../services/order-api.service';
import { SyncService } from '../../services/sync.service';
import { OfflineStorageService } from '../../services/offline-storage.service';
import { devInfo } from '../../utils/dev-log';
import {
  SamplingDto,
  SamplingCreateDto,
  SamplingContainerInputDto,
  RequiredContainerDto,
  WEATHER_OPTIONS,
  WeatherOption,
} from '../../models/sampling.model';
import { BarcodeScannerDialogComponent, BarcodeScanResult } from '../../components/barcode-scanner/barcode-scanner-dialog.component';

export interface SamplingFormDialogData {
  orderId: string;
  sampling: SamplingDto | null;
  orderNumber: string;
  locationName: string;
  /**
   * AQ-409 — roundId is needed so that an offline save can be queued against the
   * round's pending-actions store (SyncService / OfflineStorageService). Optional
   * for backwards-compatibility with callers that haven't wired it yet — in that
   * case an offline save falls back to using the orderId as a best-effort roundKey.
   */
  roundId?: string;
}

interface ContainerFormGroup {
  containerId: FormControl<string>;
  containerName: FormControl<string>;
  containerMaterial: FormControl<string>;
  containerVolumeMl: FormControl<number>;
  barcode: FormControl<string>;
}

@Component({
  selector: 'app-sampling-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatButtonModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatIconModule, MatCheckboxModule, MatProgressSpinnerModule,
    MatTooltipModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'sampling.title' | translate }} — {{ data.orderNumber }}</h2>
    <p class="location-subtitle">{{ data.locationName }}</p>

    <mat-dialog-content>
      <!-- AQ-403 — mobile-first form: single column by default, two columns only on tablet+. -->
      <form [formGroup]="form" class="sampling-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'sampling.samplingDateTime' | translate }}</mat-label>
          <input matInput type="datetime-local" formControlName="samplingDateTime">
        </mat-form-field>

        <!-- AQ-412 — renamed from .row/.col to .form-row/.form-col to avoid Bootstrap grid collisions. -->
        <div class="form-row">
          <mat-form-field appearance="outline" class="form-col">
            <mat-label>{{ 'sampling.temperature' | translate }}</mat-label>
            <input matInput type="number" formControlName="temperature" step="0.1" inputmode="decimal">
            <span matSuffix>&deg;C</span>
          </mat-form-field>

          <mat-form-field appearance="outline" class="form-col">
            <mat-label>{{ 'sampling.weather3days' | translate }}</mat-label>
            <mat-select formControlName="weather">
              @for (option of weatherOptions; track option) {
                <mat-option [value]="option">{{ 'sampling.weatherOptions.' + option | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <h3 class="section-title">{{ 'sampling.containersTitle' | translate }}</h3>

        @if (loadingContainers()) {
          <div class="loading-containers">
            <mat-spinner diameter="24"></mat-spinner>
          </div>
        } @else if (containersFormArray.controls.length === 0) {
          <p class="empty-containers">{{ 'sampling.noContainers' | translate }}</p>
        } @else {
          <div class="containers-list" formArrayName="containers">
            @for (containerGroup of containersFormArray.controls; track containerGroup.value.containerId; let i = $index) {
              <div class="container-row" [formGroupName]="i">
                <div class="container-info">
                  <div class="container-name">{{ containerGroup.value.containerName }}</div>
                  <div class="container-specs">
                    {{ containerGroup.value.containerMaterial }} — {{ containerGroup.value.containerVolumeMl }} ml
                  </div>
                </div>
                <mat-form-field appearance="outline" class="container-barcode">
                  <mat-label>{{ 'sampling.containerBarcode' | translate }}</mat-label>
                  <input matInput formControlName="barcode" autocomplete="off"
                         [matTooltip]="'sampling.barcodeSharedHint' | translate">
                </mat-form-field>
                <button type="button" mat-icon-button
                        class="scan-btn"
                        [matTooltip]="'scan.title' | translate"
                        (click)="openScanner(i)">
                  <mat-icon>photo_camera</mat-icon>
                </button>
              </div>
            }
          </div>
        }

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

        @if (errorMessage()) {
          <p class="error-message">{{ errorMessage() }}</p>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="saving()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          <ng-container>
            <mat-icon>save</mat-icon>
            {{ 'common.save' | translate }}
          </ng-container>
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    /* AQ-403 / AQ-412 — mobile-first: stack everything, expand to 2 columns at >= 768px.
       Class names prefixed with "form-" to avoid collisions with Bootstrap .row / .col. */
    :host { display: block; width: 100%; }
    .location-subtitle { margin: -8px 24px 8px; color: #666; font-size: 14px; }
    .sampling-form { display: flex; flex-direction: column; min-width: 0; width: 100%; box-sizing: border-box; }
    .form-row { display: flex; flex-direction: column; gap: 0; width: 100%; }
    .form-col { flex: 1 1 100%; width: 100%; min-width: 0; }
    .full-width { width: 100%; }
    .checkbox-row { display: flex; flex-direction: column; gap: 8px; margin: 8px 0 16px; }
    mat-dialog-content { max-height: 70vh; }
    .section-title { margin: 8px 0; font-size: 14px; font-weight: 600; color: #555; }
    .loading-containers { display: flex; justify-content: center; padding: 16px; }
    .empty-containers { color: #888; font-style: italic; padding: 8px 0; }
    .containers-list { display: flex; flex-direction: column; gap: 8px; margin-bottom: 8px; }
    .container-row { display: flex; flex-direction: column; align-items: stretch; gap: 8px; padding: 8px; border: 1px solid #eee; border-radius: 4px; }
    .container-info { flex: 1 1 auto; min-width: 0; }
    .container-name { font-weight: 500; word-break: break-word; }
    .container-specs { font-size: 12px; color: #777; }
    .container-barcode { width: 100%; }
    .scan-btn { flex-shrink: 0; align-self: flex-end; }
    .error-message { color: #c62828; font-size: 13px; margin-top: 4px; }

    @media (min-width: 768px) {
      .sampling-form { min-width: 480px; }
      .form-row { flex-direction: row; gap: 16px; }
      .form-col { flex: 1 1 0; width: auto; }
      .checkbox-row { flex-direction: row; gap: 24px; }
      .container-row { flex-direction: row; align-items: center; gap: 12px; }
      .container-barcode { width: 220px; flex: 0 0 220px; }
      .scan-btn { align-self: auto; }
    }
  `],
})
export class SamplingFormDialogComponent implements OnInit {
  readonly data = inject<SamplingFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SamplingFormDialogComponent>);
  private readonly samplingApi = inject(SamplingApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly syncService = inject(SyncService);
  private readonly offlineStorage = inject(OfflineStorageService);

  readonly saving = signal(false);
  readonly loadingContainers = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly isEditMode = signal(false);
  readonly weatherOptions: readonly WeatherOption[] = WEATHER_OPTIONS;

  readonly containersFormArray = new FormArray<FormGroup<ContainerFormGroup>>([]);

  readonly form = new FormGroup({
    samplingDateTime: new FormControl<string>(''),
    temperature: new FormControl<number | null>(null, Validators.required),
    weather: new FormControl<string | null>(null, Validators.required),
    notes: new FormControl<string | null>(null),
    hasWaterSoftener: new FormControl<boolean>(false),
    isChlorinated: new FormControl<boolean>(false),
    containers: this.containersFormArray,
  });

  async ngOnInit(): Promise<void> {
    const sampling = this.data.sampling;
    if (sampling) {
      this.isEditMode.set(true);
      this.form.patchValue({
        samplingDateTime: this.toDatetimeLocalValue(sampling.samplingDateTime),
        temperature: sampling.temperature,
        weather: sampling.weather,
        notes: sampling.notes,
        hasWaterSoftener: sampling.hasWaterSoftener ?? false,
        isChlorinated: sampling.isChlorinated,
      });
    } else {
      this.form.patchValue({
        samplingDateTime: this.toDatetimeLocalValue(new Date().toISOString()),
      });
    }

    try {
      const required = await firstValueFrom(this.orderApi.getRequiredContainers(this.data.orderId));
      this.buildContainerFormArray(required, sampling);
    } catch {
      // AQ-427 — API indisponible (hors ligne typiquement). On reconstruit les
      // required-containers depuis le snapshot IndexedDB chargé au checkout de
      // la tournée. Si pas de snapshot trouvé, on laisse les champs vides.
      try {
        const offlineRequired = await this.offlineStorage.getRequiredContainersOffline(this.data.orderId);
        if (offlineRequired && offlineRequired.length > 0) {
          this.buildContainerFormArray(offlineRequired, sampling);
        }
      } catch {
        // Best-effort : on ignore.
      }
    } finally {
      this.loadingContainers.set(false);
    }
  }

  private buildContainerFormArray(required: RequiredContainerDto[], sampling: SamplingDto | null): void {
    this.containersFormArray.clear();
    const existingByContainerId = new Map<string, string | null>();
    if (sampling?.containers) {
      for (const c of sampling.containers) {
        existingByContainerId.set(c.containerId, c.barcode);
      }
    }
    for (const required_ of required) {
      const existingBarcode = existingByContainerId.get(required_.containerId) ?? required_.existingBarcode ?? '';
      this.containersFormArray.push(new FormGroup<ContainerFormGroup>({
        containerId: new FormControl<string>(required_.containerId, { nonNullable: true }),
        containerName: new FormControl<string>(required_.name, { nonNullable: true }),
        containerMaterial: new FormControl<string>(required_.material, { nonNullable: true }),
        containerVolumeMl: new FormControl<number>(required_.volumeMl, { nonNullable: true }),
        barcode: new FormControl<string>(existingBarcode ?? '', { nonNullable: true }),
      }));
    }
  }

  openScanner(index: number): void {
    const ref = this.dialog.open(BarcodeScannerDialogComponent, {
      width: '95vw',
      maxWidth: '520px',
      panelClass: 'responsive-dialog',
    });
    ref.afterClosed().subscribe((result: BarcodeScanResult | null | undefined) => {
      if (result?.barcode) {
        // AQ-405 — the scan fills ONLY the field that opened the scanner.
        // Every bottle must be scanned individually (physical confirmation),
        // even when the resulting codes are identical.
        const group = this.containersFormArray.at(index);
        group?.patchValue({ barcode: result.barcode });
        this.snackBar.open(
          this.translate.instant('scan.success', { value: result.barcode }),
          this.translate.instant('common.close'),
          { duration: 2500 },
        );
      }
    });
  }

  async save(): Promise<void> {
    this.errorMessage.set(null);

    // AQ-363 / AQ-405 — all non-empty barcodes of a mandate MUST be identical.
    // Surface the divergence as a toast (scanning is per-bottle, so the user
    // can just re-scan the offending row to reconcile).
    const raw = this.containersFormArray.controls
      .map(ctrl => (ctrl.value.barcode ?? '').trim())
      .filter(v => v !== '');
    const distinct = Array.from(new Set(raw));
    if (distinct.length > 1) {
      this.snackBar.open(
        this.translate.instant('sampling.errors.barcodesMustBeIdentical'),
        this.translate.instant('common.close'),
        { duration: 4000 },
      );
      return;
    }
    const canonicalBarcode = distinct[0] ?? null;

    this.saving.set(true);
    try {
      const formValue = this.form.getRawValue();
      const containers: SamplingContainerInputDto[] = this.containersFormArray.controls.map(ctrl => ({
        containerId: ctrl.value.containerId!,
        // Send the canonical value on every row so the backend's server-side
        // validation sees a consistent payload.
        barcode: canonicalBarcode,
      }));
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
        sampleBarcode: canonicalBarcode,
        containers,
      };

      // AQ-409 — when the device is offline, queue the sampling payload in
      // IndexedDB instead of hitting the network. The SyncService will replay
      // CREATE_SAMPLING / UPDATE_SAMPLING on reconnect. We close the dialog with
      // an optimistic SamplingDto shape so the caller's orderSamplings map
      // immediately reflects the entry (and enables the "complete" button).
      if (!this.syncService.onlineStatus()) {
        const actionType = this.isEditMode() ? 'UPDATE_SAMPLING' : 'CREATE_SAMPLING';
        const roundKey = this.data.roundId ?? this.data.orderId;
        devInfo('[offline] queuing', actionType, 'for order', this.data.orderId);
        try {
          await this.offlineStorage.queueAction({
            roundId: roundKey,
            actionType,
            payload: { orderId: this.data.orderId, dto },
          });
          await this.syncService.refreshPendingCount();
        } catch {
          // IDB failure is fatal for offline persistence — surface it so the user
          // knows the entry isn't safely stored.
          this.errorMessage.set(this.translate.instant('sync.offlineSaveFailed'));
          this.saving.set(false);
          return;
        }
        this.snackBar.open(
          this.translate.instant('sync.queuedOffline'),
          this.translate.instant('common.close'),
          { duration: 3000 },
        );
        // Close the dialog with an optimistic SamplingDto shape. The real id /
        // createdAt will be materialised by the server on replay; the UI mainly
        // uses this to toggle the "completeSampling" button on.
        const optimistic: SamplingDto = {
          id: this.data.sampling?.id ?? `offline-${Date.now()}`,
          orderId: this.data.orderId,
          preleveurId: this.data.sampling?.preleveurId ?? '',
          preleveurName: this.data.sampling?.preleveurName ?? '',
          samplingDateTime: dto.samplingDateTime,
          temperature: dto.temperature,
          weather: dto.weather,
          notes: dto.notes,
          hasWaterSoftener: dto.hasWaterSoftener,
          isChlorinated: dto.isChlorinated,
          sampleBarcode: dto.sampleBarcode,
          barcodeScannedAt: null,
          isValidated: false,
          validatedAt: null,
          createdAt: new Date().toISOString(),
          containers: (dto.containers ?? []).map(c => ({
            id: `offline-${c.containerId}`,
            containerId: c.containerId,
            barcode: c.barcode,
            barcodeScannedAt: null,
          })),
        };
        this.dialogRef.close(optimistic);
        return;
      }

      let result: SamplingDto;
      if (this.isEditMode()) {
        result = await firstValueFrom(this.samplingApi.update(this.data.orderId, dto));
      } else {
        result = await firstValueFrom(this.samplingApi.create(this.data.orderId, dto));
      }
      this.dialogRef.close(result);
    } catch (err: unknown) {
      const message = this.extractErrorMessage(err);
      this.errorMessage.set(message);
      this.saving.set(false);
    }
  }

  private extractErrorMessage(err: unknown): string {
    if (err && typeof err === 'object') {
      const maybeResponse = err as { error?: { error?: string; message?: string } };
      if (maybeResponse.error?.error) return maybeResponse.error.error;
      if (maybeResponse.error?.message) return maybeResponse.error.message;
    }
    // F-024 — externalised, was a hard-coded French string.
    return this.translate.instant('errors.saveFailed');
  }

  private toDatetimeLocalValue(isoString: string): string {
    const date = new Date(isoString);
    const pad = (n: number): string => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }
}
