import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { DatePipe } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../../services/sampling-round-api.service';
import { SamplingApiService } from '../../services/sampling-api.service';
import { OrderApiService } from '../../services/order-api.service';
import { AuthService } from '../../services/auth.service';
import {
  SamplingRoundDetailDto,
  SamplingRoundOrderDto,
  SamplingRoundStatus,
  SamplingRoundStatusLabels,
} from '../../models/sampling-round.model';
import { SamplingDto } from '../../models/sampling.model';
import { SamplingFormDialogComponent, SamplingFormDialogData } from './sampling-form-dialog.component';
import { RoundAddOrderDialogComponent, RoundAddOrderDialogData } from './round-add-order-dialog.component';
import { AssignSamplerDialogComponent, AssignSamplerDialogData } from './assign-sampler-dialog.component';
import { ReplaceLocationDialogComponent, ReplaceLocationDialogData } from './replace-location-dialog.component';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';

@Component({
  selector: 'app-sampling-round-detail',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule,
    MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
    MatCardModule, MatSnackBarModule, DragDropModule,
    DatePipe, TranslateModule, StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else if (round()) {
      <div class="page-header">
        <div class="header-info">
          <button mat-icon-button (click)="goBack()">
            <mat-icon>arrow_back</mat-icon>
          </button>
          <h2>{{ round()!.name }}</h2>
          <app-status-chip [variant]="getRoundStatusVariant(round()!.status)"
                           [label]="(getStatusLabel() | translate)"></app-status-chip>
        </div>
        <div class="header-actions">
          @if (isDraft()) {
            <button mat-stroked-button color="warn" (click)="deleteRound()">
              <mat-icon>delete</mat-icon>
              {{ 'common.delete' | translate }}
            </button>
          }
          @if (isDraft() || isAssigned()) {
            <button mat-stroked-button color="warn" (click)="cancelRound()" [disabled]="saving()">
              <mat-icon>cancel</mat-icon>
              {{ 'samplingRounds.cancelRound' | translate }}
            </button>
          }
          @if (isDraft() && canCreate()) {
            <button mat-raised-button color="primary" (click)="assignSampler()" [disabled]="saving()">
              <mat-icon>person_add</mat-icon>
              {{ 'samplingRounds.assignSampler' | translate }}
            </button>
            <button mat-raised-button color="accent" (click)="addOrder()" [disabled]="saving()">
              <mat-icon>add</mat-icon>
              {{ 'samplingRounds.addOrder' | translate }}
            </button>
          }
          @if (isAssigned() && isAdmin()) {
            <button mat-stroked-button (click)="revertToDraft()" [disabled]="saving()">
              <mat-icon>undo</mat-icon>
              {{ 'samplingRounds.revertToDraft' | translate }}
            </button>
          }
          @if (isAssigned()) {
            <button mat-raised-button color="primary" (click)="startRound()" [disabled]="saving()">
              <mat-icon>play_arrow</mat-icon>
              {{ 'samplingRounds.startRound' | translate }}
            </button>
          }
          @if (isInProgress() && hasCompletedOrders()) {
            <button mat-raised-button color="accent" (click)="transmitAll()" [disabled]="saving()">
              <mat-icon>send</mat-icon>
              {{ 'samplingRounds.transmitAll' | translate }}
            </button>
          }
        </div>
      </div>

      <!-- Round info -->
      <mat-card class="round-info-card">
        <mat-card-content>
          <div class="info-grid">
            <div><strong>{{ 'samplingRounds.name' | translate }}:</strong> {{ round()!.name }}</div>
            <div><strong>{{ 'samplingRounds.deadline' | translate }}:</strong> {{ round()!.deadline | date:'shortDate' }}</div>
            <div><strong>{{ 'samplingRounds.distributor' | translate }}:</strong> {{ round()!.distributorName }}</div>
            <div><strong>{{ 'samplingRounds.sampler' | translate }}:</strong> {{ round()!.samplerName ?? '-' }}</div>
            <div><strong>{{ 'common.createdAt' | translate }}:</strong> {{ round()!.createdAt | date:'short' }}</div>
          </div>
          @if (round()!.description) {
            <div class="description">
              <strong>{{ 'samplingRounds.description' | translate }}:</strong> {{ round()!.description }}
            </div>
          }
        </mat-card-content>
      </mat-card>

      <!-- Material summary -->
      <mat-card class="material-summary-card">
        <mat-card-content>
          <h3 class="material-title">{{ 'samplingRounds.materialToPrepare' | translate }}</h3>
          @if (round()!.containerSummary && round()!.containerSummary.length > 0) {
            <ul class="container-summary">
              @for (item of round()!.containerSummary; track item.containerId) {
                <li>{{ item.count }} × {{ item.name }} ({{ item.volumeMl }} ml, {{ item.material }})</li>
              }
            </ul>
          } @else {
            <p class="empty-summary">{{ 'samplingRounds.noMaterial' | translate }}</p>
          }
        </mat-card-content>
      </mat-card>

      <!-- Orders with progress -->
      <div class="orders-header">
        <h3>{{ 'samplingRounds.orders' | translate }} ({{ round()!.orders.length }})</h3>
        @if (round()!.orders.length > 0) {
          <span class="progress-label">{{ completedCount() }}/{{ round()!.orders.length }} {{ 'samplingRounds.progress' | translate }}</span>
        }
      </div>
      @if (round()!.orders.length > 0) {
        <div class="progress-bar-container">
          <div class="progress-bar" [style.width.%]="progressPercent()"></div>
        </div>
      }

      @if (round()!.orders.length > 0) {
        <div class="responsive-table-container" cdkDropList (cdkDropListDropped)="onDrop($event)"
             [cdkDropListDisabled]="!isDraft()">
          <table mat-table [dataSource]="round()!.orders" class="full-width">
            <ng-container matColumnDef="sortOrder">
              <th mat-header-cell *matHeaderCellDef>#</th>
              <td mat-cell *matCellDef="let order">
                @if (isDraft()) {
                  <mat-icon class="drag-handle" cdkDragHandle>drag_indicator</mat-icon>
                }
                {{ order.sortOrder }}
              </td>
            </ng-container>

            <ng-container matColumnDef="location">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.location' | translate }}</th>
              <td mat-cell *matCellDef="let order">
                {{ order.samplingLocationCode }} — {{ order.samplingLocationName }}
              </td>
            </ng-container>

            <ng-container matColumnDef="sector">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.sector' | translate }}</th>
              <td mat-cell *matCellDef="let order">{{ order.sectorName ?? '-' }}</td>
            </ng-container>

            <ng-container matColumnDef="profiles">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.programs' | translate }}</th>
              <td mat-cell *matCellDef="let order">
                {{ order.analysisProgramNames.join(', ') }}
              </td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.status.label' | translate }}</th>
              <td mat-cell *matCellDef="let order">
                <app-status-chip [variant]="getOrderStatusVariant(order.status)"
                                 [label]="('orders.status.' + toCamelCase(order.status) | translate)"></app-status-chip>
              </td>
            </ng-container>

            <ng-container matColumnDef="remarks">
              <th mat-header-cell *matHeaderCellDef>{{ 'samplingRounds.remarks' | translate }}</th>
              <td mat-cell *matCellDef="let order">
                <div class="indicators">
                  @if (order.hasLocationReplacement) {
                    <mat-icon class="indicator-icon"
                              [style.color]="'#FF9800'"
                              [matTooltip]="'samplingRounds.locationReplaced' | translate">
                      swap_horiz
                    </mat-icon>
                  }
                  @if (order.mandataireNotes) {
                    <mat-icon class="indicator-icon clickable"
                              [style.color]="'#1976D2'"
                              [matTooltip]="order.mandataireNotes"
                              (click)="showNotes(order, $event)">
                      info_outline
                    </mat-icon>
                  }
                  @if (order.samplerComment) {
                    <mat-icon class="indicator-icon clickable"
                              [style.color]="'#9E9E9E'"
                              [matTooltip]="order.samplerComment"
                              (click)="editSamplerComment(order, $event)">
                      edit_note
                    </mat-icon>
                  }
                </div>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let order">
                <div class="action-buttons">
                  @if (isDraft()) {
                    <button mat-icon-button color="warn" (click)="removeOrder(order, $event)"
                            [matTooltip]="'samplingRounds.removeOrder' | translate">
                      <mat-icon>remove_circle_outline</mat-icon>
                    </button>
                  }
                  @if (isDraft()) {
                    <button mat-icon-button (click)="editOrder(order, $event)"
                            [matTooltip]="'common.edit' | translate">
                      <mat-icon>edit</mat-icon>
                    </button>
                  }
                  @if ((isInProgress() || isAssigned()) && (order.status === 'New' || order.status === 'InProgress')) {
                    <button mat-icon-button (click)="replaceLocation(order, $event)"
                            [matTooltip]="'samplingRounds.replaceLocation' | translate">
                      <mat-icon>swap_horiz</mat-icon>
                    </button>
                  }
                  @if (canSample() && order.status === 'InProgress' && !orderSamplings()[order.id]) {
                    <button mat-icon-button color="primary" (click)="openSamplingForm(order, $event)"
                            [matTooltip]="'sampling.enter' | translate">
                      <mat-icon>edit_note</mat-icon>
                    </button>
                  }
                  @if (canSample() && order.status === 'InProgress' && orderSamplings()[order.id]) {
                    <button mat-icon-button color="primary" (click)="openSamplingForm(order, $event)"
                            [matTooltip]="'common.edit' | translate">
                      <mat-icon>edit</mat-icon>
                    </button>
                    <button mat-icon-button color="accent" (click)="completeSampling(order, $event)"
                            [matTooltip]="'sampling.complete' | translate">
                      <mat-icon>task_alt</mat-icon>
                    </button>
                  }
                  @if (order.status === 'Completed') {
                    <mat-icon class="sampling-done-icon" [style.color]="'#7B1FA2'"
                              [matTooltip]="'sampling.completed' | translate">
                      check_circle
                    </mat-icon>
                  }
                  @if (isInProgress() && order.status === 'Completed') {
                    <button mat-icon-button color="primary" (click)="transmitOrder(order, $event)"
                            [matTooltip]="'orders.transmit' | translate">
                      <mat-icon>send</mat-icon>
                    </button>
                  }
                  @if (order.status === 'Transmitted' || order.status === 'Done') {
                    <mat-icon class="sampling-done-icon" [style.color]="'#388E3C'"
                              [matTooltip]="'sampling.transmitted' | translate">
                      check_circle
                    </mat-icon>
                  }
                </div>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="orderColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: orderColumns;"
                [class.draggable]="isDraft()" cdkDrag></tr>
          </table>
        </div>
      } @else {
        <p class="no-data">{{ 'samplingRounds.noOrders' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; flex-wrap: wrap; gap: 8px; }
    .header-info { display: flex; align-items: center; gap: 8px; }
    .header-actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .round-info-card { margin-bottom: 16px; }
    .material-summary-card { margin-bottom: 16px; }
    .material-title { margin: 0 0 8px; font-size: 15px; font-weight: 600; color: #455A64; }
    .container-summary { margin: 0; padding-left: 20px; }
    .container-summary li { margin: 4px 0; }
    .empty-summary { color: #888; font-style: italic; margin: 0; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 8px; margin-bottom: 12px; }
    .description { margin-top: 8px; }
    .orders-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0; }
    .progress-label { font-size: 14px; color: #666; }
    .progress-bar-container { height: 6px; background: #e0e0e0; border-radius: 3px; margin-bottom: 16px; overflow: hidden; }
    .progress-bar { height: 100%; background: #388E3C; border-radius: 3px; transition: width 0.3s ease; }
    .full-width { width: 100%; }
    .responsive-table-container { overflow-x: auto; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .indicators { display: flex; gap: 4px; align-items: center; }
    .indicator-icon { font-size: 20px; width: 20px; height: 20px; }
    .clickable { cursor: pointer; }
    .drag-handle { cursor: move; color: #999; }
    .draggable { cursor: move; }
    .cdk-drag-preview { box-sizing: border-box; border-radius: 4px; box-shadow: 0 5px 5px -3px rgba(0, 0, 0, 0.2), 0 8px 10px 1px rgba(0, 0, 0, 0.14); background: white; }
    .cdk-drag-placeholder { opacity: 0; }
    .cdk-drag-animating { transition: transform 250ms cubic-bezier(0, 0, 0.2, 1); }
    .action-buttons { display: flex; align-items: center; gap: 4px; }
    .sampling-done-icon { font-size: 24px; width: 24px; height: 24px; }
  `],
})
export class SamplingRoundDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roundApi = inject(SamplingRoundApiService);
  private readonly samplingApi = inject(SamplingApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly round = signal<SamplingRoundDetailDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);

  /** Map of orderId -> SamplingDto for orders that have sampling data */
  readonly orderSamplings = signal<Record<string, SamplingDto>>({});

  readonly isDraft = computed(() => this.round()?.status === SamplingRoundStatus.Draft);
  readonly isAssigned = computed(() => this.round()?.status === SamplingRoundStatus.Assigned);
  readonly isInProgress = computed(() => this.round()?.status === SamplingRoundStatus.InProgress);
  readonly canSample = computed(() => this.isInProgress());
  readonly canCreate = this.authService.canCreateOrders;
  readonly isAdmin = computed(() =>
    this.authService.currentUser()?.roles.includes('Administrator') ?? false
  );

  readonly allOrdersCompleted = computed(() => {
    const r = this.round();
    if (!r || r.orders.length === 0) return false;
    return r.orders.every(o => o.status === 'Completed' || o.status === 'Transmitted' || o.status === 'Done' || o.status === 'Cancelled');
  });

  readonly hasCompletedOrders = computed(() => {
    const r = this.round();
    if (!r) return false;
    return r.orders.some(o => o.status === 'Completed');
  });

  readonly completedCount = computed(() => {
    const r = this.round();
    if (!r) return 0;
    return r.orders.filter(o =>
      o.status === 'Completed' || o.status === 'Transmitted' || o.status === 'Done'
    ).length;
  });

  readonly progressPercent = computed(() => {
    const r = this.round();
    if (!r || r.orders.length === 0) return 0;
    return (this.completedCount() / r.orders.length) * 100;
  });

  readonly orderColumns = ['sortOrder', 'location', 'sector', 'profiles', 'status', 'remarks', 'actions'];

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      // TODO: handle create mode if needed
      this.loading.set(false);
      return;
    }

    try {
      const round = await firstValueFrom(this.roundApi.getById(id));
      this.round.set(round);
      await this.loadSamplings(round);
    } finally {
      this.loading.set(false);
    }
  }

  getStatusLabel(): string {
    const r = this.round();
    return r ? (SamplingRoundStatusLabels[r.status] ?? 'samplingRounds.status.draft') : '';
  }

  getRoundStatusVariant(status: SamplingRoundStatus): StatusChipVariant {
    const map: Record<string, StatusChipVariant> = {
      'Draft': 'draft',
      'Assigned': 'info',
      'InProgress': 'info',
      'Completed': 'success',
      'Cancelled': 'danger',
    };
    return map[status] ?? 'draft';
  }

  getOrderStatusVariant(status: string): StatusChipVariant {
    const map: Record<string, StatusChipVariant> = {
      'New': 'draft',
      'InProgress': 'info',
      'Completed': 'success',
      'Transmitted': 'success',
      'Done': 'success',
      'Cancelled': 'danger',
    };
    return map[status] ?? 'draft';
  }

  toCamelCase(value: string): string {
    if (!value) return value;
    return value.charAt(0).toLowerCase() + value.slice(1);
  }

  async onDrop(event: CdkDragDrop<SamplingRoundOrderDto[]>): Promise<void> {
    const r = this.round();
    if (!r) return;

    const orders = [...r.orders];
    moveItemInArray(orders, event.previousIndex, event.currentIndex);

    const reorderedOrders = orders.map((o, i) => ({ ...o, sortOrder: i + 1 }));
    this.round.set({ ...r, orders: reorderedOrders });

    try {
      await firstValueFrom(this.roundApi.reorderOrders(r.id, reorderedOrders.map(o => o.id)));
    } catch {
      // Reload on error to restore server state
      const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
      this.round.set(refreshed);
    }
  }

  async removeOrder(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    const r = this.round();
    if (!r) return;

    if (!confirm(this.translate.instant('samplingRounds.confirmRemoveOrder'))) return;

    try {
      await firstValueFrom(this.roundApi.removeOrder(r.id, order.id));
      const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
      this.round.set(refreshed);
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    }
  }

  editOrder(order: SamplingRoundOrderDto, event: Event): void {
    event.stopPropagation();
    this.router.navigate(['/orders', order.id]);
  }

  async replaceLocation(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    const r = this.round();
    if (!r) return;

    const dialogRef = this.dialog.open(ReplaceLocationDialogComponent, {
      data: { orderId: order.id, distributorId: r.distributorId } as ReplaceLocationDialogData,
      width: '500px',
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
      this.round.set(refreshed);
      this.snackBar.open(
        this.translate.instant('samplingRounds.locationReplaced'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    }
  }

  showNotes(order: SamplingRoundOrderDto, event: Event): void {
    event.stopPropagation();
    this.snackBar.open(
      order.mandataireNotes ?? '',
      this.translate.instant('common.close'),
      { duration: 10000 }
    );
  }

  editSamplerComment(order: SamplingRoundOrderDto, event: Event): void {
    event.stopPropagation();
    const newComment = prompt(
      this.translate.instant('samplingRounds.editComment'),
      order.samplerComment ?? ''
    );
    if (newComment === null) return;

    firstValueFrom(this.roundApi.updateSamplerComment(order.id, { comment: newComment }))
      .then(() => {
        const r = this.round();
        if (r) {
          const orders = r.orders.map(o =>
            o.id === order.id ? { ...o, samplerComment: newComment || null } : o
          );
          this.round.set({ ...r, orders });
        }
      });
  }

  async assignSampler(): Promise<void> {
    const r = this.round();
    if (!r) return;

    const dialogRef = this.dialog.open(AssignSamplerDialogComponent, {
      data: { distributorId: r.distributorId, distributorName: r.distributorName } as AssignSamplerDialogData,
      width: '500px',
    });

    const preleveurId = await firstValueFrom(dialogRef.afterClosed());
    if (!preleveurId) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.assign(r.id, { preleveurId }));
      this.round.set(updated);
      this.snackBar.open(
        this.translate.instant('samplingRounds.assigned'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    } finally {
      this.saving.set(false);
    }
  }

  async cancelRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!confirm(this.translate.instant('samplingRounds.confirmCancel'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.cancel(r.id));
      this.round.set(updated);
    } finally {
      this.saving.set(false);
    }
  }

  async revertToDraft(): Promise<void> {
    const r = this.round();
    if (!r) return;
    if (!confirm(this.translate.instant('samplingRounds.confirmRevertToDraft'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.revertToDraft(r.id));
      this.round.set(updated);
      this.snackBar.open(
        this.translate.instant('samplingRounds.revertedToDraft'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    } finally {
      this.saving.set(false);
    }
  }

  async startRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    // Start all "New" orders in the round to transition them (and the round) to InProgress
    const newOrders = r.orders.filter(o => o.status === 'New');
    if (newOrders.length === 0) return;

    this.saving.set(true);
    try {
      for (const order of newOrders) {
        await firstValueFrom(this.roundApi.startOrder(order.id));
      }
      const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
      this.round.set(refreshed);
      this.snackBar.open(
        this.translate.instant('samplingRounds.started'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    } finally {
      this.saving.set(false);
    }
  }

  async transmitAll(): Promise<void> {
    const r = this.round();
    if (!r) return;
    if (!confirm(this.translate.instant('samplingRounds.confirmTransmitAll'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.transmitAll(r.id));
      this.round.set(updated);
      this.snackBar.open(
        this.translate.instant('samplingRounds.transmittedAll'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    } finally {
      this.saving.set(false);
    }
  }

  async deleteRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!confirm(this.translate.instant('samplingRounds.confirmDelete'))) return;

    await firstValueFrom(this.roundApi.delete(r.id));
    this.router.navigate(['/sampling-rounds']);
  }

  async addOrder(): Promise<void> {
    const r = this.round();
    if (!r) return;

    const dialogData: RoundAddOrderDialogData = {
      roundId: r.id,
      distributorId: r.distributorId,
      distributorName: r.distributorName,
    };

    const dialogRef = this.dialog.open(RoundAddOrderDialogComponent, {
      data: dialogData,
      width: '560px',
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.round.set(result);
    }
  }

  async openSamplingForm(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    const existingSampling = this.orderSamplings()[order.id] ?? null;

    const dialogData: SamplingFormDialogData = {
      orderId: order.id,
      sampling: existingSampling,
      orderNumber: order.orderNumber,
      locationName: `${order.samplingLocationCode} — ${order.samplingLocationName}`,
    };

    const dialogRef = this.dialog.open(SamplingFormDialogComponent, {
      data: dialogData,
      width: '560px',
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.orderSamplings.update(current => ({ ...current, [order.id]: result }));
    }
  }

  async completeSampling(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    if (!confirm(this.translate.instant('sampling.confirmComplete'))) return;

    try {
      await firstValueFrom(this.samplingApi.complete(order.id));
      // Refresh round to get updated order status
      const r = this.round();
      if (r) {
        const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
        this.round.set(refreshed);
      }
      this.snackBar.open(
        this.translate.instant('sampling.completedSuccess'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    }
  }

  private async loadSamplings(round: SamplingRoundDetailDto): Promise<void> {
    const inProgressOrders = round.orders.filter(o => o.status === 'InProgress');
    const samplings: Record<string, SamplingDto> = {};

    for (const order of inProgressOrders) {
      try {
        const sampling = await firstValueFrom(this.samplingApi.getByOrderId(order.id));
        samplings[order.id] = sampling;
      } catch {
        // No sampling exists for this order yet — that is expected
      }
    }

    this.orderSamplings.set(samplings);
  }

  async transmitOrder(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    if (!confirm(this.translate.instant('orders.confirmTransmit'))) return;

    try {
      await firstValueFrom(this.orderApi.transition(order.id, 'Transmitted'));
      const r = this.round();
      if (r) {
        const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
        this.round.set(refreshed);
      }
      this.snackBar.open(
        this.translate.instant('orders.transmitted'),
        this.translate.instant('common.close'),
        { duration: 3000 }
      );
    } catch (err: unknown) {
      const apiError = err as { error?: { error?: string } };
      this.snackBar.open(
        apiError?.error?.error ?? 'Error',
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
    }
  }

  goBack(): void {
    this.router.navigate(['/sampling-rounds']);
  }
}
