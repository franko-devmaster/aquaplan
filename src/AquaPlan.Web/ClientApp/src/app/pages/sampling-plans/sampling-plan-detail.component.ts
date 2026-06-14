import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingPlanApiService } from '../../services/sampling-plan-api.service';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { AnalysisProfileApiService } from '../../services/analysis-profile-api.service';
import { AuthService } from '../../services/auth.service';
import { ConfirmService } from '../../services/confirm.service';
import { ApiErrorService } from '../../services/api-error.service';
import {
  SamplingPlanDetailDto,
  SamplingPlanItemCreateDto,
  SamplingPlanStatus,
  SamplingPlanStatusLabels,
} from '../../models/sampling-plan.model';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';
import { planStatusVariant } from '../../utils/status-variant';

interface EditableItem {
  samplingLocationId: string;
  analysisProfileId: string;
  frequencyPerYear: number;
  plannedMonths: number[];
}

interface SelectOption {
  id: string;
  label: string;
}

@Component({
  selector: 'app-sampling-plan-detail',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule,
    MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
    MatCheckboxModule, MatCardModule, MatSnackBarModule,
    DatePipe, TranslateModule, StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else if (plan()) {
      <div class="page-header">
        <div class="header-info">
          <button mat-icon-button (click)="goBack()">
            <mat-icon>arrow_back</mat-icon>
          </button>
          <h2>{{ 'samplingPlans.details' | translate }} — {{ plan()!.distributorName }} {{ plan()!.year }}</h2>
          <app-status-chip [variant]="getStatusVariant()"
                           [label]="(getStatusLabel() | translate)"></app-status-chip>
        </div>
        <div class="header-actions">
          @if (isDraft()) {
            <button mat-stroked-button color="warn" (click)="deletePlan()">
              <mat-icon>delete</mat-icon>
              {{ 'common.delete' | translate }}
            </button>
            <button mat-raised-button color="primary" (click)="save()" [disabled]="saving()">
              <mat-icon>save</mat-icon>
              {{ 'common.save' | translate }}
            </button>
            <button mat-raised-button color="accent" (click)="submitPlan()" [disabled]="saving() || editItems().length === 0">
              <mat-icon>send</mat-icon>
              {{ 'samplingPlans.submit' | translate }}
            </button>
          }
          @if (isSubmitted() && isAdmin()) {
            <button mat-raised-button color="primary" (click)="validatePlan()">
              <mat-icon>check_circle</mat-icon>
              {{ 'samplingPlans.validate' | translate }}
            </button>
            <button mat-stroked-button color="warn" (click)="rejectPlan()">
              <mat-icon>cancel</mat-icon>
              {{ 'samplingPlans.reject' | translate }}
            </button>
          }
          @if (isValidated()) {
            <button mat-raised-button color="primary" (click)="generateOrders()" [disabled]="saving()">
              <mat-icon>playlist_add</mat-icon>
              {{ 'samplingPlans.generateOrders' | translate }}
            </button>
          }
        </div>
      </div>

      <!-- Plan info -->
      <mat-card class="plan-info-card">
        <mat-card-content>
          <div class="info-grid">
            <div><strong>{{ 'samplingPlans.distributor' | translate }}:</strong> {{ plan()!.distributorName }}</div>
            <div><strong>{{ 'samplingPlans.year' | translate }}:</strong> {{ plan()!.year }}</div>
            <div><strong>{{ 'common.createdBy' | translate }}:</strong> {{ plan()!.createdByName ?? '-' }}</div>
            <div><strong>{{ 'common.createdAt' | translate }}:</strong> {{ plan()!.createdAt | date:'short' }}</div>
          </div>
          @if (plan()!.rejectionReason) {
            <div class="rejection-reason">
              <mat-icon color="warn">warning</mat-icon>
              <strong>{{ 'samplingPlans.rejectionReason' | translate }}:</strong> {{ plan()!.rejectionReason }}
            </div>
          }
          @if (isDraft()) {
            <mat-form-field appearance="outline" class="full-width notes-field">
              <mat-label>{{ 'samplingPlans.notes' | translate }}</mat-label>
              <textarea matInput [(ngModel)]="notes" rows="2"></textarea>
            </mat-form-field>
          } @else if (plan()!.notes) {
            <div><strong>{{ 'samplingPlans.notes' | translate }}:</strong> {{ plan()!.notes }}</div>
          }
        </mat-card-content>
      </mat-card>

      <!-- Items -->
      <div class="items-header">
        <h3>{{ 'samplingPlans.items' | translate }} ({{ editItems().length }})</h3>
        @if (isDraft()) {
          <button mat-stroked-button (click)="addItem()">
            <mat-icon>add</mat-icon>
            {{ 'samplingPlans.addItem' | translate }}
          </button>
        }
      </div>

      @if (isDraft()) {
        @for (item of editItems(); track $index) {
          <mat-card class="item-card">
            <mat-card-content>
              <div class="item-row">
                <mat-form-field appearance="outline" class="item-field">
                  <mat-label>{{ 'samplingPlans.samplingLocation' | translate }}</mat-label>
                  <mat-select [(ngModel)]="item.samplingLocationId">
                    @for (loc of samplingLocations(); track loc.id) {
                      <mat-option [value]="loc.id">{{ loc.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline" class="item-field">
                  <mat-label>{{ 'samplingPlans.analysisProfile' | translate }}</mat-label>
                  <mat-select [(ngModel)]="item.analysisProfileId">
                    @for (prof of analysisProfiles(); track prof.id) {
                      <mat-option [value]="prof.id">{{ prof.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline" class="freq-field">
                  <mat-label>{{ 'samplingPlans.frequency' | translate }}</mat-label>
                  <input matInput type="number" [(ngModel)]="item.frequencyPerYear" min="1" max="365">
                </mat-form-field>

                <button mat-icon-button color="warn" (click)="removeItem($index)"
                        [matTooltip]="'samplingPlans.removeItem' | translate">
                  <mat-icon>delete</mat-icon>
                </button>
              </div>

              <div class="months-row">
                <span class="months-label">{{ 'samplingPlans.plannedMonths' | translate }}:</span>
                <mat-chip-listbox multiple>
                  @for (m of months; track m.value) {
                    <mat-chip-option [selected]="item.plannedMonths.includes(m.value)"
                                     (selectionChange)="toggleMonth(item, m.value)">
                      {{ m.label }}
                    </mat-chip-option>
                  }
                </mat-chip-listbox>
              </div>
            </mat-card-content>
          </mat-card>
        }
      } @else {
        <div class="responsive-table-container">
          <table mat-table [dataSource]="plan()!.items" class="full-width">
            <ng-container matColumnDef="samplingLocation">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingPlans.samplingLocation' | translate }}</th>
              <td mat-cell *matCellDef="let item">{{ item.samplingLocationCode }} — {{ item.samplingLocationName }}</td>
            </ng-container>

            <ng-container matColumnDef="analysisProfile">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingPlans.analysisProfile' | translate }}</th>
              <td mat-cell *matCellDef="let item">{{ item.analysisProfileCode }} — {{ item.analysisProfileName }}</td>
            </ng-container>

            <ng-container matColumnDef="frequency">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingPlans.frequency' | translate }}</th>
              <td mat-cell *matCellDef="let item">{{ item.frequencyPerYear }}x</td>
            </ng-container>

            <ng-container matColumnDef="months">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingPlans.plannedMonths' | translate }}</th>
              <td mat-cell *matCellDef="let item">
                @for (m of item.plannedMonths; track m) {
                  <mat-chip>{{ getMonthLabel(m) }}</mat-chip>
                }
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="readonlyColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: readonlyColumns;"></tr>
          </table>
        </div>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; flex-wrap: wrap; gap: 8px; }
    .header-info { display: flex; align-items: center; gap: 8px; }
    .header-actions { display: flex; gap: 8px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .plan-info-card { margin-bottom: 16px; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 8px; margin-bottom: 12px; }
    .rejection-reason { display: flex; align-items: center; gap: 8px; margin: 12px 0; padding: 8px; background: #fff3e0; border-radius: 4px; }
    .notes-field { margin-top: 12px; }
    .items-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0; }
    .item-card { margin-bottom: 12px; }
    .item-row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .item-field { flex: 1; min-width: 200px; }
    .freq-field { width: 120px; }
    .months-row { display: flex; gap: 4px; align-items: center; flex-wrap: wrap; margin-top: 8px; }
    .months-label { font-size: 13px; color: #666; margin-right: 8px; }
    .full-width { width: 100%; }
    .responsive-table-container { overflow-x: auto; }
  `],
})
export class SamplingPlanDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly planApi = inject(SamplingPlanApiService);
  private readonly samplingLocationApi = inject(SamplingLocationApiService);
  private readonly analysisProfileApi = inject(AnalysisProfileApiService);
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly confirmService = inject(ConfirmService);
  private readonly apiError = inject(ApiErrorService);

  readonly plan = signal<SamplingPlanDetailDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly editItems = signal<EditableItem[]>([]);
  readonly samplingLocations = signal<SelectOption[]>([]);
  readonly analysisProfiles = signal<SelectOption[]>([]);
  notes = '';

  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );
  readonly isDraft = computed(() => this.plan()?.status === SamplingPlanStatus.Draft);
  readonly isSubmitted = computed(() => this.plan()?.status === SamplingPlanStatus.Submitted);
  readonly isValidated = computed(() => this.plan()?.status === SamplingPlanStatus.Validated);

  readonly readonlyColumns = ['samplingLocation', 'analysisProfile', 'frequency', 'months'];

  readonly months = [
    { value: 1, label: 'Jan' }, { value: 2, label: 'Fev' }, { value: 3, label: 'Mar' },
    { value: 4, label: 'Avr' }, { value: 5, label: 'Mai' }, { value: 6, label: 'Jun' },
    { value: 7, label: 'Jul' }, { value: 8, label: 'Aou' }, { value: 9, label: 'Sep' },
    { value: 10, label: 'Oct' }, { value: 11, label: 'Nov' }, { value: 12, label: 'Dec' },
  ];

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/sampling-plans']);
      return;
    }

    try {
      const [plan, locations, profiles] = await Promise.all([
        firstValueFrom(this.planApi.getById(id)),
        firstValueFrom(this.samplingLocationApi.getForCurrentUser()),
        firstValueFrom(this.analysisProfileApi.getAll()),
      ]);

      this.plan.set(plan);
      this.notes = plan.notes ?? '';

      this.samplingLocations.set(
        locations.map((l: { id: string; locationCode: string; name: string }) => ({ id: l.id, label: `${l.locationCode} — ${l.name}` }))
      );
      this.analysisProfiles.set(
        profiles.map((p: { id: string; code: string; name: string }) => ({ id: p.id, label: `${p.code} — ${p.name}` }))
      );

      this.editItems.set(plan.items.map(i => ({
        samplingLocationId: i.samplingLocationId,
        analysisProfileId: i.analysisProfileId,
        frequencyPerYear: i.frequencyPerYear,
        plannedMonths: [...i.plannedMonths],
      })));
    } finally {
      this.loading.set(false);
    }
  }

  getStatusLabel(): string {
    const p = this.plan();
    return p ? (SamplingPlanStatusLabels[p.status] ?? 'samplingPlans.status.draft') : '';
  }

  getStatusVariant(): StatusChipVariant {
    const p = this.plan();
    return p ? planStatusVariant(p.status) : 'draft';
  }

  getMonthLabel(month: number): string {
    return this.months.find(m => m.value === month)?.label ?? month.toString();
  }

  addItem(): void {
    this.editItems.update(items => [...items, {
      samplingLocationId: '',
      analysisProfileId: '',
      frequencyPerYear: 1,
      plannedMonths: [],
    }]);
  }

  removeItem(index: number): void {
    this.editItems.update(items => items.filter((_, i) => i !== index));
  }

  toggleMonth(item: EditableItem, month: number): void {
    if (item.plannedMonths.includes(month)) {
      item.plannedMonths = item.plannedMonths.filter(m => m !== month);
    } else {
      item.plannedMonths = [...item.plannedMonths, month].sort((a, b) => a - b);
    }
  }

  /**
   * F-021 — returns true on success so callers (submitPlan) can abort the chain
   * when the save fails, and surfaces errors instead of swallowing them.
   */
  async save(): Promise<boolean> {
    const p = this.plan();
    if (!p) return false;

    this.saving.set(true);
    try {
      const items: SamplingPlanItemCreateDto[] = this.editItems()
        .filter(i => i.samplingLocationId && i.analysisProfileId)
        .map(i => ({
          samplingLocationId: i.samplingLocationId,
          analysisProfileId: i.analysisProfileId,
          frequencyPerYear: i.frequencyPerYear,
          plannedMonths: i.plannedMonths,
        }));

      const updated = await firstValueFrom(this.planApi.update(p.id, {
        notes: this.notes || null,
        items,
      }));
      this.plan.set(updated);
      return true;
    } catch (err: unknown) {
      this.apiError.toast(err);
      return false;
    } finally {
      this.saving.set(false);
    }
  }

  async submitPlan(): Promise<void> {
    // F-021 — abort the submit if the preliminary save failed.
    if (!(await this.save())) return;
    const p = this.plan();
    if (!p) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.planApi.submit(p.id));
      this.plan.set(updated);
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async validatePlan(): Promise<void> {
    const p = this.plan();
    if (!p) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.planApi.validate(p.id));
      this.plan.set(updated);
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async rejectPlan(): Promise<void> {
    const p = this.plan();
    if (!p) return;

    const reason = await this.confirmService.prompt('samplingPlans.confirmReject', {
      multiline: true,
      required: true,
    });
    if (!reason) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.planApi.reject(p.id, { reason }));
      this.plan.set(updated);
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async generateOrders(): Promise<void> {
    const p = this.plan();
    if (!p) return;

    if (!(await this.confirmService.confirm('samplingPlans.confirmGenerateOrders'))) return;

    this.saving.set(true);
    try {
      const result = await firstValueFrom(this.planApi.generateOrders(p.id));
      this.snackBar.open(
        this.translate.instant('samplingPlans.ordersGenerated', { count: result.ordersCreated }),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
      this.router.navigate(['/orders']);
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async deletePlan(): Promise<void> {
    const p = this.plan();
    if (!p) return;

    if (!(await this.confirmService.confirm('samplingPlans.deleteConfirmMessage'))) return;

    try {
      await firstValueFrom(this.planApi.delete(p.id));
      this.router.navigate(['/sampling-plans']);
    } catch (err: unknown) {
      this.apiError.toast(err);
    }
  }

  goBack(): void {
    this.router.navigate(['/sampling-plans']);
  }
}
