import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Transfer {
  id: string;
  sourceAccountName: string;
  destinationAccountName: string;
  amount: number;
  occurredAt: string;
  status: string;
  description?: string;
}
interface Account { id: string; name: string }

@Component({
  selector: 'app-transfers',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  template: `
    <div class="page-head"><h1>Transferências</h1></div>

    <div class="card">
      <div class="card-head"><strong>Nova transferência simulada</strong></div>
      <div class="form-row">
        <select [(ngModel)]="sourceId"><option value="">Origem</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <select [(ngModel)]="destId"><option value="">Destino</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <input type="number" [(ngModel)]="amount" placeholder="Valor" />
        <input type="date" [(ngModel)]="occurredAt" />
        <input [(ngModel)]="description" placeholder="Descrição" />
        <button class="btn" (click)="create()">Transferir</button>
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Data</th><th>Origem</th><th>Destino</th><th>Valor</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (transfer of transfers(); track transfer.id) {
            <tr>
              <td>{{ transfer.occurredAt }}</td>
              <td>{{ transfer.sourceAccountName }}</td><td>{{ transfer.destinationAccountName }}</td>
              <td>{{ transfer.amount | currency: 'BRL' }}</td><td>{{ transfer.status }}</td>
              <td>@if (transfer.status === 'Confirmed') { <button class="btn danger small" (click)="voidTransfer(transfer.id)">Estornar</button> }</td>
            </tr>
          }
        </tbody>
      </table>
      @if (!transfers().length) { <div class="empty">Nenhuma transferência.</div> }
    </div>
  `,
  styles: [`.small { padding:5px 9px; font-size:11px; }`]
})
export class TransfersComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly transfers = signal<Transfer[]>([]);
  readonly accounts = signal<Account[]>([]);

  sourceId = ''; destId = ''; amount = 0; description = '';
  occurredAt = new Date().toISOString().slice(0, 10);

  ngOnInit(): void {
    this.api.get<Account[]>('/accounts').subscribe(items => this.accounts.set(items));
    this.load();
  }

  load(): void {
    this.api.get<{ items: Transfer[] }>('/transfers?page=1&pageSize=50').subscribe(page => this.transfers.set(page.items));
  }

  create(): void {
    this.api.post<Transfer>('/transfers', {
      sourceAccountId: this.sourceId,
      destinationAccountId: this.destId,
      amount: this.amount,
      occurredAt: this.occurredAt,
      description: this.description || null
    }).subscribe(() => {
      this.toasts.success('Transferência registrada.');
      this.amount = 0;
      this.description = '';
      this.load();
    });
  }

  voidTransfer(id: string): void {
    this.api.post(`/transfers/${id}/void`).subscribe(() => {
      this.toasts.success('Transferência estornada.');
      this.load();
    });
  }
}
