import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../services/auth.service';

// AQ-424 — login refreshed with Aquaplan Deep tokens: full-bleed page with
// low-contrast background pattern, centered card (420px desktop, fullscreen on
// mobile), brand mark, outlined fields with icon prefix, primary CTA + SSO below.
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-page">
      <form class="login-card" [formGroup]="form" (ngSubmit)="onLogin()">
        <div class="brand">
          <img src="assets/brand/mark.svg" width="48" height="48" alt="Aquaplan" />
          <h1 class="brand-title">{{ 'app.title' | translate }}</h1>
          <p class="brand-subtitle">{{ 'app.subtitle' | translate }}</p>
        </div>

        <h2 class="form-title">{{ 'auth.signInHeading' | translate }}</h2>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'auth.email' | translate }}</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="email" />
          <mat-icon matPrefix aria-hidden="true">mail_outline</mat-icon>
          @if (form.controls.email.touched && form.controls.email.hasError('email')) {
            <mat-error>{{ 'auth.invalidEmail' | translate }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ 'auth.password' | translate }}</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="current-password" />
          <mat-icon matPrefix aria-hidden="true">lock_outline</mat-icon>
        </mat-form-field>

        @if (errorMessage()) {
          <p class="error-message" role="alert">
            <mat-icon aria-hidden="true">error_outline</mat-icon>
            <span>{{ errorMessage() | translate }}</span>
          </p>
        }

        <button mat-flat-button color="primary" type="submit" class="full-width submit-btn"
                [disabled]="loading() || form.invalid">
          @if (loading()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            {{ 'auth.loginButton' | translate }}
          }
        </button>

        @if (oidcEnabled()) {
          <div class="divider">
            <span class="divider-line"></span>
            <span class="divider-text">{{ 'auth.or' | translate }}</span>
            <span class="divider-line"></span>
          </div>

          <button mat-stroked-button type="button" class="full-width sso-button" (click)="onSsoLogin()">
            <mat-icon aria-hidden="true">business</mat-icon>
            <span>{{ 'auth.loginSso' | translate }}</span>
          </button>
        }

        <p class="contact-hint">{{ 'auth.contactAdmin' | translate }}</p>
      </form>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .login-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--space-6);
      background-color: var(--color-neutral-50);
      background-image: url('/assets/brand/bg-login-pattern.svg');
      background-repeat: no-repeat;
      background-position: center;
      background-size: cover;
    }

    .login-card {
      width: 420px;
      max-width: 100%;
      background: var(--color-bg-surface);
      border: 1px solid var(--color-border-default);
      border-radius: var(--radius-md);
      box-shadow: var(--elevation-1);
      padding: var(--space-8);
      display: flex;
      flex-direction: column;
      gap: var(--space-5);
    }

    .brand {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-2);
      margin-bottom: var(--space-2);
    }

    .brand img {
      display: block;
    }

    .brand-title {
      margin: 0;
      font-family: var(--font-family-base);
      font-size: var(--font-size-22);
      font-weight: var(--font-weight-semibold);
      color: var(--color-fg-default);
      letter-spacing: var(--letter-spacing-tight);
    }

    .brand-subtitle {
      margin: 0;
      font-size: var(--font-size-13);
      color: var(--color-fg-muted);
      text-align: center;
    }

    .form-title {
      margin: 0;
      font-family: var(--font-family-base);
      font-size: var(--font-size-20);
      font-weight: var(--font-weight-semibold);
      color: var(--color-fg-default);
    }

    .full-width { width: 100%; }

    .submit-btn {
      height: var(--touch-md);
      font-weight: var(--font-weight-medium);
    }

    .error-message {
      margin: 0;
      display: flex;
      align-items: flex-start;
      gap: var(--space-2);
      padding: var(--space-3);
      background: var(--color-error-50);
      color: var(--color-error-700);
      border: 1px solid var(--color-error-500);
      border-radius: var(--radius-sm);
      font-size: var(--font-size-13);
      line-height: var(--line-height-snug);
    }

    .error-message mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      flex-shrink: 0;
      color: var(--color-error-600);
    }

    .divider {
      display: flex;
      align-items: center;
      gap: var(--space-3);
    }

    .divider-line {
      flex: 1;
      height: 1px;
      background: var(--color-border-default);
    }

    .divider-text {
      font-size: var(--font-size-12);
      color: var(--color-fg-muted);
      text-transform: lowercase;
    }

    .sso-button {
      height: var(--touch-md);
      color: var(--color-primary-600);
      border-color: var(--color-primary-600);
    }

    .contact-hint {
      margin: 0;
      font-size: var(--font-size-12);
      color: var(--color-fg-muted);
      text-align: center;
      font-style: italic;
    }

    /* Mobile: fullscreen card, no radius, no border */
    @media (max-width: 480px) {
      .login-page {
        padding: 0;
        align-items: stretch;
      }
      .login-card {
        width: 100%;
        border-radius: 0;
        border: none;
        box-shadow: none;
        padding: var(--space-6);
        justify-content: center;
        min-height: 100vh;
      }
    }
  `],
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  // F-049 — typed Reactive Form with client-side email validation (was raw
  // [(ngModel)] string properties with no validation).
  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly oidcEnabled = signal(false);

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
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.errorMessage.set('');

    const { email, password } = this.form.getRawValue();
    const success = await this.authService.login(email, password);

    if (success) {
      await this.router.navigate(['/']);
    } else {
      this.errorMessage.set('auth.loginFailed');
    }

    this.loading.set(false);
  }
}
