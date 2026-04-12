import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import {
  SamplingRoundDetailDto,
  SamplingRoundListDto,
  SamplingRoundStatus,
} from '../../models/sampling-round.model';

export interface OrderLinkRoundDialogData {
  orderId: string;
  distributorId: string;
}

@Component({
  selector: 'app-order-link-round-dialog',
  standalone: true,
  imports: [
    FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatSelectModule, MatInputModule, MatRadioModule, MatProgressSpinnerModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.linkToRound' | translate }}</h2>
    <mat-dialog-content>
      <mat-radio-group [(ngModel)]="mode" class="mode-group">
        <mat-radio-button value="existing">{{ 'samplingRounds.linkToExisting' | translate }}</mat-radio-button>
        <mat-radio-button value="new">{{ 'samplingRounds.createNew' | translate }}</mat-radio-button>
      </mat-radio-group>

      @if (mode === 'existing') {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.selectRound' | translate }}</mat-label>
          <mat-select [(ngModel)]="selectedRoundId" required>
            @for (round of availableRounds(); track round.id) {
              <mat-option [value]="round.id">{{ round.name }} ({{ round.distributorName }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (availableRounds().length === 0 && !loadingRounds()) {
          <p class="info-text">{{ 'samplingRounds.noRoundsAvailable' | translate }}</p>
        }
      }

      @if (mode === 'new') {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.name' | translate }}</mat-label>
          <input matInput [(ngModel)]="name" required>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.description' | translate }}</mat-label>
          <textarea matInput [(ngModel)]="description" rows="3"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'samplingRounds.deadline' | translate }}</mat-label>
          <input matInput type="date" [(ngModel)]="deadline">
        </mat-form-field>
      }

      @if (error()) {
        <p class="error-text">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="confirm()"
              [disabled]="saving() || !isValid()">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.confirm' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    .mode-group { display: flex; gap: 16px; margin-bottom: 16px; }
    .error-text { color: #f44336; font-size: 13px; margin-top: 8px; }
    .info-text { color: #666; font-size: 13px; font-style: italic; }
  `],
})
export class OrderLinkRoundDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderLinkRoundDialogComponent>);
  private readonly data: OrderLinkRoundDialogData = inject(MAT_DIALOG_DATA);
  private readonly roundApi = inject(SamplingRoundApiService);

  readonly availableRounds = signal<SamplingRoundListDto[]>([]);
  readonly loadingRounds = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  mode: 'existing' | 'new' = 'existing';
  selectedRoundId: string | null = null;
  name: string = '';
  description: string = '';
  deadline: string = '';

  async ngOnInit(): Promise<void> {
    await this.loadRounds();
  }

  private async loadRounds(): Promise<void> {
    this.loadingRounds.set(true);
    try {
      const result = await firstValueFrom(this.roundApi.getFiltered({
        statuses: [SamplingRoundStatus.Draft, SamplingRoundStatus.Assigned],
        pageSize: 100,
      }));
      const filtered = result.items.filter(r => r.distributorId === this.data.distributorId);
      this.availableRounds.set(filtered);
    } finally {
      this.loadingRounds.set(false);
    }
  }

  isValid(): boolean {
    if (this.mode === 'existing') {
      return !!this.selectedRoundId;
    }
    return !!this.name;
  }

  async confirm(): Promise<void> {
    if (!this.isValid()) {
      return;
    }

    this.saving.set(true);
    this.error.set('');

    try {
      let roundDetail: SamplingRoundDetailDto;

      if (this.mode === 'new') {
        roundDetail = await firstValueFrom(this.roundApi.create({
          distributorId: this.data.distributorId,
          name: this.name,
          description: this.description || null,
          deadline: this.deadline || new Date().toISOString().split('T')[0],
        }));
      } else {
        roundDetail = await firstValueFrom(this.roundApi.getById(this.selectedRoundId!));
      }

      await firstValueFrom(this.roundApi.addOrder(roundDetail.id, this.data.orderId));
      this.dialogRef.close(roundDetail);
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.error.set(apiError?.error?.error ?? 'An error occurred');
    } finally {
      this.saving.set(false);
    }
  }
}
