import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout.component';
import { LoginComponent } from './pages/login/login.component';
import { HomeComponent } from './pages/home/home.component';
import { OrderDetailComponent } from './pages/orders/order-detail.component';
import { SamplingLocationListComponent } from './pages/sampling-locations/sampling-location-list.component';
import { DistributorListComponent } from './pages/distributors/distributor-list.component';
import { SectorListComponent } from './pages/sectors/sector-list.component';
import { UserListComponent } from './pages/admin/users/user-list.component';
import { RoleListComponent } from './pages/admin/roles/role-list.component';
import { OrderStatusComponent } from './pages/admin/order-status/order-status.component';
import { ValidationQueueComponent } from './pages/admin/validation-queue/validation-queue.component';
import { AuthCallbackComponent } from './pages/auth-callback/auth-callback.component';
import { AnalysisProfilesComponent } from './pages/analysis-catalog/analysis-profiles.component';
import { AnalysisProgramsComponent } from './pages/analysis-catalog/analysis-programs.component';
import { AnalysisContainersComponent } from './pages/analysis-catalog/analysis-containers.component';
import { DelegationListComponent } from './pages/admin/delegations/delegation-list.component';
import { SamplingPlanListComponent } from './pages/sampling-plans/sampling-plan-list.component';
import { SamplingPlanDetailComponent } from './pages/sampling-plans/sampling-plan-detail.component';
import { SamplingRoundListComponent } from './pages/sampling-rounds/sampling-round-list.component';
import { SamplingRoundDetailComponent } from './pages/sampling-rounds/sampling-round-detail.component';
import { ResultsComponent } from './pages/results/results.component';
import { authorizeGuard } from './guards/authorize.guard';
import { featureGuard } from './guards/feature.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'auth/callback', component: AuthCallbackComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authorizeGuard],
    children: [
      { path: '', component: HomeComponent },
      { path: 'orders/:id', component: OrderDetailComponent },
      { path: 'sampling-plans', component: SamplingPlanListComponent },
      { path: 'sampling-plans/:id', component: SamplingPlanDetailComponent },
      { path: 'sampling-rounds', component: SamplingRoundListComponent },
      { path: 'sampling-rounds/:id', component: SamplingRoundDetailComponent },
      { path: 'results', component: ResultsComponent },
      { path: 'sampling-locations', component: SamplingLocationListComponent },
      { path: 'distributors', component: DistributorListComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'sectors', component: SectorListComponent },
      { path: 'analysis-profiles', component: AnalysisProfilesComponent },
      { path: 'analysis-programs', component: AnalysisProgramsComponent },
      { path: 'analysis-containers', component: AnalysisContainersComponent },
      { path: 'admin/users', component: UserListComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'admin/roles', component: RoleListComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'admin/order-status', component: OrderStatusComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'admin/validation-queue', component: ValidationQueueComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'admin/delegations', component: DelegationListComponent, canActivate: [featureGuard(['Administrator'])] },
    ],
  },
];
