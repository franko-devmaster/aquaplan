import { Injectable, signal } from '@angular/core';

/**
 * F-035 — bulk actions (validate / transmit / finalize) change the very counts the
 * dashboard tiles and the orders-list bulk badges display. Before, those counters were
 * loaded once in ngOnInit and never refreshed after a bulk operation, so they stayed
 * frozen until a manual reload. This tiny signal-based bus lets any component that
 * performs a bulk mutation notify interested views to reload their counters.
 *
 * Consumers read `version()` inside an `effect` (or recompute on change); producers call
 * `notifyBulkChange()` after a successful bulk operation.
 */
@Injectable({ providedIn: 'root' })
export class DashboardRefreshService {
  private readonly _version = signal(0);

  /** Monotonic counter; bumped on every bulk mutation. Read it in an effect to react. */
  readonly version = this._version.asReadonly();

  /** Notify all consumers that bulk-affected counts (orders by status, conformity) changed. */
  notifyBulkChange(): void {
    this._version.update((v) => v + 1);
  }
}
