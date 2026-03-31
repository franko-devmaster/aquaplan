import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';

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
  private readonly authService = inject(AuthService);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const token = params.get('token');
    const refresh = params.get('refresh');

    if (token && refresh) {
      this.authService.setTokensFromOidc(token, refresh);
      this.router.navigate(['/']);
    } else {
      this.router.navigate(['/login'], { queryParams: { error: 'oidc_failed' } });
    }
  }
}
