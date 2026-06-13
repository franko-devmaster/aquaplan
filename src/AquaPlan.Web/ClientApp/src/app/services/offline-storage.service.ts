import { Injectable } from '@angular/core';
import { openDB, DBSchema, IDBPDatabase } from 'idb';

/**
 * AQ-375 — types of pending offline actions queued for sync-on-reconnect.
 * Evolutive union; new types can be added without breaking the store schema.
 * AQ-409 — UPDATE_ORDER_STATUS folded in (extends the COMPLETE_ORDER pattern).
 */
export type OfflineActionType =
  | 'CREATE_SAMPLING'
  | 'UPDATE_SAMPLING'
  | 'COMPLETE_SAMPLING'
  | 'REPLACE_LOCATION'
  | 'COMPLETE_ORDER'
  | 'UPDATE_ORDER_STATUS';

/**
 * AQ-409 — persisted auth token (JWT + optional refresh). Stored in IndexedDB so the
 * session survives Safari iOS' aggressive sessionStorage eviction when the browser is
 * put to background while offline in the field.
 */
export interface PersistedAuth {
  accessToken: string;
  refreshToken: string | null;
  savedAt: string;
}

/**
 * AQ-375 — a single pending action waiting to be replayed to the backend.
 * The id is auto-incremented by IndexedDB; queuedAt/attempts are filled by
 * the service at queue time.
 */
export interface PendingAction {
  id?: number;
  roundId: string;
  actionType: OfflineActionType;
  payload: unknown;
  queuedAt: string;
  attempts: number;
}

/**
 * AQ-375 — full offline snapshot stored in IndexedDB. Shape mirrors the
 * backend OfflineSnapshotDto but kept typed as a generic record here to avoid
 * coupling the storage layer to the API surface.
 */
export interface OfflineSnapshot {
  roundId: string;
  data: unknown;
  savedAt: string;
}

interface AquaPlanOfflineDb extends DBSchema {
  'active-round': {
    key: string;
    value: OfflineSnapshot;
  };
  'pending-actions': {
    key: number;
    value: PendingAction;
    indexes: {
      'by-round': string;
      'by-queuedAt': string;
    };
  };
  // AQ-409 — durable auth token store (keyed by a singleton 'current' row).
  'auth': {
    key: string;
    value: PersistedAuth;
  };
}

const DB_NAME = 'aquaplan-offline';
// AQ-409 — schema v2 adds the `auth` object store.
const DB_VERSION = 2;
const AUTH_KEY = 'current';

/**
 * AQ-375 — offline storage service backed by IndexedDB via the `idb` library.
 * Two object stores: `active-round` (snapshot per round) and `pending-actions`
 * (FIFO queue of mutations to replay once back online).
 */
@Injectable({ providedIn: 'root' })
export class OfflineStorageService {
  private dbPromise: Promise<IDBPDatabase<AquaPlanOfflineDb>> | undefined;

  private getDb(): Promise<IDBPDatabase<AquaPlanOfflineDb>> {
    if (!this.dbPromise) {
      this.dbPromise = openDB<AquaPlanOfflineDb>(DB_NAME, DB_VERSION, {
        upgrade(db) {
          if (!db.objectStoreNames.contains('active-round')) {
            db.createObjectStore('active-round', { keyPath: 'roundId' });
          }
          if (!db.objectStoreNames.contains('pending-actions')) {
            const pending = db.createObjectStore('pending-actions', {
              keyPath: 'id',
              autoIncrement: true,
            });
            pending.createIndex('by-round', 'roundId');
            pending.createIndex('by-queuedAt', 'queuedAt');
          }
          // AQ-409 — v2: auth store for durable JWT persistence.
          if (!db.objectStoreNames.contains('auth')) {
            db.createObjectStore('auth');
          }
        },
      });
    }
    return this.dbPromise;
  }

  // ==== AQ-409 — auth persistence (IndexedDB) ==============================

  async saveAuth(accessToken: string, refreshToken: string | null): Promise<void> {
    try {
      const db = await this.getDb();
      const record: PersistedAuth = {
        accessToken,
        refreshToken,
        savedAt: new Date().toISOString(),
      };
      await db.put('auth', record, AUTH_KEY);
    } catch {
      // IndexedDB may be unavailable (private mode, quota) — AuthService also
      // mirrors the token in sessionStorage so we degrade gracefully.
    }
  }

  async loadAuth(): Promise<PersistedAuth | null> {
    try {
      const db = await this.getDb();
      const row = await db.get('auth', AUTH_KEY);
      return row ?? null;
    } catch {
      return null;
    }
  }

  async clearAuth(): Promise<void> {
    try {
      const db = await this.getDb();
      await db.delete('auth', AUTH_KEY);
    } catch {
      // ignore — session cleanup is best-effort
    }
  }

  async saveSnapshot(roundId: string, data: unknown): Promise<void> {
    const db = await this.getDb();
    const snapshot: OfflineSnapshot = {
      roundId,
      data,
      savedAt: new Date().toISOString(),
    };
    await db.put('active-round', snapshot);
  }

