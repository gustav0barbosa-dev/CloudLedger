import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LoginComponent } from './features/login/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AccountsComponent } from './features/accounts/accounts.component';
import { CategoriesComponent } from './features/categories/categories.component';
import { TransactionsComponent } from './features/transactions/transactions.component';
import { TransfersComponent } from './features/transfers/transfers.component';
import { RecurringComponent } from './features/recurring/recurring.component';
import { GoalsComponent } from './features/goals/goals.component';
import { NotificationsComponent } from './features/notifications/notifications.component';
import { AuditComponent } from './features/audit/audit.component';
import { ReportsComponent } from './features/reports/reports.component';
import { ProfileComponent } from './features/profile/profile.component';
import { SettingsComponent } from './features/settings/settings.component';

const privateRoute = (component: Routes[number]['component']) => ({
  component,
  canActivate: [authGuard]
});

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', ...privateRoute(DashboardComponent) },
  { path: 'accounts', ...privateRoute(AccountsComponent) },
  { path: 'categories', ...privateRoute(CategoriesComponent) },
  { path: 'transactions', ...privateRoute(TransactionsComponent) },
  { path: 'transfers', ...privateRoute(TransfersComponent) },
  { path: 'recurring', ...privateRoute(RecurringComponent) },
  { path: 'goals', ...privateRoute(GoalsComponent) },
  { path: 'notifications', ...privateRoute(NotificationsComponent) },
  { path: 'audit', ...privateRoute(AuditComponent) },
  { path: 'reports', ...privateRoute(ReportsComponent) },
  { path: 'profile', ...privateRoute(ProfileComponent) },
  { path: 'settings', ...privateRoute(SettingsComponent) },
  { path: '**', redirectTo: 'login' }
];
