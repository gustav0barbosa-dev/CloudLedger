import { Routes } from '@angular/router';
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
  { path: 'dashboard', component: DashboardComponent },
  { path: 'accounts', component: AccountsComponent },
  { path: 'transactions', component: TransactionsComponent },
  { path: 'transfers', component: TransfersComponent },
  { path: 'recurring', component: RecurringComponent },
  { path: 'goals', component: GoalsComponent },
  { path: '**', redirectTo: 'login' }
];