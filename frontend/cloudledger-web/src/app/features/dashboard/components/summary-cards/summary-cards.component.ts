import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { DashboardSummary } from '../../models/dashboard.model';

@Component({
  selector: 'app-summary-cards',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe],
  template: `
    <div class="stats">
      <div class="card">
        <div class="stat-label">Saldo total</div>
        <div class="stat-value">{{ summary.totalBalance | currency: 'BRL' }}</div>
        <div class="change">{{ summary.balanceChangePercent | number: '1.1-1' }}%</div>
      </div>
      <div class="card">
        <div class="stat-label">Receitas</div>
        <div class="stat-value">{{ summary.income | currency: 'BRL' }}</div>
        <div class="change">{{ summary.incomeChangePercent | number: '1.1-1' }}%</div>
      </div>
      <div class="card">
        <div class="stat-label">Despesas</div>
        <div class="stat-value">{{ summary.expense | currency: 'BRL' }}</div>
        <div class="change down">{{ summary.expenseChangePercent | number: '1.1-1' }}%</div>
      </div>
      <div class="card">
        <div class="stat-label">Economia</div>
        <div class="stat-value">{{ summary.savings | currency: 'BRL' }}</div>
        <div class="change">{{ summary.savingsChangePercent | number: '1.1-1' }}%</div>
      </div>
    </div>
  `
})
export class SummaryCardsComponent {
  @Input({ required: true }) summary!: DashboardSummary;
}
