import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../../services/order-api.service';
import { UserApiService } from '../../services/user-api.service';
import { OrderDetailDto, OrderStatusLabels } from '../../models/order.model';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    MatCardModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, MatFormFieldModule, MatSelectModule,
    DatePipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else if (order()) {
      <div class="page-header">
        <button mat-icon-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon>
        </button>
        <h2>{{ 'orders.details' | translate }} — {{ order()!.orderNumber }}</h2>
      </div>

      <mat-card>
        <mat-card-content>
          <div class="detail-grid">
            <div class="detail-item">
              <label>{{ 'orders.orderNumber' | translate }}</label>
              <span>{{ order()!.orderNumber }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.status.label' | translate }}</label>
              <mat-chip>{{ getStatusLabel() | translate }}</mat-chip>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.distributor' | translate }}</label>
              <span>{{ order()!.distributorName }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.createdBy' | translate }}</label>
              <span>{{ order()!.createdByName ?? order()!.createdById }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.preleveur' | translate }}</label>
              <span>{{ order()!.preleveurName ?? '-' }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.createdAt' | translate }}</label>
              <span>{{ order()!.createdAt | date:'medium' }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.isUnplanned' | translate }}</label>
              <span>{{ (order()!.isUnplanned ? 'common.yes' : 'common.no') | translate }}</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: [`
    .page-header { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; padding: 16px; }
    .detail-item { display: flex; flex-direction: column; gap: 4px; }
    .detail-item label { font-size: 12px; color: #666; text-transform: uppercase; }
    .detail-item span { font-size: 16px; }
  `],
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly orderApi = inject(OrderApiService);

  readonly order = signal<OrderDetailDto | null>(null);
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.orderApi.getById(id));
      this.order.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  getStatusLabel(): string {
    const o = this.order();
    if (!o) return '';
    return OrderStatusLabels[o.status] ?? 'orders.status.draft';
  }

  goBack(): void {
    this.router.navigate(['/orders']);
  }
}
