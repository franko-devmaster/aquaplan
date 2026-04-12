import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../../services/order-api.service';
import { OrderDatastore } from '../../datastore/order.datastore';
import { OrderDetailDto, OrderStatus, OrderStatusLabels, UnplannedReasonLabels } from '../../models/order.model';
import { AuthService } from '../../services/auth.service';
import { OrderEditDialogComponent } from './order-edit-dialog.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog.component';
import { OrderLinkRoundDialogComponent } from './order-link-round-dialog.component';
import { SamplingRoundDetailDto } from '../../models/sampling-round.model';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    MatCardModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, MatDialogModule, MatSnackBarModule,
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
        <div class="header-left">
          <button mat-icon-button (click)="goBack()">
            <mat-icon>arrow_back</mat-icon>
          </button>
          <h2>{{ 'orders.details' | translate }} — {{ order()!.orderNumber }}</h2>
        </div>
        @if (canEdit() || canDelete() || canLinkToRound()) {
          <div class="header-actions">
            @if (canLinkToRound()) {
              <button mat-raised-button (click)="openLinkRoundDialog()">
                <mat-icon>route</mat-icon>
                {{ 'orders.linkToRound' | translate }}
              </button>
            }
            @if (canEdit()) {
              <button mat-raised-button color="primary" (click)="openEditDialog()">
                <mat-icon>edit</mat-icon>
                {{ 'common.edit' | translate }}
              </button>
            }
            @if (canDelete()) {
              <button mat-raised-button color="warn" (click)="confirmDelete()">
                <mat-icon>delete</mat-icon>
                {{ 'common.delete' | translate }}
              </button>
            }
          </div>
        }
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
              <label>{{ 'orders.samplingLocation' | translate }}</label>
              <span>{{ order()!.samplingLocationName ?? '-' }}</span>
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
              <label>{{ 'orders.plannedDate' | translate }}</label>
              <span>{{ order()!.plannedDate ? (order()!.plannedDate | date:'mediumDate') : '-' }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.createdAt' | translate }}</label>
              <span>{{ order()!.createdAt | date:'medium' }}</span>
            </div>
            <div class="detail-item">
              <label>{{ 'orders.isUnplanned' | translate }}</label>
              <span>{{ (order()!.isUnplanned ? 'common.yes' : 'common.no') | translate }}</span>
            </div>
            @if (order()!.isUnplanned && order()!.unplannedReason !== null) {
              <div class="detail-item">
                <label>{{ 'orders.unplannedReason.label' | translate }}</label>
                <span>{{ getUnplannedReasonLabel() | translate }}</span>
              </div>
            }
            @if (order()!.unplannedReasonDetails) {
              <div class="detail-item">
                <label>{{ 'orders.unplannedReason.details' | translate }}</label>
                <span>{{ order()!.unplannedReasonDetails }}</span>
              </div>
            }
            @if (order()!.isDelegated) {
              <div class="detail-item">
                <label>{{ 'orders.delegation' | translate }}</label>
                <mat-chip color="accent" highlighted>{{ 'orders.delegated' | translate }}</mat-chip>
              </div>
            }
            @if (order()!.updatedAt) {
              <div class="detail-item">
                <label>{{ 'orders.updatedAt' | translate }}</label>
                <span>{{ order()!.updatedAt | date:'medium' }}</span>
              </div>
            }
          </div>

          @if (order()!.notes) {
            <div class="notes-section">
              <label>{{ 'orders.notes' | translate }}</label>
              <p>{{ order()!.notes }}</p>
            </div>
          }

          @if (order()!.analysisProfiles.length > 0) {
            <div class="profiles-section">
              <label>{{ 'orders.analysisProfiles' | translate }}</label>
              <div class="profiles-list">
                @for (profile of order()!.analysisProfiles; track profile.analysisProfileId) {
                  <mat-chip>{{ profile.code }} — {{ profile.name }}</mat-chip>
                }
              </div>
            </div>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .header-left { display: flex; align-items: center; gap: 8px; }
    .header-actions { display: flex; gap: 8px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; padding: 16px; }
    .detail-item { display: flex; flex-direction: column; gap: 4px; }
    .detail-item label { font-size: 12px; color: #666; text-transform: uppercase; }
    .detail-item span { font-size: 16px; }
    .notes-section { padding: 16px; border-top: 1px solid #e0e0e0; margin-top: 8px; }
    .notes-section label { font-size: 12px; color: #666; text-transform: uppercase; display: block; margin-bottom: 8px; }
    .notes-section p { margin: 0; white-space: pre-wrap; }
    .profiles-section { padding: 16px; border-top: 1px solid #e0e0e0; margin-top: 8px; }
    .profiles-section label { font-size: 12px; color: #666; text-transform: uppercase; display: block; margin-bottom: 8px; }
    .profiles-list { display: flex; gap: 8px; flex-wrap: wrap; }
  `],
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly orderApi = inject(OrderApiService);
  private readonly orderStore = inject(OrderDatastore);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);
  private readonly authService = inject(AuthService);

  readonly order = signal<OrderDetailDto | null>(null);
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    await this.loadOrder();
  }

  private async loadOrder(): Promise<void> {
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

  private isAdmin(): boolean {
    return this.authService.currentUser()?.roles.includes('Administrator') ?? false;
  }

  canEdit(): boolean {
    const o = this.order();
    if (!o) return false;
    if (this.isAdmin()) {
      return o.status !== OrderStatus.Completed && o.status !== OrderStatus.Cancelled;
    }
    return o.status === OrderStatus.Draft || o.status === OrderStatus.Assigned;
  }

  canDelete(): boolean {
    const o = this.order();
    if (!o) return false;
    if (this.isAdmin()) {
      return o.status < OrderStatus.SamplingCompleted;
    }
    return o.status === OrderStatus.Draft || o.status === OrderStatus.Assigned;
  }

  getStatusLabel(): string {
    const o = this.order();
    if (!o) return '';
    return OrderStatusLabels[o.status] ?? 'orders.status.draft';
  }

  getUnplannedReasonLabel(): string {
    const o = this.order();
    if (!o || o.unplannedReason === null) return '';
    return UnplannedReasonLabels[o.unplannedReason] ?? '';
  }

  canLinkToRound(): boolean {
    const o = this.order();
    if (!o) return false;
    return o.status === OrderStatus.Draft;
  }

  openLinkRoundDialog(): void {
    const o = this.order();
    if (!o) return;

    const dialogRef = this.dialog.open(OrderLinkRoundDialogComponent, {
      width: '500px',
      panelClass: 'responsive-dialog',
      data: { orderId: o.id, distributorId: o.distributorId },
    });
    dialogRef.afterClosed().subscribe((result: SamplingRoundDetailDto | undefined) => {
      if (result) {
        this.snackBar.open(
          this.translate.instant('samplingRounds.linkedSuccess'),
          this.translate.instant('common.close'),
          { duration: 3000 }
        );
      }
    });
  }

  openEditDialog(): void {
    const dialogRef = this.dialog.open(OrderEditDialogComponent, {
      width: '550px',
      panelClass: 'responsive-dialog',
      data: this.order(),
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadOrder();
      }
    });
  }

  confirmDelete(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      panelClass: 'responsive-dialog',
      data: {
        title: this.translate.instant('orders.deleteConfirmTitle'),
        message: this.translate.instant('orders.deleteConfirmMessage'),
      },
    });
    dialogRef.afterClosed().subscribe(async (confirmed) => {
      if (confirmed) {
        try {
          await this.orderStore.deleteOrder(this.order()!.id);
          this.snackBar.open(
            this.translate.instant('orders.deleteSuccess'),
            this.translate.instant('common.close'),
            { duration: 3000 }
          );
          this.router.navigate(['/orders']);
        } catch {
          this.snackBar.open(
            this.translate.instant('orders.deleteError'),
            this.translate.instant('common.close'),
            { duration: 5000 }
          );
        }
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/orders']);
  }
}
