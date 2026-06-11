import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../services/auth.service';

interface OidcExchangeResponse {
  accessToken: string;
  refreshToken: string;
}

// Sprint Sec F-006 — the OIDC callback no longer receives tokens in the URL.
// The API redirects here with a short-lived single-use ?code=… which is redeemed
// via POST /api/auth/oidc-exchange. Tokens never appear in browser history,
// proxy logs, or the Referer header.
@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="callback-container">
      <mat-spinner diameter="48"></mat-spinner>
    </div>
  `,
  styles: [`
    .callback-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; }
  `],
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

  async ngOnInit(): Promise<void> {
    const code = this.route.snapshot.queryParamMap.get('code');

    if (!code) {
      await this.router.navigate(['/login'], { queryParams: { error: 'oidc_failed' } });
      return;
    }

    try {
      const tokens = await firstValueFrom(
        this.http.post<OidcExchangeResponse>('/api/auth/oidc-exchange', { code })
      );
      await this.authService.setTokensFromOidc(tokens.accessToken, tokens.refreshToken);
      await this.router.navigate(['/']);
    } catch {
      await this.router.navigate(['/login'], { queryParams: { error: 'oidc_failed' } });
    }
  }
}
