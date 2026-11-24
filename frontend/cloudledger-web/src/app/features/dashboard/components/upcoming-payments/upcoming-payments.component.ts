import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UpcomingPayment } from '../../models/dashboard.model';

@Component({
  selector: 'app-upcoming-payments',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  template: `
    <div class="card upcoming">
      <div class="card-head">
        <strong>Próximos pagamentos</strong>
        <a class="link" routerLink="/recurring">Ver recorrentes</a>
      </div>

      @if (payments().length) {
        <div class="table-scroll">
          <table class="table">
            <thead>
              <tr><th>Descrição</th><th>Conta</th><th>Vencimento</th><th>Valor</th></tr>
            </thead>
            <tbody>
              @for (payment of payments(); track payment.recurringPaymentId) {
                <tr>
                  <td>{{ payment.description }}</td>
                  <td>{{ payment.accountName }}</td>
                  <td>{{ payment.dueDate }}</td>
                  <td>{{ payment.amount | currency: 'BRL' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <div class="empty">Sem pagamentos próximos.</div>
      }
    </div>
  `,
  styles: [`
    .table-scroll { overflow-x: auto; }
  `]
})
export class UpcomingPaymentsComponent {
  readonly payments = input<UpcomingPayment[]>([]);
}
