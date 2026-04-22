import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, ViewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, map } from 'rxjs';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { FooterComponent } from '../footer/footer.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [MatSidenavModule, RouterModule, HeaderComponent, SidebarComponent, FooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-header (menuToggle)="toggleSidenav()" [isMobile]="isMobile()" />
    <mat-sidenav-container class="sidenav-container">
      <mat-sidenav #sidenav
        [mode]="isMobile() ? 'over' : 'side'"
        [opened]="!isMobile()"
        [class.sidenav-mobile]="isMobile()"
        class="sidenav">
        <app-sidebar (navigated)="onSidebarNavigated()" />
      </mat-sidenav>
      <mat-sidenav-content class="content" [class.content-mobile]="isMobile()">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
    <app-footer />
  `,
  styles: [`
    /* AQ-FOOTER-STICKY — footer fixed at bottom, always visible.
       Sidenav container leaves space at the bottom equal to the footer height
       so content is never hidden behind it.
       AQ-423 — header is fixed 64px (desktop + mobile). Sidebar is 240px
       dark-navy; content padding follows design-system scale. */
    .sidenav-container {
      position: absolute;
      top: 64px;
      bottom: 32px;
      left: 0;
      right: 0;
      background: var(--color-bg-page);
    }
    .sidenav {
      width: 240px;
      background: var(--color-primary-800);
      border-right: none;
    }
    .sidenav-mobile { width: 280px; }
    .content {
      padding: var(--space-6);
      background: var(--color-bg-page);
    }
    .content-mobile { padding: var(--space-3); }
    @media (min-width: 768px) and (max-width: 1199px) {
      .content { padding: var(--space-4); }
    }
  `],
})
export class LayoutComponent {
  @ViewChild('sidenav') sidenav!: MatSidenav;

  private readonly breakpointObserver = inject(BreakpointObserver);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly mobileBreakpoint = toSignal(
    this.breakpointObserver.observe('(max-width: 767px)').pipe(
      map(result => result.matches)
    ),
    { initialValue: false }
  );

  readonly isMobile = computed(() => this.mobileBreakpoint());

  constructor() {
    // Close drawer on navigation in mobile mode
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      if (this.isMobile() && this.sidenav?.opened) {
        this.sidenav.close();
      }
    });
  }

  toggleSidenav(): void {
    this.sidenav.toggle();
  }

  onSidebarNavigated(): void {
    if (this.isMobile()) {
      this.sidenav.close();
    }
  }
}
