import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

export interface UserInfo {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  organization: string | null;
  tenantId: string;
  roles: string[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly accessToken = signal<string | null>(null);
  readonly currentUser = signal<UserInfo | null>(null);
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

  constructor() {
    const token = sessionStorage.getItem('access_token');
    if (token) {
      this.accessToken.set(token);
      this.loadCurrentUser();
    }
  }

  async login(email: string, password: string): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>('/api/auth/login', { email, password })
      );
      sessionStorage.setItem('access_token', response.accessToken);
      sessionStorage.setItem('refresh_token', response.refreshToken);
      this.accessToken.set(response.accessToken);
      await this.loadCurrentUser();
      return true;
    } catch {
      return false;
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } catch {
      // Ignore errors on logout
    }
    sessionStorage.removeItem('access_token');
    sessionStorage.removeItem('refresh_token');
    this.accessToken.set(null);
    this.currentUser.set(null);
    await this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.accessToken();
  }

  setTokensFromOidc(accessToken: string, refreshToken: string): void {
    sessionStorage.setItem('access_token', accessToken);
    sessionStorage.setItem('refresh_token', refreshToken);
    this.accessToken.set(accessToken);
    this.loadCurrentUser();
  }

  private async loadCurrentUser(): Promise<void> {
    try {
      const user = await firstValueFrom(
        this.http.get<UserInfo>('/api/auth/me')
      );
      this.currentUser.set(user);
    } catch {
      this.accessToken.set(null);
      sessionStorage.removeItem('access_token');
    }
  }
}
