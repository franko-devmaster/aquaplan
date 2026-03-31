import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    FormsModule, MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatDividerModule, MatProgressSpinnerModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-header>
          <mat-card-title>{{ 'app.title' | translate }}</mat-card-title>
          <mat-card-subtitle>{{ 'app.subtitle' | translate }}</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <form (ngSubmit)="onLogin()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ 'auth.email' | translate }}</mat-label>
              <input matInput type="email" [(ngModel)]="email" name="email" required>
              <mat-icon matPrefix>email</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{ 'auth.password' | translate }}</mat-label>
              <input matInput type="password" [(ngModel)]="password" name="password" required>
              <mat-icon matPrefix>lock</mat-icon>
            </mat-form-field>

            @if (errorMessage()) {
              <p class="error-message">{{ errorMessage() }}</p>
            }

            <button mat-raised-button color="primary" type="submit" class="full-width"
                    [disabled]="loading()">
              @if (loading()) {
                <mat-spinner diameter="20"></mat-spinner>
              } @else {
                {{ 'auth.loginButton' | translate }}
              }
            </button>
          </form>

          <mat-divider class="divider"></mat-divider>

          @if (oidcEnabled()) {
            <button mat-stroked-button class="full-width sso-button" (click)="onSsoLogin()">
              <mat-icon>login</mat-icon>
              {{ 'auth.loginSso' | translate }}
            </button>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; background: #f5f5f5; }
    .login-card { width: 400px; padding: 24px; }
    .full-width { width: 100%; }
    .error-message { color: #f44336; margin-bottom: 16px; text-align: center; }
    .divider { margin: 24px 0; }
    .sso-button { margin-top: 8px; }
    mat-form-field { margin-bottom: 8px; }
  `],
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  email = '';
  password = '';
  loading = signal(false);
  errorMessage = signal('');
  oidcEnabled = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      const config = await firstValueFrom(
        this.http.get<{ enabled: boolean }>('/api/auth/oidc-config')
      );
      this.oidcEnabled.set(config.enabled);
    } catch {
      // OIDC config unavailable, keep SSO hidden
    }
  }

  onSsoLogin(): void {
    window.location.href = '/api/auth/oidc-login';
  }

  async onLogin(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set('');

    const success = await this.authService.login(this.email, this.password);

    if (success) {
      await this.router.navigate(['/']);
    } else {
      this.errorMessage.set('auth.loginFailed');
    }

    this.loading.set(false);
  }
}
