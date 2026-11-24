import { Component, inject, OnInit, signal } from '@angular/core';
import { SummaryCardsComponent } from './components/summary-cards/summary-cards.component';
import { FinancialChartComponent } from './components/financial-chart/financial-chart.component';
import { TransactionHistoryComponent } from './components/transaction-history/transaction-history.component';
import { ExpenseDistributionComponent } from './components/expense-distribution/expense-distribution.component';
import { UpcomingPaymentsComponent } from './components/upcoming-payments/upcoming-payments.component';
import { SavingsGoalsComponent } from './components/savings-goals/savings-goals.component';
import { DashboardService } from './services/dashboard.service';
import { DashboardData } from './models/dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    SummaryCardsComponent,
    FinancialChartComponent,
    TransactionHistoryComponent,
    ExpenseDistributionComponent,
    UpcomingPaymentsComponent,
    SavingsGoalsComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly service = inject(DashboardService);

  readonly data = signal<DashboardData | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.service.getDashboard().subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar o dashboard. Tente novamente mais tarde.');
        this.loading.set(false);
      }
    });
  }
}
