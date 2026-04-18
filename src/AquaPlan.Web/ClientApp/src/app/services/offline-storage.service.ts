import { Injectable } from '@angular/core';
import { openDB, DBSchema, IDBPDatabase } from 'idb';

/**
 * AQ-375 — types of pending offline actions queued for sync-on-reconnect.
 * Evolutive union; new types can be added without breaking the store schema.
 */
export type OfflineActionType =
  | 'CREATE_SAMPLING'
  | 'UPDATE_SAMPLING'
  | 'COMPLETE_SAMPLING'
  | 'REPLACE_LOCATION'
  | 'COMPLETE_ORDER';

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
}

const DB_NAME = 'aquaplan-offline';
const DB_VERSION = 1;

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
        },
      });
    }
    return this.dbPromise;
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
    ]);
  }
}