  async getSnapshot(roundId: string): Promise<OfflineSnapshot | undefined> {
    const db = await this.getDb();
    return db.get('active-round', roundId);
  }

  async getActiveRoundIds(): Promise<string[]> {
    const db = await this.getDb();
    const keys = await db.getAllKeys('active-round');
    return keys as string[];
  }

  async clearSnapshot(roundId: string): Promise<void> {
    const db = await this.getDb();
    await db.delete('active-round', roundId);
  }

  async clearAllSnapshots(): Promise<void> {
    const db = await this.getDb();
    await db.clear('active-round');
  }

  /**
   * AQ-427 — en mode hors ligne, le dialog sampling-form ne peut pas appeler
   * /api/orders/{id}/required-containers. On reconstitue la liste à partir du
   * snapshot offline stocké lors du checkout de la tournée : pour chaque
   * programme du mandat, on agrège ses profils.container → déduplication par
   * containerId. Retourne null si aucun snapshot trouvé (→ le caller peut
   * afficher un message plus clair au préleveur).
   */
  async getRequiredContainersOffline(orderId: string): Promise<OfflineRequiredContainer[] | null> {
    const db = await this.getDb();
    const all = await db.getAll('active-round');
    for (const snapshot of all) {
      const data = snapshot.data as OfflineSnapshotData | undefined;
      if (!data?.orders) continue;
      const order = data.orders.find(o => o.id === orderId);
      if (!order) continue;
      const programs = data.analysisPrograms ?? [];
      const seen = new Map<string, OfflineRequiredContainer>();
      for (const orderProgram of order.analysisPrograms ?? []) {
        const prog = programs.find(p => p.id === orderProgram.analysisProgramId);
        if (!prog?.requiredContainers) continue;
        for (const c of prog.requiredContainers) {
          if (!seen.has(c.containerId)) {
            seen.set(c.containerId, {
              containerId: c.containerId,
              code: c.code,
              name: c.name,
              material: c.material,
              volumeMl: c.volumeMl,
              existingBarcode: order.sampling?.sampleBarcode ?? null,
            });
          }
        }
      }
      return Array.from(seen.values());
    }
    return null;
  }

  async queueAction(
    action: Omit<PendingAction, 'id' | 'queuedAt' | 'attempts'>
  ): Promise<number> {
    const db = await this.getDb();
    const record: PendingAction = {
      ...action,
      queuedAt: new Date().toISOString(),
      attempts: 0,
    };
    const id = await db.add('pending-actions', record);
    return id as number;
  }

  async getPendingActions(roundId?: string): Promise<PendingAction[]> {
    const db = await this.getDb();
    if (roundId) {
      return db.getAllFromIndex('pending-actions', 'by-round', roundId);
    }
    // No predicate — return every queued action ordered by insertion (FIFO).
    return db.getAllFromIndex('pending-actions', 'by-queuedAt');
  }

  async removeAction(actionId: number): Promise<void> {
    const db = await this.getDb();
    await db.delete('pending-actions', actionId);
  }

  /**
   * F-010 — persist the running retry counter for a queued action. Without this the
   * `attempts` field is dead code: every `online` event restarts the full backoff
   * budget for an action that keeps failing. Persisting it lets SyncService enforce a
   * cumulative cap across flush passes (and reconnects) instead of per-flush.
   */
  async updateActionAttempts(actionId: number, attempts: number): Promise<void> {
    const db = await this.getDb();
    const existing = await db.get('pending-actions', actionId);
    if (!existing) {
      return;
    }
    await db.put('pending-actions', { ...existing, attempts });
  }

  async clearActionsForRound(roundId: string): Promise<void> {
    const db = await this.getDb();
    const tx = db.transaction('pending-actions', 'readwrite');
    const idx = tx.store.index('by-round');
    let cursor = await idx.openCursor(roundId);
    while (cursor) {
      await cursor.delete();
      cursor = await cursor.continue();
    }
    await tx.done;
  }

  async getPendingActionsCount(): Promise<number> {
    const db = await this.getDb();
    return db.count('pending-actions');
  }

  async clearAll(): Promise<void> {
    const db = await this.getDb();
    await Promise.all([
      db.clear('active-round'),
      db.clear('pending-actions'),
      db.clear('auth'),
    ]);
  }
}

/**
 * AQ-427 — subset typé du snapshot (shape backend) utilisé pour recalculer
 * les contenants requis en offline sans coupler le storage aux DTOs publics.
 */
interface OfflineSnapshotData {
  orders?: OfflineOrder[];
  analysisPrograms?: OfflineProgram[];
}

interface OfflineOrder {
  id: string;
  analysisPrograms?: { analysisProgramId: string }[];
  sampling?: { sampleBarcode?: string | null } | null;
}

interface OfflineProgram {
  id: string;
  requiredContainers?: OfflineContainer[];
}

interface OfflineContainer {
  containerId: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
}

/** AQ-427 — shape parallèle à RequiredContainerDto pour éviter l'import mutuel. */
export interface OfflineRequiredContainer {
  containerId: string;
  code: string;
  name: string;
  material: string;
  volumeMl: number;
  existingBarcode: string | null;
}
