import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { DelegationApiService } from '../../services/delegation-api.service';
import { DistributorDto } from '../../models/distributor.model';
import { SamplingRoundDetailDto } from '../../models/sampling-round.model';

@Component({
  selector: 'app-sampling-round-create-dialog',
  standalone: true,
  imports: [
    FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatSelectModule, MatInputModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.createRoundTitle' | translate }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingRounds.distributor' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedDistributorId" required>
          @for (dist of distributors(); track dist.id) {
            <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

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

      @if (error()) {
        <p class="error-text">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button color="primary" (click)="create()"
              [disabled]="saving() || !selectedDistributorId || !name">
        @if (saving()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          {{ 'common.create' | translate }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    .error-text { color: #f44336; font-size: 13px; margin-top: 8px; }
  `],
})
export class SamplingRoundCreateDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<SamplingRoundCreateDialogComponent>);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly delegationApi = inject(DelegationApiService);

  readonly distributors = signal<DistributorDto[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  selectedDistributorId: string | null = null;
  name: string = '';
  description: string = '';
  deadline: string = '';

  async ngOnInit(): Promise<void> {
    // AQ-369 — only distributors the user is authorized to create on
    const dists = await firstValueFrom(this.delegationApi.getMyAuthorizedDistributors());
    this.distributors.set(dists);
    if (dists.length === 1) {
      this.selectedDistributorId = dists[0].id;
    }
  }

  async create(): Promise<void> {
    if (!this.selectedDistributorId || !this.name) {
      return;
    }

    this.saving.set(true);
    this.error.set('');

    try {
      const result = await firstValueFrom(this.roundApi.create({
        distributorId: this.selectedDistributorId,
        name: this.name,
        description: this.description || null,
        deadline: this.deadline || new Date().toISOString().split('T')[0],
      }));
      this.dialogRef.close(result);
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.error.set(apiError?.error?.error ?? 'An error occurred');
    } finally {
      this.saving.set(false);
    }
  }
}
