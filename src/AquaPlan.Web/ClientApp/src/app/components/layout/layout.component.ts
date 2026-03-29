import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { RouterModule } from '@angular/router';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [MatSidenavModule, RouterModule, HeaderComponent, SidebarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-header />
    <mat-sidenav-container class="sidenav-container">
      <mat-sidenav #sidenav mode="side" [opened]="true" class="sidenav">
        <app-sidebar />
      </mat-sidenav>
      <mat-sidenav-content class="content">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .sidenav-container { position: absolute; top: 64px; bottom: 0; left: 0; right: 0; }
    .sidenav { width: 250px; }
    .content { padding: 24px; }
  `],
})
export class LayoutComponent {
  @ViewChild('sidenav') sidenav!: MatSidenav;

  toggleSidenav(): void {
    this.sidenav.toggle();
  }
}
