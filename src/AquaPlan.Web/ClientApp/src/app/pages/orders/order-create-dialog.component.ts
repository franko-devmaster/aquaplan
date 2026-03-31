import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { OrderDatastore } from '../../datastore/order.datastore';
import { SamplingLocationApiService } from '../../services/sampling-location-api.service';
import { firstValueFrom } from 'rxjs';

interface DistributorOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-order-create-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule,
    MatButtonModule, MatCheckboxModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'orders.createOrder' | translate }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-container">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'orders.distributor' | translate }}</mat-label>
          <mat-select formControlName="distributorId">
            @for (dist of distributors(); track dist.id) {
              <mat-option [value]="dist.id">{{ dist.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-checkbox formControlName="isUnplanned">
          {{ 'orders.isUnplanned' | translate }}
        </mat-checkbox>
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
    .form-container { display: flex; flex-direction: column; min-width: 350px; gap: 16px; }
    .full-width { width: 100%; }
  `],
})
export class OrderCreateDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<OrderCreateDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly orderStore = inject(OrderDatastore);
  private readonly locationApi = inject(SamplingLocationApiService);

  readonly distributors = signal<DistributorOption[]>([]);
  readonly saving = signal(false);
  readonly form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      distributorId: ['', Validators.required],
      isUnplanned: [false],
    });
  }

  async ngOnInit(): Promise<void> {
    // Get unique distributors from sampling locations
    const locations = await firstValueFrom(this.locationApi.getForCurrentUser());
    const uniqueDistributors = new Map<string, string>();
    for (const loc of locations) {
      if (!uniqueDistributors.has(loc.distributorId)) {
        uniqueDistributors.set(loc.distributorId, loc.distributorName ?? '');
      }
    }
    this.distributors.set(
      Array.from(uniqueDistributors, ([id, name]) => ({ id, name }))
    );
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);

    try {
      await this.orderStore.create({
        distributorId: this.form.value.distributorId,
        preleveurId: null,
        isUnplanned: this.form.value.isUnplanned,
      });
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }
}
