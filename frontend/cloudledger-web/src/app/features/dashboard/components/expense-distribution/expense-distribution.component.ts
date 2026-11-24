import { Component, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { CategoryExpense } from '../../models/dashboard.model';

@Component({
  selector: 'app-expense-distribution',
  standalone: true,
  imports: [CurrencyPipe],
  template: `
    <div class="aside-panel">
      <div class="card-head">
        <h3>Despesas por categoria</h3>
        <span class="total">100%</span>
      </div>

      @for (category of categories(); track category.categoryName) {
        <div class="distribution-row">
          <div class="label">
            <span class="category"><i [style.background]="category.color || '#8b5cf6'"></i>{{ category.categoryName }}</span>
            <span>{{ category.percentage }}%</span>
          </div>
          <div class="bar"><div [style.width.%]="category.percentage"></div></div>
          <div class="amount">{{ category.total | currency: 'BRL' }}</div>
        </div>
      } @empty {
        <div class="empty">Sem despesas no período.</div>
      }
    </div>
  `,
  styles: [`
    .total { color: var(--green); font-size: 11px; }
    .distribution-row { padding: 8px 0; border-bottom: 1px solid #303037; }
    .distribution-row:last-child { border-bottom: none; }
    .label { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 6px; }
    .category { display: flex; align-items: center; gap: 7px; }
    .category i { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
    .bar { height: 5px; background: #2a2a31; border-radius: 4px; overflow: hidden; }
    .bar div { height: 100%; background: linear-gradient(90deg, var(--purple), var(--cyan)); }
    .amount { color: var(--muted); font-size: 10px; margin-top: 4px; }
  `]
})
export class ExpenseDistributionComponent {
  readonly categories = input<CategoryExpense[]>([]);
}
