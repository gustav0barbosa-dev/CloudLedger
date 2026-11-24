import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RecentTransaction } from '../../models/dashboard.model';

@Component({
  selector: 'app-transaction-history',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  template: `
    <div class="card history">
      <div class="card-head">
        <strong>Transações recentes</strong>
        <a class="link" routerLink="/transactions">Ver histórico</a>
      </div>

      @if (transactions().length) {
        <div class="table-scroll">
          <table class="table">
            <thead>
              <tr><th>Data</th><th>Descrição</th><th>Conta</th><th>Tipo</th><th>Valor</th></tr>
            </thead>
            <tbody>
              @for (transaction of transactions(); track transaction.id) {
                <tr>
                  <td>{{ transaction.occurredAt }}</td>
                  <td>{{ transaction.description }}</td>
                  <td>{{ transaction.accountName }}</td>
                  <td><span class="tag" [class.income]="transaction.type === 'Income'" [class.expense]="transaction.type === 'Expense'">{{ transaction.type }}</span></td>
                  <td>{{ transaction.amount | currency: 'BRL' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <div class="empty">Nenhum lançamento recente.</div>
      }
    </div>
  `,
  styles: [`
    .table-scroll { overflow-x: auto; }
    .tag { padding: 3px 7px; border-radius: 5px; font-size: 10px; border: 1px solid var(--line); }
    .tag.income { color: var(--green); border-color: var(--green); }
    .tag.expense { color: var(--red); border-color: var(--red); }
  `]
})
export class TransactionHistoryComponent {
  readonly transactions = input<RecentTransaction[]>([]);
}
