import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout.component';
import { LoginComponent } from './pages/login/login.component';
import { HomeComponent } from './pages/home/home.component';
import { OrderListComponent } from './pages/orders/order-list.component';
import { OrderDetailComponent } from './pages/orders/order-detail.component';
import { SamplingLocationListComponent } from './pages/sampling-locations/sampling-location-list.component';
import { UserListComponent } from './pages/admin/users/user-list.component';
import { RoleListComponent } from './pages/admin/roles/role-list.component';
import { AuthCallbackComponent } from './pages/auth-callback/auth-callback.component';
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
      { path: 'orders', component: OrderListComponent },
      { path: 'orders/:id', component: OrderDetailComponent },
      { path: 'sampling-locations', component: SamplingLocationListComponent },
      { path: 'admin/users', component: UserListComponent, canActivate: [featureGuard(['Administrator'])] },
      { path: 'admin/roles', component: RoleListComponent, canActivate: [featureGuard(['Administrator'])] },
    ],
  },
];
