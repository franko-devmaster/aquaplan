import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout.component';
import { HomeComponent } from './pages/home/home.component';
import { authorizeGuard } from './guards/authorize.guard';
import { featureGuard } from './guards/feature.guard';

// F-003 — lazy-load every heavy business route via loadComponent so they no longer
// ship in the initial bundle. Kept eager (synchronous import): the shell (LayoutComponent)
// and the landing dashboard (HomeComponent), which is the default child rendered on boot —
// lazy-loading it would only add a round-trip on the most common entry point.
// login + auth/callback are lazy too: they are off the critical authenticated path and a
// field préleveur on 4G should not pay for the admin/catalog code at startup.
// Guards (authorizeGuard / featureGuard) are preserved on the lazy routes — loadComponent
// is fully compatible with canActivate.
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./pages/auth-callback/auth-callback.component').then((m) => m.AuthCallbackComponent),
  },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authorizeGuard],
    children: [
      { path: '', component: HomeComponent },
      {
        path: 'orders',
        loadComponent: () =>
          import('./pages/orders/order-list.component').then((m) => m.OrderListComponent),
      },
      {
        path: 'orders/:id',
        loadComponent: () =>
          import('./pages/orders/order-detail.component').then((m) => m.OrderDetailComponent),
      },
      {
        path: 'sampling-plans',
        loadComponent: () =>
          import('./pages/sampling-plans/sampling-plan-list.component').then(
            (m) => m.SamplingPlanListComponent,
          ),
      },
      {
        path: 'sampling-plans/:id',
        loadComponent: () =>
          import('./pages/sampling-plans/sampling-plan-detail.component').then(
            (m) => m.SamplingPlanDetailComponent,
          ),
      },
      {
        path: 'sampling-rounds',
        loadComponent: () =>
          import('./pages/sampling-rounds/sampling-round-list.component').then(
            (m) => m.SamplingRoundListComponent,
          ),
      },
      {
        path: 'sampling-rounds/:id',
        loadComponent: () =>
          import('./pages/sampling-rounds/sampling-round-detail.component').then(
            (m) => m.SamplingRoundDetailComponent,
          ),
      },
      {
        path: 'results',
        loadComponent: () =>
          import('./pages/results/results.component').then((m) => m.ResultsComponent),
      },
      {
        path: 'sampling-locations',
        loadComponent: () =>
          import('./pages/sampling-locations/sampling-location-list.component').then(
            (m) => m.SamplingLocationListComponent,
          ),
      },
      {
        path: 'distributors',
        loadComponent: () =>
          import('./pages/distributors/distributor-list.component').then(
            (m) => m.DistributorListComponent,
          ),
        canActivate: [featureGuard(['Administrator'])],
      },
      {
        path: 'sectors',
        loadComponent: () =>
          import('./pages/sectors/sector-list.component').then((m) => m.SectorListComponent),
      },
      {
        path: 'analysis-profiles',
        loadComponent: () =>
          import('./pages/analysis-catalog/analysis-profiles.component').then(
            (m) => m.AnalysisProfilesComponent,
          ),
      },
      {
        path: 'analysis-programs',
        loadComponent: () =>
          import('./pages/analysis-catalog/analysis-programs.component').then(
            (m) => m.AnalysisProgramsComponent,
          ),
      },
      {
        path: 'analysis-containers',
        loadComponent: () =>
          import('./pages/analysis-catalog/analysis-containers.component').then(
            (m) => m.AnalysisContainersComponent,
          ),
      },
      {
        path: 'admin/users',
        loadComponent: () =>
          import('./pages/admin/users/user-list.component').then((m) => m.UserListComponent),
        canActivate: [featureGuard(['Administrator'])],
      },
      {
        path: 'admin/roles',
        loadComponent: () =>
          import('./pages/admin/roles/role-list.component').then((m) => m.RoleListComponent),
        canActivate: [featureGuard(['Administrator'])],
      },
      {
        path: 'admin/order-status',
        loadComponent: () =>
          import('./pages/admin/order-status/order-status.component').then(
            (m) => m.OrderStatusComponent,
          ),
        canActivate: [featureGuard(['Administrator'])],
      },
      { path: 'admin/validation-queue', redirectTo: '/sampling-locations', pathMatch: 'full' },
      {
        path: 'admin/delegations',
        loadComponent: () =>
          import('./pages/admin/delegations/delegation-list.component').then(
            (m) => m.DelegationListComponent,
          ),
        canActivate: [featureGuard(['Administrator'])],
      },
    ],
  },
];
