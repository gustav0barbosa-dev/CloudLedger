import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Recurring {
  id: string;
  description: string;
  amount: number;
  frequency: string;
  nextDueDate: string;
  isActive: boolean;
  accountId: string;
  categoryId?: string;
  accountName: string;
}

interface Occurrence {
  recurringPaymentId: string;
  dueDate: string;
  status: string;
  transactionId?: string;
  paidAt?: string;
}
interface Account { id: string; name: string }
interface Category { id: string; name: string; kind: string }

@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  template: `
    <div class="page-head">
      <h1>Pagamentos futuros</h1>
      <label class="filter"><input type="checkbox" [(ngModel)]="activeOnly" (ngModelChange)="load()" /> Somente ativos</label>
    </div>

    <div class="card">
      <div class="card-head"><strong>Nova recorrência</strong></div>
      <div class="form-row">
        <select [(ngModel)]="accountId"><option value="">Conta</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <select [(ngModel)]="categoryId"><option value="">Sem categoria</option>@for (category of categories(); track category.id) {<option [value]="category.id">{{ category.name }}</option>}</select>
        <input [(ngModel)]="description" placeholder="Descrição" />
        <input type="number" [(ngModel)]="amount" placeholder="Valor" />
        <select [(ngModel)]="frequency"><option value="Monthly">Mensal</option><option value="Weekly">Semanal</option><option value="Yearly">Anual</option></select>
        <input type="date" [(ngModel)]="nextDueDate" />
        <button class="btn" (click)="create()">Criar</button>
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Descrição</th><th>Conta</th><th>Valor</th><th>Frequência</th><th>Próx. vencimento</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (item of items(); track item.id) {
            <tr>
              <td>{{ item.description }}</td><td>{{ item.accountName }}</td>
              <td>{{ item.amount | currency: 'BRL' }}</td><td>{{ item.frequency }}</td>
              <td>{{ item.nextDueDate }}</td><td>{{ item.isActive ? 'Ativo' : 'Inativo' }}</td>
              <td>
                <button class="link" (click)="toggleOccurrences(item)">{{ expanded()[item.id] ? 'Ocultar' : 'Ocorrências' }}</button>
                <button class="btn secondary small" (click)="toggleActive(item)">{{ item.isActive ? 'Inativar' : 'Ativar' }}</button>
              </td>
            </tr>
            @if (expanded()[item.id]) {
              <tr class="details">
                <td colspan="7">
                  <table class="table inner">
                    <thead><tr><th>Vencimento</th><th>Status</th><th>Pago em</th><th></th></tr></thead>
                    <tbody>
                      @for (occurrence of occurrences()[item.id] || []; track occurrence.dueDate) {
                        <tr>
                          <td>{{ occurrence.dueDate }}</td><td>{{ occurrence.status }}</td>
                          <td>{{ occurrence.paidAt || '—' }}</td>
                          <td>
                            @if (occurrence.status === 'Pending') {
                              <button class="btn secondary small" (click)="pay(item, occurrence)">Pagar</button>
                              <button class="btn danger small" (click)="skip(item, occurrence)">Ignorar</button>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                  @if (!(occurrences()[item.id] || []).length) { <div class="empty">Sem ocorrências no período.</div> }
                </td>
              </tr>
            }
          }
        </tbody>
      </table>
      @if (!items().length) { <div class="empty">Nenhum pagamento recorrente.</div> }
    </div>
  `,
  styles: [`
    .filter { font-size:12px; color:var(--muted); display:flex; align-items:center; gap:8px; }
    .small { padding:5px 9px; font-size:11px; margin-left:5px; }
    .details td { background:#111114; }
    .inner { margin-top:0; }
  `]
})
export class RecurringComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly items = signal<Recurring[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly expanded = signal<Record<string, boolean>>({});
  readonly occurrences = signal<Record<string, Occurrence[]>>({});

  accountId = ''; categoryId = ''; description = ''; amount = 0; frequency = 'Monthly';
  nextDueDate = new Date().toISOString().slice(0, 10);
  activeOnly = false;

  ngOnInit(): void {
    this.api.get<Account[]>('/accounts').subscribe(items => this.accounts.set(items));
    this.api.get<Category[]>('/categories').subscribe(items => this.categories.set(items.filter(c => c.kind === 'Expense')));
    this.load();
  }

  load(): void {
    this.api.get<Recurring[]>('/recurring-payments', { activeOnly: this.activeOnly }).subscribe(items => this.items.set(items));
  }

  create(): void {
    this.api.post<Recurring>('/recurring-payments', {
      accountId: this.accountId,
      categoryId: this.categoryId || null,
      description: this.description,
      amount: this.amount,
      frequency: this.frequency,
      nextDueDate: this.nextDueDate
    }).subscribe(() => {
      this.toasts.success('Pagamento recorrente criado.');
      this.description = '';
      this.amount = 0;
      this.load();
    });
  }

  toggleActive(item: Recurring): void {
    this.api.put<Recurring>(`/recurring-payments/${item.id}`, {
      accountId: item.accountId,
      categoryId: item.categoryId || null,
      description: item.description,
      amount: item.amount,
      frequency: item.frequency,
      nextDueDate: item.nextDueDate,
      isActive: !item.isActive
    }).subscribe(() => {
      this.toasts.success(item.isActive ? 'Recorrência inativada.' : 'Recorrência ativada.');
      this.load();
    });
  }

  toggleOccurrences(item: Recurring): void {
    const isExpanded = !!this.expanded()[item.id];
    this.expanded.update(state => ({ ...state, [item.id]: !isExpanded }));
    if (!isExpanded) {
      this.loadOccurrences(item.id);
    }
  }

  private loadOccurrences(id: string): void {
    this.api.get<Occurrence[]>(`/recurring-payments/${id}/occurrences`).subscribe(items => {
      this.occurrences.update(state => ({ ...state, [id]: items }));
    });
  }

  pay(item: Recurring, occurrence: Occurrence): void {
    this.api.post(`/recurring-payments/${item.id}/occurrences/${occurrence.dueDate}/pay`).subscribe(() => {
      this.toasts.success('Ocorrência paga. Lançamento gerado.');
      this.loadOccurrences(item.id);
    });
  }

  skip(item: Recurring, occurrence: Occurrence): void {
    this.api.post(`/recurring-payments/${item.id}/occurrences/${occurrence.dueDate}/skip`).subscribe(() => {
      this.toasts.success('Ocorrência ignorada.');
      this.loadOccurrences(item.id);
    });
  }
}
