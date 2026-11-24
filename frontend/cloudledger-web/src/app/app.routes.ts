import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LoginComponent } from './features/login/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AccountsComponent } from './features/accounts/accounts.component';
import { TransactionsComponent } from './features/transactions/transactions.component';
import { TransfersComponent } from './features/transfers/transfers.component';
import { RecurringComponent } from './features/recurring/recurring.component';
import { GoalsComponent } from './features/goals/goals.component';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'accounts', component: AccountsComponent, canActivate: [authGuard] },
  { path: 'transactions', component: TransactionsComponent, canActivate: [authGuard] },
  { path: 'transfers', component: TransfersComponent, canActivate: [authGuard] },
  { path: 'recurring', component: RecurringComponent, canActivate: [authGuard] },
  { path: 'goals', component: GoalsComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'login' }
];