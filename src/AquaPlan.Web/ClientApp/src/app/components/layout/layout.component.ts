import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, ViewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, map } from 'rxjs';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [MatSidenavModule, RouterModule, HeaderComponent, SidebarComponent],
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
  `,
  styles: [`
    .sidenav-container { position: absolute; top: 64px; bottom: 0; left: 0; right: 0; }
    .sidenav { width: 250px; }
    .sidenav-mobile { width: 280px; }
    .content { padding: 24px; }
    .content-mobile { padding: 16px; }
    @media (max-width: 767px) {
      .sidenav-container { top: 56px; }
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
