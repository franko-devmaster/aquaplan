import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import {
  OfflineStorageService,
  PendingAction,
  OfflineActionType,
} from './offline-storage.service';
import { devInfo } from '../utils/dev-log';

/** AQ-376 — aggregate result of a single flush pass over the pending queue. */
export interface SyncResult {
  succeeded: number;
  failed: number;
  skipped: number;
}

const RETRY_DELAYS_MS = [2000, 5000, 15000] as const;
const MAX_RETRIES = RETRY_DELAYS_MS.length;

/**
 * AQ-376 — replays offline pending actions (IndexedDB) against the backend when
 * connectivity returns. Event-driven via `window.online`/`offline`; exposes
 * signals consumed by the header badges (AQ-378).
 *
 * Ordering is strict FIFO by queuedAt. 409 `round.unlocked` aborts the flush,
 * purges the round state, and redirects home (admin force-unlock case).
 */
@Injectable({ providedIn: 'root' })
export class SyncService {
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  private readonly _onlineStatus = signal<boolean>(
    typeof navigator !== 'undefined' ? navigator.onLine : true,
  );
  private readonly _pendingCount = signal<number>(0);
  private readonly _isSyncing = signal<boolean>(false);
  private listenersAttached = false;

  readonly onlineStatus = computed(() => this._onlineStatus());
  readonly pendingCount = computed(() => this._pendingCount());
  readonly isSyncing = computed(() => this._isSyncing());

  /**
   * Subscribe to browser connectivity events and refresh the pending counter.
   * Idempotent: safe to call multiple times (e.g. app bootstrap + route guard).
   */
  startListening(): void {
    if (this.listenersAttached || typeof window === 'undefined') {
      return;
    }
    this.listenersAttached = true;

    window.addEventListener('online', this.handleOnline);
    window.addEventListener('offline', this.handleOffline);

    // Initial count (await intentionally backgrounded — consumers just read the signal).
    void this.refreshPendingCount();

    // If we boot already online with a non-empty queue, drain it now.
    if (this._onlineStatus()) {
      void this.maybeSyncOnReconnect();
    }
  }

  async syncNow(): Promise<SyncResult> {
    return this.flushQueue();
  }

  /** Refresh the pending counter from IndexedDB (called after every mutation). */
  async refreshPendingCount(): Promise<void> {
    try {
      const count = await this.offlineStorage.getPendingActionsCount();
      this._pendingCount.set(count);
    } catch {
      // Silently ignore — the badge just won't update; we don't want to crash.
    }
  }

  private readonly handleOnline = (): void => {
    this._onlineStatus.set(true);
    void this.maybeSyncOnReconnect();
  };

  private readonly handleOffline = (): void => {
    this._onlineStatus.set(false);
  };

  private async maybeSyncOnReconnect(): Promise<void> {
    await this.refreshPendingCount();
    if (this._pendingCount() > 0 && !this._isSyncing()) {
      await this.flushQueue();
    }
  }

  private async flushQueue(): Promise<SyncResult> {
    if (this._isSyncing()) {
      return { succeeded: 0, failed: 0, skipped: 0 };
    }
    this._isSyncing.set(true);

    const result: SyncResult = { succeeded: 0, failed: 0, skipped: 0 };

    try {
      // FIFO by queuedAt, as set up in OfflineStorageService.getPendingActions().
      const actions = await this.offlineStorage.getPendingActions();
      for (const action of actions) {
        const outcome = await this.replayAction(action);
        if (outcome === 'done') {
          result.succeeded++;
        } else if (outcome === 'rejected') {
          result.failed++;
        } else if (outcome === 'abort') {
          // 409 round.unlocked already handled inside replayAction (queue cleared,
          // snapshot dropped, redirect issued). Stop processing the rest.
          break;
        } else {
          // 5xx retries exhausted — leave action in queue for next online event.
          result.failed++;
          break;
        }
      }

      if (result.succeeded > 0) {
        this.snackBar.open(
          this.translate.instant('sync.success', { count: result.succeeded }),
          this.translate.instant('common.close'),
          { duration: 3000 },
        );
      }
    } finally {
      this._isSyncing.set(false);
      await this.refreshPendingCount();
    }

    return result;
  }

