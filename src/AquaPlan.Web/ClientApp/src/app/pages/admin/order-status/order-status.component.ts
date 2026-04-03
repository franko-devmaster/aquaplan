import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderStatusApiService } from '../../../services/order-status-api.service';
import { OrderStatusDto } from '../../../models/order-status.model';
import { OrderStatus, OrderStatusLabels } from '../../../models/order.model';

interface StatusWithTransitions {
  status: OrderStatusDto;
  transitions: OrderStatusDto[];
}

@Component({
  selector: 'app-order-status',
  standalone: true,
  imports: [
    MatTableModule, MatChipsModule, MatProgressSpinnerModule,
    MatIconModule, MatCardModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'orderStatus.title' | translate }}</h2>
    </div>

    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <div class="status-grid">
        @for (item of statusItems(); track item.status.status) {
          <mat-card class="status-card">
            <mat-card-header>
              <div class="status-badge" [style.background-color]="item.status.color"></div>
              <mat-card-title>{{ getStatusTranslationKey(item.status.status) | translate }}</mat-card-title>
              <mat-card-subtitle>{{ getStatusDescriptionKey(item.status.status) | translate }}</mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <div class="status-meta">
                @if (item.status.isTerminal) {
                  <mat-chip class="terminal-chip">
                    <mat-icon matChipAvatar>block</mat-icon>
                    {{ 'orderStatus.terminal' | translate }}
                  </mat-chip>
                }
              </div>
              @if (item.transitions.length > 0) {
                <div class="transitions-section">
                  <span class="transitions-label">{{ 'orderStatus.allowedTransitions' | translate }}:</span>
                  <div class="transitions-list">
                    @for (t of item.transitions; track t.status) {
                      <mat-chip>
                        <span class="transition-dot" [style.background-color]="t.color"></span>
                        {{ getStatusTranslationKey(t.status) | translate }}
                      </mat-chip>
                    }
                  </div>
                </div>
              } @else {
                <p class="no-transitions">{{ 'orderStatus.noTransitions' | translate }}</p>
              }
            </mat-card-content>
          </mat-card>
        }
      </div>

      @if (statusItems().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .status-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(350px, 1fr)); gap: 16px; }
    .status-card { margin-bottom: 0; }
    .status-badge { width: 12px; height: 12px; border-radius: 50%; margin-right: 8px; flex-shrink: 0; align-self: center; }
    mat-card-header { display: flex; align-items: center; }
    .status-meta { margin: 8px 0; }
    .terminal-chip { font-size: 12px; }
    .transitions-section { margin-top: 12px; }
    .transitions-label { font-size: 13px; color: #666; display: block; margin-bottom: 8px; }
    .transitions-list { display: flex; flex-wrap: wrap; gap: 8px; }
    .transition-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; margin-right: 4px; }
    .no-transitions { font-size: 13px; color: #999; font-style: italic; }
  `],
})
export class OrderStatusComponent implements OnInit {
  private readonly orderStatusApi = inject(OrderStatusApiService);

  readonly statusItems = signal<StatusWithTransitions[]>([]);
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const statuses = await firstValueFrom(this.orderStatusApi.getAll());
      const items: StatusWithTransitions[] = [];

      for (const status of statuses) {
        const transitions = await firstValueFrom(
          this.orderStatusApi.getAllowedTransitions(status.status)
        );
        items.push({ status, transitions });
      }

      this.statusItems.set(items);
    } finally {
      this.loading.set(false);
    }
  }

  getStatusTranslationKey(status: OrderStatus): string {
    return OrderStatusLabels[status] ?? status.toString();
  }

  getStatusDescriptionKey(status: OrderStatus): string {
    const key = this.getStatusTranslationKey(status);
    return key.replace('orders.status.', 'orderStatus.descriptions.');
  }
}
