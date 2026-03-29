import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout.component';
import { LoginComponent } from './pages/login/login.component';
import { HomeComponent } from './pages/home/home.component';
import { authorizeGuard } from './guards/authorize.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authorizeGuard],
    children: [
      { path: '', component: HomeComponent },
    ],
  },
];
