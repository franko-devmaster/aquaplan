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
import { SamplingPlanApiService } from '../../services/sampling-plan-api.service';
import { DistributorApiService } from '../../services/distributor-api.service';
import { DistributorListDto } from '../../models/distributor.model';
import { SamplingPlanDetailDto } from '../../models/sampling-plan.model';

@Component({
  selector: 'app-sampling-plan-create-dialog',
  standalone: true,
  imports: [
    FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatSelectModule, MatInputModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingPlans.createPlan' | translate }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingPlans.distributor' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedDistributorId" required>
          @for (dist of distributors(); track dist.id) {
            <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingPlans.year' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedYear" required>
          @for (y of availableYears; track y) {
            <mat-option [value]="y">{{ y }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'samplingPlans.notes' | translate }}</mat-label>
        <textarea matInput [(ngModel)]="notes" rows="3"></textarea>
      </mat-form-field>

      @if (error()) {
        <p class="error-text">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="create()"
              [disabled]="saving() || !selectedDistributorId || !selectedYear">
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
export class SamplingPlanCreateDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<SamplingPlanCreateDialogComponent>);
  private readonly planApi = inject(SamplingPlanApiService);
  private readonly distributorApi = inject(DistributorApiService);

  readonly distributors = signal<DistributorListDto[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  selectedDistributorId: string | null = null;
  selectedYear: number = new Date().getFullYear();
  notes: string = '';

  readonly currentYear = new Date().getFullYear();
  readonly availableYears = [this.currentYear, this.currentYear + 1, this.currentYear + 2];

  async ngOnInit(): Promise<void> {
    const dists = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
    this.distributors.set(dists);
  }

  async create(): Promise<void> {
    if (!this.selectedDistributorId || !this.selectedYear) {
      return;
    }

    this.saving.set(true);
    this.error.set('');

    try {
      const result = await firstValueFrom(this.planApi.create({
        distributorId: this.selectedDistributorId,
        year: this.selectedYear,
        notes: this.notes || null,
        items: [],
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
