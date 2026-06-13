import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { OfflineStorageService } from './offline-storage.service';
import { devInfo } from '../utils/dev-log';

export interface UserInfo {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organization: string | null;
  tenantId: string;
  roles: string[];
  distributorId: string | null;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

const SESSION_ACCESS_KEY = 'access_token';
const SESSION_REFRESH_KEY = 'refresh_token';
const SESSION_USER_KEY = 'aq_current_user';

/**
 * AQ-409 — AuthService now persists the JWT and the cached user profile in both
 * sessionStorage (fast bootstrap, in-memory eviction) and IndexedDB (durable across
 * Safari iOS background purges while offline). logout() is split into:
 *   - `logout()`           : explicit user action, wipes everything and redirects to /login
 *   - `handleAuthFailure()` : called by the HTTP interceptor on 401, only wipes if we're
 *                             actually online; otherwise we keep the session so the user
 *                             can keep working offline and the token will be retried on
 *                             reconnect.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly offlineStorage = inject(OfflineStorageService);

  readonly accessToken = signal<string | null>(null);
  readonly currentUser = signal<UserInfo | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

  /**
   * True if the current user has at least one of the given roles.
   * Returns false when the user is not loaded yet.
   */
  hasAnyRole(roles: string[]): boolean {
    const userRoles = this.currentUser()?.roles ?? [];
    return roles.some((r) => userRoles.includes(r));
  }

  /**
   * Reactive computed — true when the user can create orders, rounds or plans.
   * Mirrors the backend [Authorize(Roles = Administrator,Requérant,Requérant-Préleveur)].
   */
  readonly canCreateOrders = computed(() => {
    const userRoles = this.currentUser()?.roles ?? [];
    return ['Administrator', 'Requérant', 'Requérant-Préleveur'].some((r) => userRoles.includes(r));
  });

  private _userLoaded: Promise<void> = Promise.resolve();

  constructor() {
    // Synchronous bootstrap from sessionStorage (keeps guards that check
    // isAuthenticated() straight after construction happy).
    const sessionToken = this.safeSessionGet(SESSION_ACCESS_KEY);
    if (sessionToken) {
      this.accessToken.set(sessionToken);
      this.rehydrateUserFromSession();
    }
    // Then, asynchronously, reconcile with IndexedDB — if the session cache was
    // wiped (Safari iOS) but IDB still has a token, restore it.
    this._userLoaded = this.bootstrapFromIndexedDb(sessionToken);
  }

  private rehydrateUserFromSession(): void {
    const cached = this.safeSessionGet(SESSION_USER_KEY);
    if (!cached) return;
    try {
      const user = JSON.parse(cached) as UserInfo;
      if (user && typeof user.id === 'string') {
        this.currentUser.set(user);
      }
    } catch {
      // Stale/corrupt payload — drop it silently.
    }
  }

  private async bootstrapFromIndexedDb(sessionToken: string | null): Promise<void> {
    try {
      const persisted = await this.offlineStorage.loadAuth();
      if (persisted?.accessToken && !sessionToken) {
        // Session cache was wiped but IDB still holds a token — restore it.
        devInfo('[offline] auth: restored JWT from IndexedDB');
        this.accessToken.set(persisted.accessToken);
        this.safeSessionSet(SESSION_ACCESS_KEY, persisted.accessToken);
        if (persisted.refreshToken) {
          this.safeSessionSet(SESSION_REFRESH_KEY, persisted.refreshToken);
        }
      }
      // Try to refresh the profile. If we're offline or the call fails, keep
      // whatever we cached — we must NOT log the user out just because the
      // network is unavailable.
      if (this.accessToken()) {
        await this.loadCurrentUser();
      }
    } catch {
      // Non-fatal — guards will redirect if no token could be restored.
    }
  }

  /**
   * Wait for the initial user load to complete.
   * Guards should call this before checking currentUser().
   */
  whenUserLoaded(): Promise<void> {
    return this._userLoaded;
  }

  async login(email: string, password: string): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>('/api/auth/login', { email, password })
      );
      await this.persistTokens(response.accessToken, response.refreshToken);
      this.accessToken.set(response.accessToken);
      await this.loadCurrentUser();
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Explicit user-initiated logout — wipes everything and sends the user to /login.
   * Do NOT call this from the HTTP interceptor; use handleAuthFailure() instead so
   * a transient 401 while offline doesn't boot the préleveur out of the field.
   */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } catch {
      // Ignore errors on logout
    }
    await this.clearSession();
    await this.router.navigate(['/login']);
  }

  /**
   * AQ-409 — HTTP interceptor hook. On a 401:
   *  - if we're online, the session is really dead → wipe and redirect.
   *  - if we're offline (or the request never left the device), keep the session
   *    so the user can keep working; the token will be revalidated at reconnect.
   */
  async handleAuthFailure(): Promise<void> {
    const online = typeof navigator !== 'undefined' ? navigator.onLine : true;
    if (!online) {
      devInfo('[offline] auth: 401 received while offline, keeping session');
      return;
    }
    devInfo('[offline] auth: 401 received online, clearing session');
    await this.clearSession();
    await this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.accessToken();
  }

  async setTokensFromOidc(accessToken: string, refreshToken: string): Promise<void> {
    // F-008 — set the in-memory signal FIRST so authorizeGuard's isAuthenticated()
    // is true the instant the callback navigates, regardless of IndexedDB latency.
    // The user-load promise is reassigned synchronously here so whenUserLoaded()
    // resolves against the fresh profile (not a stale resolved bootstrap promise).
    this.accessToken.set(accessToken);
    this._userLoaded = this.loadCurrentUser();
    await this.persistTokens(accessToken, refreshToken);
    await this._userLoaded;
  }

  private async loadCurrentUser(): Promise<void> {
    try {
      const user = await firstValueFrom(
        this.http.get<UserInfo>('/api/auth/me')
      );
      this.currentUser.set(user);
      this.safeSessionSet(SESSION_USER_KEY, JSON.stringify(user));
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status;
      // AQ-409 — only drop the token on a real 401. Network errors (status 0) or
      // 5xx should preserve the cached session so the préleveur keeps working.
      if (status === 401) {
        const online = typeof navigator !== 'undefined' ? navigator.onLine : true;
        if (online) {
          this.accessToken.set(null);
          this.safeSessionRemove(SESSION_ACCESS_KEY);
          this.safeSessionRemove(SESSION_USER_KEY);
          await this.offlineStorage.clearAuth();
        }
      }
      // else: keep current cached state (signals already in place).
    }
  }

  private async persistTokens(accessToken: string, refreshToken: string | null): Promise<void> {
    this.safeSessionSet(SESSION_ACCESS_KEY, accessToken);
    if (refreshToken) {
      this.safeSessionSet(SESSION_REFRESH_KEY, refreshToken);
    }
    await this.offlineStorage.saveAuth(accessToken, refreshToken ?? null);
  }

  private async clearSession(): Promise<void> {
    this.safeSessionRemove(SESSION_ACCESS_KEY);
    this.safeSessionRemove(SESSION_REFRESH_KEY);
    this.safeSessionRemove(SESSION_USER_KEY);
    await this.offlineStorage.clearAuth();
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

  private safeSessionGet(key: string): string | null {
    try {
      return sessionStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private safeSessionSet(key: string, value: string): void {
    try {
      sessionStorage.setItem(key, value);
    } catch {
      // quota / private mode — IndexedDB still persists the token.
    }
  }

  private safeSessionRemove(key: string): void {
    try {
      sessionStorage.removeItem(key);
    } catch {
      // ignore
    }
  }
}