  /**
   * Replays a single action. Returns:
   * - 'done'     → 2xx, action removed.
   * - 'rejected' → non-409 4xx, action removed + user toast.
   * - 'retry'    → 5xx budget exhausted, action kept, flush should stop.
   * - 'abort'    → 409 round.unlocked, queue purged + redirect, caller stops.
   */
  private async replayAction(
    action: PendingAction,
  ): Promise<'done' | 'rejected' | 'retry' | 'abort'> {
    let attempt = action.attempts;
    while (attempt < MAX_RETRIES) {
      try {
        await this.dispatch(action);
        if (action.id !== undefined) {
          await this.offlineStorage.removeAction(action.id);
        }
        return 'done';
      } catch (err: unknown) {
        const httpError = err as HttpErrorResponse;
        const status = typeof httpError?.status === 'number' ? httpError.status : 0;
        const apiCode = (httpError?.error as { error?: string } | undefined)?.error;

        // 409 round.unlocked — admin force-unlock blew away the lock.
        if (status === 409 && apiCode === 'round.unlocked') {
          await this.handleRoundUnlocked(action.roundId);
          return 'abort';
        }

        // 401 — session expired. Keep queue intact so user can re-login and retry.
        if (status === 401) {
          return 'retry';
        }

        // Other 4xx (validation, conflicts, not found) — drop action with a toast.
        if (status >= 400 && status < 500) {
          if (action.id !== undefined) {
            await this.offlineStorage.removeAction(action.id);
          }
          this.snackBar.open(
            this.translate.instant('sync.rejected', {
              reason: apiCode ?? httpError?.statusText ?? 'unknown',
            }),
            this.translate.instant('common.close'),
            { duration: 5000 },
          );
          return 'rejected';
        }

        // 5xx (and network errors with status 0) → exponential backoff.
        attempt++;
        if (attempt >= MAX_RETRIES) {
          this.snackBar.open(
            this.translate.instant('sync.error5xx'),
            this.translate.instant('common.close'),
            { duration: 5000 },
          );
          return 'retry';
        }

        const delay = RETRY_DELAYS_MS[attempt - 1];
        await this.sleep(delay);
      }
    }
    return 'retry';
  }

  private async dispatch(action: PendingAction): Promise<void> {
    const type: OfflineActionType = action.actionType;
    const payload = action.payload as Record<string, unknown> | undefined;

    switch (type) {
      case 'CREATE_SAMPLING': {
        // payload expected shape: { orderId: string; dto: SamplingCreateDto }
        const orderId = this.readString(payload, 'orderId');
        const dto = payload?.['dto'] ?? payload;
        await firstValueFrom(
          this.http.post(`/api/orders/${orderId}/sampling`, dto),
        );
        return;
      }
      case 'UPDATE_SAMPLING': {
        // payload expected shape: { orderId: string; dto: SamplingCreateDto }
        const orderId = this.readString(payload, 'orderId');
        const dto = payload?.['dto'] ?? payload;
        await firstValueFrom(
          this.http.put(`/api/orders/${orderId}/sampling`, dto),
        );
        return;
      }
      case 'COMPLETE_SAMPLING': {
        const orderId = this.readString(payload, 'orderId');
        await firstValueFrom(
          this.http.post(`/api/orders/${orderId}/sampling/complete`, {}),
        );
        return;
      }
      case 'REPLACE_LOCATION': {
        // TODO AQ-376 — plumbing ready; endpoint exists at POST /api/orders/{id}/replace-location.
        // payload shape: { orderId: string; dto: LocationReplacementDto }
        const orderId = this.readString(payload, 'orderId');
        const dto = payload?.['dto'] ?? payload;
        await firstValueFrom(
          this.http.post(`/api/orders/${orderId}/replace-location`, dto),
        );
        return;
      }
      case 'COMPLETE_ORDER': {
        // TODO AQ-376 — no dedicated endpoint yet; transition status via the
        // existing orders/transition route so the replay remains idempotent.
        const orderId = this.readString(payload, 'orderId');
        const newStatus =
          this.readOptionalString(payload, 'newStatus') ?? 'Completed';
        await firstValueFrom(
          this.http.post(`/api/orders/${orderId}/transition`, { newStatus }),
        );
        return;
      }
      case 'UPDATE_ORDER_STATUS': {
        // AQ-409 — generic order status transition queued from the field.
        // Same backend as COMPLETE_ORDER but with an explicit target status so the
        // préleveur can kick "New → InProgress" while offline without any assumption
        // baked into the handler.
        const orderId = this.readString(payload, 'orderId');
        const newStatus = this.readString(payload, 'newStatus');
        devInfo('[offline] sync: replay UPDATE_ORDER_STATUS', orderId, '->', newStatus);
        await firstValueFrom(
          this.http.post(`/api/orders/${orderId}/transition`, { newStatus }),
        );
        return;
      }
      default: {
        // Exhaustiveness guard — unknown action type, drop it silently.
        const _exhaustive: never = type;
        return _exhaustive;
      }
    }
  }

  private async handleRoundUnlocked(roundId: string): Promise<void> {
    try {
      await this.offlineStorage.clearActionsForRound(roundId);
      await this.offlineStorage.clearSnapshot(roundId);
    } catch {
      // Best-effort cleanup — don't let a storage glitch hide the user-facing message.
    }
    this.snackBar.open(
      this.translate.instant('sync.unlockedLost'),
      this.translate.instant('common.close'),
      { duration: 8000 },
    );
    void this.router.navigate(['/sampling-rounds']);
  }

  private readString(payload: Record<string, unknown> | undefined, key: string): string {
    const value = payload?.[key];
    if (typeof value !== 'string' || value.length === 0) {
      throw new Error(`Pending action payload missing required string "${key}"`);
    }
    return value;
  }

  private readOptionalString(
    payload: Record<string, unknown> | undefined,
    key: string,
  ): string | undefined {
    const value = payload?.[key];
    return typeof value === 'string' ? value : undefined;
  }

  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
