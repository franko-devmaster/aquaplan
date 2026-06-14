import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatMenuModule } from '@angular/material/menu';
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
import { NetworkCheckService } from '../../services/network-check.service';
import { SyncService } from '../../services/sync.service';
import { OfflineStorageService } from '../../services/offline-storage.service';
import { ConfirmService } from '../../services/confirm.service';
import { ApiErrorService } from '../../services/api-error.service';
import { devInfo } from '../../utils/dev-log';
import { orderStatusVariant, roundStatusVariant } from '../../utils/status-variant';
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
import { OrderEditDialogComponent } from '../orders/order-edit-dialog.component';
import { StatusChipComponent, StatusChipVariant } from '../../components/status-chip/status-chip.component';
import { OrderIndicatorsComponent, OrderIndicatorsInput } from '../../components/order-indicators/order-indicators.component';

@Component({
  selector: 'app-sampling-round-detail',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatIconModule, MatMenuModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule,
    MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
    MatCardModule, MatSnackBarModule, DragDropModule,
    DatePipe, TranslateModule, StatusChipComponent,
    OrderIndicatorsComponent,
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
          <!-- Secondary actions (neutral) -->
          @if (isDraft() && canCreate()) {
            <button mat-stroked-button (click)="addOrder()" [disabled]="saving()">
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
          <!-- Single primary action (status-driven), aligned right -->
          @if (isDraft() && canCreate()) {
            <button mat-flat-button color="primary" (click)="assignSampler()" [disabled]="saving()">
              <mat-icon>person_add</mat-icon>
              {{ 'samplingRounds.assignSampler' | translate }}
            </button>
          }
          @if (isAssigned()) {
            <button mat-flat-button color="primary" (click)="startRound()" [disabled]="saving()">
              <mat-icon>play_arrow</mat-icon>
              {{ 'samplingRounds.startRound' | translate }}
            </button>
          }
          @if (isInProgress() && hasCompletedOrders()) {
            <button mat-flat-button color="primary" (click)="transmitAll()" [disabled]="saving()">
              <mat-icon>send</mat-icon>
              {{ 'samplingRounds.transmitAll' | translate }}
            </button>
          }
          <!-- Destructive / rare actions relegated to an overflow menu -->
          @if (isDraft() || isAssigned() || (isLocked() && isAdmin())) {
            <button mat-icon-button [matMenuTriggerFor]="roundMenu" [disabled]="saving()"
                    [attr.aria-label]="'common.actions' | translate">
              <mat-icon>more_vert</mat-icon>
            </button>
            <mat-menu #roundMenu="matMenu">
              @if (isDraft() || isAssigned()) {
                <button mat-menu-item (click)="cancelRound()">
                  <mat-icon>cancel</mat-icon>
                  <span>{{ 'samplingRounds.cancelRound' | translate }}</span>
                </button>
              }
              @if (isLocked() && isAdmin()) {
                <button mat-menu-item (click)="forceUnlockRound()">
                  <mat-icon>lock_open</mat-icon>
                  <span>{{ 'samplingRounds.forceUnlock' | translate }}</span>
                </button>
              }
              @if (isDraft()) {
                <button mat-menu-item (click)="deleteRound()">
                  <mat-icon color="warn">delete</mat-icon>
                  <span>{{ 'common.delete' | translate }}</span>
                </button>
              }
            </mat-menu>
          }
        </div>
      </div>

      @if (isLocked()) {
        <div class="lock-banner" [class.lock-banner-self]="isLockedByCurrentUser()">
          <mat-icon>{{ isLockedByCurrentUser() ? 'lock_outline' : 'lock' }}</mat-icon>
          <span>
            @if (isLockedByCurrentUser()) {
              {{ 'samplingRounds.lockedByYou' | translate }}
            } @else {
              {{ 'samplingRounds.lockedByOther' | translate:{ name: round()?.lockedByName ?? '' } }}
            }
            @if (round()?.lockedAt) {
              — {{ round()!.lockedAt | date:'short' }}
            }
          </span>
        </div>
      }

      <!-- Round info -->
      <mat-card class="round-info-card">
        <mat-card-content>
          <div class="info-grid">
            <div><strong>{{ 'samplingRounds.name' | translate }}:</strong> {{ round()!.name }}</div>
            <div><strong>{{ 'samplingRounds.deadline' | translate }}:</strong> {{ round()!.deadline | date:'shortDate' }}</div>
            <div><strong>{{ 'samplingRounds.distributor' | translate }}:</strong> {{ round()!.distributorName }}</div>
            <div><strong>{{ 'samplingRounds.sampler' | translate }}:</strong> {{ round()!.preleveurName ?? '-' }}</div>
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
                  <!-- AQ-414 — consolidated indicators (mandator note, préleveur remark, replaced LDP). -->
                  <app-order-indicators [data]="indicatorsFor(order)"></app-order-indicators>
                  <!-- Sampler comment remains clickable (edit shortcut for préleveurs). -->
                  @if (order.samplerComment) {
                    <mat-icon class="indicator-icon clickable"
                              [matTooltip]="order.samplerComment"
                              (click)="editSamplerComment(order, $event)">
                      edit_note
                    </mat-icon>
                  }
                </div>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>{{ 'common.actions' | translate }}</th>
              <td mat-cell *matCellDef="let order">
                <div class="action-buttons">
                  <!-- AQ-399 — dedicated Treat button (InProgress + admin or assigned preleveur) -->
                  @if (canTreat(order) && !orderSamplings()[order.id]) {
                    <button mat-icon-button color="primary" (click)="openSamplingForm(order, $event)"
                            [matTooltip]="'sampling.enter' | translate">
                      <mat-icon>edit_note</mat-icon>
                    </button>
                  }
                  @if (canTreat(order) && orderSamplings()[order.id]) {
                    <button mat-icon-button color="primary" (click)="openSamplingForm(order, $event)"
                            [matTooltip]="'sampling.enter' | translate">
                      <mat-icon>edit_note</mat-icon>
                    </button>
                    <button mat-icon-button color="accent" (click)="completeSampling(order, $event)"
                            [matTooltip]="'sampling.complete' | translate">
                      <mat-icon>task_alt</mat-icon>
                    </button>
                  }
                  @if ((isInProgress() || isAssigned()) && (order.status === 'New' || order.status === 'InProgress') && (isAdmin() || canTreat(order))) {
                    <button mat-icon-button (click)="replaceLocation(order, $event)"
                            [matTooltip]="'samplingRounds.replaceLocation' | translate">
                      <mat-icon>swap_horiz</mat-icon>
                    </button>
                  }
                  @if (order.status === 'Completed') {
                    <mat-icon class="sampling-done-icon sampling-done-icon--completed"
                              [matTooltip]="'sampling.completed' | translate">
                      check_circle
                    </mat-icon>
                  }
                  @if (isInProgress() && order.status === 'Completed' && (isAdmin() || canTreat(order))) {
                    <button mat-icon-button color="primary" (click)="transmitOrder(order, $event)"
                            [matTooltip]="'orders.transmit' | translate">
                      <mat-icon>send</mat-icon>
                    </button>
                  }
                  @if (order.status === 'Transmitted' || order.status === 'Done') {
                    <mat-icon class="sampling-done-icon sampling-done-icon--transmitted"
                              [matTooltip]="'sampling.transmitted' | translate">
                      check_circle
                    </mat-icon>
                  }

                  <!-- AQ-399 — kebab menu for View/Edit/Delete (pattern LDP AQ-334) -->
                  <button mat-icon-button (click)="$event.stopPropagation()"
                          [matMenuTriggerFor]="rowMenu"
                          [matMenuTriggerData]="{ order: order }"
                          [attr.aria-label]="'common.actions' | translate">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                </div>
              </td>
            </ng-container>

            <mat-menu #rowMenu="matMenu">
              <ng-template matMenuContent let-order="order">
                <button mat-menu-item (click)="onView(order)">
                  <mat-icon>visibility</mat-icon>
                  <span>{{ 'samplingRounds.actions.view' | translate }}</span>
                </button>
                @if (canEditOrder(order)) {
                  <button mat-menu-item (click)="onEdit(order)">
                    <mat-icon>edit</mat-icon>
                    <span>{{ 'samplingRounds.actions.edit' | translate }}</span>
                  </button>
                }
                @if (canRemoveOrder(order)) {
                  <!-- AQ-413 — "Retirer de la tournée" detaches the order without deleting it.
                       Kept as the only destructive-looking action on the kebab (the Order entity
                       survives, available for inclusion in another round). -->
                  <button mat-menu-item (click)="onRemoveFromRound(order)">
                    <mat-icon color="warn">link_off</mat-icon>
                    <span>{{ 'samplingRounds.actions.removeFromRound' | translate }}</span>
                  </button>
                }
              </ng-template>
            </mat-menu>

            <tr mat-header-row *matHeaderRowDef="orderColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: orderColumns;"
                [class.draggable]="isDraft()" cdkDrag></tr>
          </table>
        </div>
      } @else {
        <p class="no-data">{{ 'samplingRounds.noOrders' | translate }}</p>
      }
    } @else {
      <!-- F-030 — explicit not-found / error state with a way back. -->
      <div class="load-error">
        <mat-icon aria-hidden="true">error_outline</mat-icon>
        <p>{{ 'samplingRounds.loadError' | translate }}</p>
        <button mat-stroked-button (click)="goBack()">
          <mat-icon aria-hidden="true">arrow_back</mat-icon>
          {{ 'common.back' | translate }}
        </button>
      </div>
    }
  `,
  styles: [`
    :host { display: block; padding: var(--space-6); max-width: 1440px; margin: 0 auto; }
    .page-header {
      display: flex; justify-content: space-between; align-items: center;
      margin-bottom: var(--space-4); flex-wrap: wrap; gap: var(--space-2);
      padding-bottom: var(--space-3); border-bottom: 1px solid var(--color-border-default);
    }
    .header-info { display: flex; align-items: center; gap: var(--space-2); }
    .header-info h2 {
      margin: 0; font-family: var(--font-family-base);
      font-size: var(--font-size-22); font-weight: var(--font-weight-semibold);
      color: var(--color-fg-default);
    }
    .header-actions { display: flex; gap: var(--space-2); flex-wrap: wrap; }
    .lock-banner {
      display: flex; align-items: center; gap: var(--space-2);
      padding: var(--space-3) var(--space-4);
      background: var(--color-warning-50);
      border-left: 4px solid var(--color-warning-500);
      border-radius: var(--radius-sm);
      color: var(--color-warning-700);
      font-size: var(--font-size-14);
      margin-bottom: var(--space-4);
    }
    .lock-banner.lock-banner-self {
      background: var(--color-success-50);
      border-left-color: var(--color-success-500);
      color: var(--color-success-700);
    }
    .lock-banner mat-icon { font-size: 20px; width: 20px; height: 20px; }
    .loading-container { display: flex; justify-content: center; padding: var(--space-12); }
    .round-info-card { margin-bottom: var(--space-4); border: 1px solid var(--color-border-default); box-shadow: none; }
    .material-summary-card { margin-bottom: var(--space-4); border: 1px solid var(--color-border-default); box-shadow: none; }
    .material-title {
      margin: 0 0 var(--space-2);
      font-size: var(--font-size-12);
      font-weight: var(--font-weight-semibold);
      letter-spacing: var(--letter-spacing-wide);
      text-transform: uppercase;
      color: var(--color-fg-muted);
    }
    .container-summary { margin: 0; padding-left: var(--space-5); }
    .container-summary li { margin: var(--space-1) 0; color: var(--color-fg-default); }
    .empty-summary { color: var(--color-fg-subtle); font-style: italic; margin: 0; }
    .info-grid {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: var(--space-2); margin-bottom: var(--space-3);
      font-size: var(--font-size-14); color: var(--color-fg-default);
    }
    .description { margin-top: var(--space-2); color: var(--color-fg-default); }
    .orders-header { display: flex; justify-content: space-between; align-items: center; margin: var(--space-4) 0; }
    .progress-label { font-size: var(--font-size-14); color: var(--color-fg-muted); }
    .progress-bar-container {
      height: 6px; background: var(--color-neutral-100);
      border-radius: var(--radius-pill);
      margin-bottom: var(--space-4); overflow: hidden;
    }
    .progress-bar {
      height: 100%; background: var(--color-success-500);
      border-radius: var(--radius-pill);
      transition: width var(--duration-slow) var(--easing-standard);
    }
    .full-width { width: 100%; }
    .responsive-table-container {
      overflow-x: auto;
      background: var(--color-bg-surface);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
    }
    .no-data { text-align: center; padding: var(--space-6); color: var(--color-fg-muted); }
    .load-error {
      display: flex; flex-direction: column; align-items: center; gap: var(--space-3);
      padding: var(--space-12); color: var(--color-fg-muted); text-align: center;
    }
    .load-error mat-icon { font-size: 48px; width: 48px; height: 48px; color: var(--color-fg-subtle); }
    .indicators { display: flex; gap: var(--space-1); align-items: center; }
    .indicator-icon { font-size: 20px; width: 20px; height: 20px; color: var(--color-fg-muted); }
    .clickable { cursor: pointer; }
    .drag-handle { cursor: move; color: var(--color-fg-subtle); }
    .draggable { cursor: move; }
    .cdk-drag-preview {
      box-sizing: border-box;
      border-radius: var(--radius-sm);
      box-shadow: var(--elevation-3);
      background: var(--color-bg-surface);
    }
    .cdk-drag-placeholder { opacity: 0; }
    .cdk-drag-animating { transition: transform var(--duration-slow) var(--easing-decelerate); }
    .action-buttons { display: flex; align-items: center; gap: var(--space-1); }
    .sampling-done-icon { font-size: 24px; width: 24px; height: 24px; color: var(--color-success-500); }
    /* F-039 — token-based status colours (were inline hex #7B1FA2 / #388E3C). */
    .sampling-done-icon--completed { color: var(--chip-in-analysis-fg); }
    .sampling-done-icon--transmitted { color: var(--color-success-600); }

    @media (max-width: 768px) {
      :host { padding: var(--space-3); }
    }
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
  private readonly networkCheck = inject(NetworkCheckService);
  private readonly syncService = inject(SyncService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly confirmService = inject(ConfirmService);
  private readonly apiError = inject(ApiErrorService);

  readonly round = signal<SamplingRoundDetailDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  // F-030 — surface load failures instead of rendering a blank page on 404/error.
  readonly loadError = signal(false);

  /** Map of orderId -> SamplingDto for orders that have sampling data */
  readonly orderSamplings = signal<Record<string, SamplingDto>>({});

  readonly isDraft = computed(() => this.round()?.status === SamplingRoundStatus.Draft);
  readonly isAssigned = computed(() => this.round()?.status === SamplingRoundStatus.Assigned);
  readonly isInProgress = computed(() => this.round()?.status === SamplingRoundStatus.InProgress);
  readonly canSample = computed(() => this.isInProgress());
  readonly canCreate = this.authService.canCreateOrders;
  // F-012 — centralised role checks (AuthService is the single source of truth).
  readonly isAdmin = this.authService.isAdmin;
  // AQ-399 — distinguish preleveur from mandataire to scope menu actions.
  readonly isPreleveur = this.authService.isPreleveur;
  readonly isAssignedToCurrentUser = computed(() => {
    const r = this.round();
    const user = this.authService.currentUser();
    return !!r && !!user && r.preleveurId === user.id;
  });
  // AQ-370/AQ-371 — round lock awareness
  readonly isLocked = computed(() => this.round()?.isLocked === true);
  readonly isLockedByCurrentUser = computed(() => {
    const r = this.round();
    const currentId = this.authService.currentUser()?.id;
    return r?.isLocked === true && r.lockedById === currentId;
  });
  readonly isLockedByOther = computed(() => this.isLocked() && !this.isLockedByCurrentUser());

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
    // F-032 — rounds are never created via this route ("new" was a dead branch);
    // redirect any such navigation back to the list.
    if (!id || id === 'new') {
      this.router.navigate(['/sampling-rounds']);
      return;
    }

    try {
      const round = await firstValueFrom(this.roundApi.getById(id));
      this.round.set(round);
      await this.loadSamplings(round);
      // AQ-432 — cache the offline snapshot while online so the préleveur can run the
      // round (containers, barcode fields, catalog) without network later.
      void this.cacheOfflineSnapshot(id);
    } catch {
      // F-030 — 404 (deleted round / stale notification link) or network error.
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  // AQ-432 — best-effort: never let snapshot caching break the page.
  private async cacheOfflineSnapshot(roundId: string): Promise<void> {
    if (!navigator.onLine) {
      return;
    }
    try {
      const snapshot = await firstValueFrom(this.roundApi.getOfflineSnapshot(roundId));
      await this.offlineStorage.saveSnapshot(roundId, snapshot);
    } catch {
      // 403 for non-assigned users / offline / transient — ignore.
    }
  }

  getStatusLabel(): string {
    const r = this.round();
    return r ? (SamplingRoundStatusLabels[r.status] ?? 'samplingRounds.status.draft') : '';
  }

  getRoundStatusVariant(status: SamplingRoundStatus): StatusChipVariant {
    return roundStatusVariant(status);
  }

  getOrderStatusVariant(status: string): StatusChipVariant {
    return orderStatusVariant(status);
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

  // AQ-399 — role-scoped action guards for the row kebab menu.
  /**
   * Sampler can treat when round is InProgress AND user is admin OR the assigned préleveur.
   * Accepts order status New (will be auto-started on click) and InProgress.
   */
  canTreat(order: SamplingRoundOrderDto): boolean {
    if (!this.isInProgress()) return false;
    if (order.status !== 'InProgress' && order.status !== 'New') return false;
    return this.isAdmin() || this.isAssignedToCurrentUser();
  }

  /** Edit allowed: Admin always (except terminal statuses); Mandataire only when round not locked. Préleveurs never. */
  canEditOrder(order: SamplingRoundOrderDto): boolean {
    if (this.isPreleveur()) return false;
    if (order.status === 'Completed' || order.status === 'Transmitted' ||
        order.status === 'Done' || order.status === 'Cancelled') {
      return false;
    }
    if (this.isAdmin()) return true;
    // Mandataire / Requérant — only on rounds that are not yet locked (Draft / Assigned)
    return this.isDraft() || this.isAssigned();
  }

  /**
   * AQ-413 — "Remove from round" (detach without deleting): admins and mandataires, only while the
   * round is still modifiable (Draft or Assigned). Préleveurs never. Any non-terminal status is OK.
   */
  canRemoveOrder(order: SamplingRoundOrderDto): boolean {
    if (this.isPreleveur()) return false;
    if (order.status === 'Transmitted' || order.status === 'Done' || order.status === 'Cancelled') {
      return false;
    }
    // Round must be in a status that still allows modification. InProgress / Completed / Cancelled
    // are blocked (409 backend side) — we hide the action to avoid a confusing error.
    return this.isDraft() || this.isAssigned();
  }

  onView(order: SamplingRoundOrderDto): void {
    this.router.navigate(['/orders', order.id]);
  }

  async onEdit(order: SamplingRoundOrderDto): Promise<void> {
    // AQ-399 — open the edit dialog directly (same form as creation) instead of
    // routing to the detail page which is perceived as read-only.
    const detail = await firstValueFrom(this.orderApi.getById(order.id));
    const dialogRef = this.dialog.open(OrderEditDialogComponent, {
      width: '550px',
      panelClass: 'responsive-dialog',
      data: detail,
    });
    const changed = await firstValueFrom(dialogRef.afterClosed());
    if (changed) {
      const r = this.round();
      if (r) {
        const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
        this.round.set(refreshed);
      }
    }
  }

  /**
   * AQ-413 — detach the order from the round (keeps Order entity). Backend returns 204 on success,
   * 409 if the round is locked, 404 if not found. Errors surface via a snackbar.
   */
  async onRemoveFromRound(order: SamplingRoundOrderDto): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!(await this.confirmService.confirm('samplingRounds.confirmRemoveOrder'))) return;

    try {
      await firstValueFrom(this.roundApi.removeOrder(r.id, order.id));
      const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
      this.round.set(refreshed);
    } catch (err: unknown) {
      this.apiError.toast(err);
    }
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

  /**
   * AQ-414 — adapter from the round order DTO to the <app-order-indicators> input shape.
   * Uses the backend flags rather than recomputing from text, so the UI stays consistent
   * with any future server-side rules (e.g. whitespace-only notes should NOT count).
   */
  indicatorsFor(order: SamplingRoundOrderDto): OrderIndicatorsInput {
    return {
      hasMandatorNote: order.hasMandatorNote === true,
      hasPreleveurNote: order.hasPreleveurNote === true,
      hasReplacedLocation: order.hasReplacedLocation === true,
      mandatorNote: order.notes,
      preleveurNote: order.preleveurNote,
      locationReplacementReason: order.locationReplacementReason,
    };
  }

  async editSamplerComment(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    const newComment = await this.confirmService.prompt('samplingRounds.editComment', {
      value: order.samplerComment ?? '',
      multiline: true,
    });
    // undefined = cancelled (distinct from an empty string = cleared comment).
    if (newComment === undefined) return;

    // F-020 — optimistic update with rollback on failure (was a bare .then()
    // with no catch, leaving the user believing the comment was saved).
    const previous = this.round();
    if (previous) {
      const orders = previous.orders.map(o =>
        o.id === order.id ? { ...o, samplerComment: newComment || null } : o
      );
      this.round.set({ ...previous, orders });
    }

    try {
      await firstValueFrom(this.roundApi.updateSamplerComment(order.id, { comment: newComment }));
    } catch (err: unknown) {
      // Roll back the optimistic change and surface the error.
      if (previous) {
        this.round.set(previous);
      }
      this.apiError.toast(err);
    }
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
      this.apiError.success('samplingRounds.assigned');
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async cancelRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!(await this.confirmService.confirm('samplingRounds.confirmCancel'))) return;

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
    if (!(await this.confirmService.confirm('samplingRounds.confirmRevertToDraft'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.revertToDraft(r.id));
      this.round.set(updated);
      this.apiError.success('samplingRounds.revertedToDraft');
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async startRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    // AQ-370 — require network for the Assigned → InProgress transition (lock is posed).
    const online = await this.networkCheck.pingServer();
    if (!online) {
      this.snackBar.open(
        this.translate.instant('samplingRounds.startRequiresNetwork'),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
      return;
    }

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.start(r.id));
      this.round.set(updated);
      this.apiError.success('samplingRounds.started');
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  /** AQ-372 — admin force-unlocks a round currently held by a préleveur. */
  async forceUnlockRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!(await this.confirmService.confirm('samplingRounds.confirmForceUnlock'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.forceUnlock(r.id));
      this.round.set(updated);
      this.apiError.success('samplingRounds.forceUnlocked');
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async transmitAll(): Promise<void> {
    const r = this.round();
    if (!r) return;

    // AQ-377 — transmission talks to Limsophy: verify connectivity up-front and
    // abort loudly rather than queue half-baked transmissions.
    const online = await this.networkCheck.pingServer();
    if (!online) {
      this.snackBar.open(
        this.translate.instant('orders.transmitNoNetwork'),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
      return;
    }

    if (!(await this.confirmService.confirm('samplingRounds.confirmTransmitAll'))) return;

    this.saving.set(true);
    try {
      const updated = await firstValueFrom(this.roundApi.transmitAll(r.id));
      this.round.set(updated);
      this.apiError.success('samplingRounds.transmittedAll');
    } catch (err: unknown) {
      this.apiError.toast(err);
    } finally {
      this.saving.set(false);
    }
  }

  async deleteRound(): Promise<void> {
    const r = this.round();
    if (!r) return;

    if (!(await this.confirmService.confirm('samplingRounds.confirmDelete'))) return;

    try {
      await firstValueFrom(this.roundApi.delete(r.id));
      this.router.navigate(['/sampling-rounds']);
    } catch (err: unknown) {
      this.apiError.toast(err);
    }
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

    // AQ-399 — if the order is still in New status, transition it to InProgress
    // before opening the sampling form. This is what the sampler expects when
    // clicking the Treat button on an unstarted order in an InProgress round.
    // AQ-401 / AQ-409 — when the browser is offline, do NOT block on the backend call.
    // Queue the transition for replay on reconnect, optimistically flip the
    // local order status, and open the dialog immediately so the préleveur can
    // still fill the sampling form in the field.
    let workingOrder = order;
    if (order.status === 'New') {
      if (!this.syncService.onlineStatus()) {
        const r = this.round();
        if (r) {
          try {
            devInfo('[offline] queuing UPDATE_ORDER_STATUS (New→InProgress) for', order.id);
            await this.offlineStorage.queueAction({
              roundId: r.id,
              actionType: 'UPDATE_ORDER_STATUS',
              payload: { orderId: order.id, newStatus: 'InProgress' },
            });
            await this.syncService.refreshPendingCount();
          } catch {
            // Storage failure is best-effort; dialog still opens.
          }
          // Optimistic local update so the badge/row reflects InProgress.
          const updatedOrders = r.orders.map(o =>
            o.id === order.id ? { ...o, status: 'InProgress' } : o
          );
          this.round.set({ ...r, orders: updatedOrders });
          const reloaded = updatedOrders.find(o => o.id === order.id);
          if (reloaded) workingOrder = reloaded;
        }
      } else {
        try {
          await firstValueFrom(this.orderApi.transition(order.id, 'InProgress'));
          const r = this.round();
          if (r) {
            const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
            this.round.set(refreshed);
            const reloaded = refreshed.orders.find(o => o.id === order.id);
            if (reloaded) workingOrder = reloaded;
          }
        } catch (err: unknown) {
          this.apiError.toast(err);
          return;
        }
      }
    }

    const existingSampling = this.orderSamplings()[workingOrder.id] ?? null;

    const dialogData: SamplingFormDialogData = {
      orderId: workingOrder.id,
      sampling: existingSampling,
      orderNumber: workingOrder.orderNumber,
      locationName: `${workingOrder.samplingLocationCode} — ${workingOrder.samplingLocationName}`,
      // AQ-409 — round id propagated so offline saves queue against the round.
      roundId: this.round()?.id,
    };

    // AQ-403 — responsive sizing; let CSS media queries inside the dialog drive the layout.
    // AQ-86 — widen max on tablet/iPad for comfortable data entry.
    const dialogRef = this.dialog.open(SamplingFormDialogComponent, {
      data: dialogData,
      width: '95vw',
      maxWidth: '720px',
      panelClass: 'responsive-dialog',
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.orderSamplings.update(current => ({ ...current, [workingOrder.id]: result }));
    }
  }

  async completeSampling(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();
    if (!(await this.confirmService.confirm('sampling.confirmComplete'))) return;

    try {
      await firstValueFrom(this.samplingApi.complete(order.id));
      // Refresh round to get updated order status
      const r = this.round();
      if (r) {
        const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
        this.round.set(refreshed);
      }
      this.apiError.success('sampling.completedSuccess');
    } catch (err: unknown) {
      this.apiError.toast(err);
    }
  }

  /**
   * F-019 — fetch the sampling data for all in-progress orders in parallel
   * instead of sequentially (a 20-order round used to cost ~20 serial RTTs on
   * the field network). A missing sampling (404) is expected and simply skipped.
   */
  private async loadSamplings(round: SamplingRoundDetailDto): Promise<void> {
    const inProgressOrders = round.orders.filter(o => o.status === 'InProgress');
    const samplings: Record<string, SamplingDto> = {};

    const results = await Promise.allSettled(
      inProgressOrders.map(order => firstValueFrom(this.samplingApi.getByOrderId(order.id)))
    );
    results.forEach((result, index) => {
      if (result.status === 'fulfilled') {
        samplings[inProgressOrders[index].id] = result.value;
      }
      // rejected = no sampling exists yet for this order — expected, skip.
    });

    this.orderSamplings.set(samplings);
  }

  async transmitOrder(order: SamplingRoundOrderDto, event: Event): Promise<void> {
    event.stopPropagation();

    // AQ-377 — unitary transmission also requires live connectivity.
    const online = await this.networkCheck.pingServer();
    if (!online) {
      this.snackBar.open(
        this.translate.instant('orders.transmitNoNetwork'),
        this.translate.instant('common.close'),
        { duration: 5000 }
      );
      return;
    }

    if (!(await this.confirmService.confirm('orders.confirmTransmit'))) return;

    try {
      await firstValueFrom(this.orderApi.transition(order.id, 'Transmitted'));
      const r = this.round();
      if (r) {
        const refreshed = await firstValueFrom(this.roundApi.getById(r.id));
        this.round.set(refreshed);
      }
      this.apiError.success('orders.transmitted');
    } catch (err: unknown) {
      this.apiError.toast(err);
    }
  }

  goBack(): void {
    this.router.navigate(['/sampling-rounds']);
  }
}
