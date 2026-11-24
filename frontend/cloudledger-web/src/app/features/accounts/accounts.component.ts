import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Account {
  id: string;
  name: string;
  type: string;
  currency: string;
  openingBalance: number;
  balance: number;
  isArchived: boolean;
}

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  template: `
    <div class="page-head"><h1>Contas</h1></div>

    <div class="card">
      <div class="card-head"><strong>{{ editingId() ? 'Editar conta' : 'Nova conta' }}</strong></div>
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome da conta" />
        <select [(ngModel)]="type">
          <option value="Checking">Corrente</option>
          <option value="Savings">Poupança</option>
          <option value="Cash">Dinheiro</option>
          <option value="Investment">Investimento</option>
        </select>
        <input type="number" [(ngModel)]="opening" placeholder="Saldo inicial" />
        <button class="btn" (click)="save()">{{ editingId() ? 'Salvar' : 'Criar' }}</button>
        @if (editingId()) {
          <button class="btn secondary" (click)="reset()">Cancelar</button>
        }
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Nome</th><th>Tipo</th><th>Saldo inicial</th><th>Saldo</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (account of accounts(); track account.id) {
            <tr>
              <td>{{ account.name }}</td>
              <td>{{ account.type }}</td>
              <td>{{ account.openingBalance | currency: 'BRL' }}</td>
              <td>{{ account.balance | currency: 'BRL' }}</td>
              <td>{{ account.isArchived ? 'Arquivada' : 'Ativa' }}</td>
              <td>
                <button class="btn secondary small" (click)="edit(account)">Editar</button>
                <button class="btn secondary small" (click)="toggleArchive(account)">
                  {{ account.isArchived ? 'Reativar' : 'Arquivar' }}
                </button>
                <button class="btn danger small" (click)="remove(account)">Excluir</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
      @if (!accounts().length) { <div class="empty">Nenhuma conta cadastrada.</div> }
    </div>
  `,
  styles: [`.small { padding:5px 9px; font-size:11px; margin-right:5px; }`]
})
export class AccountsComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly accounts = signal<Account[]>([]);
  readonly editingId = signal<string | null>(null);

  name = '';
  type = 'Checking';
  opening = 0;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<Account[]>('/accounts').subscribe({
      next: items => this.accounts.set(items)
    });
  }

  save(): void {
    const id = this.editingId();
    const request = id
      ? { name: this.name, type: this.type, openingBalance: this.opening }
      : { name: this.name, type: this.type, openingBalance: this.opening, currency: 'BRL' };

    const operation = id
      ? this.api.put<Account>(`/accounts/${id}`, request)
      : this.api.post<Account>('/accounts', request);

    operation.subscribe({
      next: () => {
        this.toasts.success(id ? 'Conta atualizada.' : 'Conta criada.');
        this.reset();
        this.load();
      }
    });
  }

  edit(account: Account): void {
    this.editingId.set(account.id);
    this.name = account.name;
    this.type = account.type;
    this.opening = account.openingBalance;
  }

  reset(): void {
    this.editingId.set(null);
    this.name = '';
    this.type = 'Checking';
    this.opening = 0;
  }

  toggleArchive(account: Account): void {
    const path = account.isArchived ? 'unarchive' : 'archive';
    this.api.post(`/accounts/${account.id}/${path}`).subscribe({
      next: () => {
        this.toasts.success(account.isArchived ? 'Conta reativada.' : 'Conta arquivada.');
        this.load();
      }
    });
  }

  remove(account: Account): void {
    if (!confirm(`Excluir a conta "${account.name}"? Contas com movimentações devem ser arquivadas.`)) {
      return;
    }
    this.api.delete(`/accounts/${account.id}`).subscribe({
      next: () => {
        this.toasts.success('Conta excluída.');
        this.load();
      }
    });
  }
}
