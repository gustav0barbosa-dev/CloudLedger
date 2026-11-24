import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Tx {
  id: string;
  description: string;
  type: string;
  amount: number;
  occurredAt: string;
  status: string;
  accountName: string;
  categoryName?: string;
}

interface Account { id: string; name: string }
interface Category { id: string; name: string; kind: string }
interface Paged<T> { items: T[]; page: number; pageSize: number; total: number }

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  template: `
    <div class="page-head"><h1>Lançamentos</h1></div>

    <div class="card">
      <div class="card-head"><strong>Novo lançamento</strong></div>
      <div class="form-row">
        <select [(ngModel)]="accountId"><option value="">Conta</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <select [(ngModel)]="categoryId"><option value="">Sem categoria</option>@for (category of categories(); track category.id) {<option [value]="category.id">{{ category.name }}</option>}</select>
        <select [(ngModel)]="type"><option value="Expense">Despesa</option><option value="Income">Receita</option></select>
        <input type="number" [(ngModel)]="amount" placeholder="Valor" />
        <input [(ngModel)]="description" placeholder="Descrição" />
        <input type="date" [(ngModel)]="occurredAt" />
        <button class="btn" (click)="create()">Lançar</button>
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <div class="card-head"><strong>Histórico</strong></div>
      <div class="form-row">
        <input type="date" [(ngModel)]="from" title="De" />
        <input type="date" [(ngModel)]="to" title="Até" />
        <select [(ngModel)]="filterType">
          <option value="">Todos os tipos</option>
          <option value="Income">Receitas</option>
          <option value="Expense">Despesas</option>
        </select>
        <select [(ngModel)]="filterAccount"><option value="">Todas as contas</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <select [(ngModel)]="filterCategory"><option value="">Todas as categorias</option>@for (category of categories(); track category.id) {<option [value]="category.id">{{ category.name }}</option>}</select>
        <input [(ngModel)]="query" placeholder="Buscar descrição" />
        <button class="btn secondary" (click)="search()">Filtrar</button>
      </div>

      <table class="table">
        <thead><tr><th>Data</th><th>Descrição</th><th>Conta</th><th>Categoria</th><th>Tipo</th><th>Valor</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (tx of txs(); track tx.id) {
            <tr>
              <td>{{ tx.occurredAt }}</td><td>{{ tx.description }}</td><td>{{ tx.accountName }}</td>
              <td>{{ tx.categoryName || '—' }}</td><td>{{ tx.type }}</td>
              <td>{{ tx.amount | currency: 'BRL' }}</td><td>{{ tx.status }}</td>
              <td>@if (tx.status === 'Confirmed') { <button class="btn danger small" (click)="voidTx(tx.id)">Estornar</button> }</td>
            </tr>
          }
        </tbody>
      </table>
      @if (!txs().length) { <div class="empty">Nenhum lançamento encontrado.</div> }

      @if (total() > pageSize) {
        <div class="pager">
          <button class="btn secondary" [disabled]="page === 1" (click)="changePage(-1)">Anterior</button>
          <span>Página {{ page }} de {{ totalPages() }}</span>
          <button class="btn secondary" [disabled]="page >= totalPages()" (click)="changePage(1)">Próxima</button>
        </div>
      }
    </div>
  `,
  styles: [`.small { padding:5px 9px; font-size:11px; } .pager { display:flex; gap:12px; align-items:center; justify-content:flex-end; margin-top:14px; font-size:12px; }`]
})
export class TransactionsComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly txs = signal<Tx[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly total = signal(0);

  accountId = ''; categoryId = ''; type = 'Expense'; amount = 0; description = '';
  occurredAt = new Date().toISOString().slice(0, 10);

  from = ''; to = ''; filterType = ''; filterAccount = ''; filterCategory = ''; query = '';
  page = 1;
  readonly pageSize = 20;

  ngOnInit(): void {
    this.api.get<Account[]>('/accounts').subscribe(items => this.accounts.set(items));
    this.api.get<Category[]>('/categories').subscribe(items => this.categories.set(items));
    this.load();
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  search(): void {
    this.page = 1;
    this.load();
  }

  changePage(delta: number): void {
    this.page += delta;
    this.load();
  }

  load(): void {
    this.api.get<Paged<Tx>>('/transactions', {
      from: this.from || undefined,
      to: this.to || undefined,
      type: this.filterType || undefined,
      accountId: this.filterAccount || undefined,
      categoryId: this.filterCategory || undefined,
      q: this.query || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe(result => {
      this.txs.set(result.items);
      this.total.set(result.total);
    });
  }

  create(): void {
    this.api.post<Tx>('/transactions', {
      accountId: this.accountId,
      categoryId: this.categoryId || null,
      type: this.type,
      amount: this.amount,
      description: this.description,
      occurredAt: this.occurredAt
    }).subscribe(() => {
      this.toasts.success('Lançamento registrado.');
      this.description = '';
      this.amount = 0;
      this.load();
    });
  }

  voidTx(id: string): void {
    this.api.post(`/transactions/${id}/void`).subscribe(() => {
      this.toasts.success('Lançamento estornado.');
      this.load();
    });
  }
}
