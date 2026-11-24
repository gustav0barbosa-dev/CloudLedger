import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Tx { id:string; description:string; type:string; amount:number; occurredAt:string; status:string; accountName:string; categoryName?:string; }
interface Account { id:string; name:string; }
interface Category { id:string; name:string; kind:string; }

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="page-head"><h1>Lançamentos</h1></div>
    <div class="card">
      <div class="form-row">
        <select [(ngModel)]="accountId"><option value="">Conta</option>@for(a of accounts(); track a.id){<option [value]="a.id">{{a.name}}</option>}</select>
        <select [(ngModel)]="categoryId"><option value="">Categoria</option>@for(c of categories(); track c.id){<option [value]="c.id">{{c.name}}</option>}</select>
        <select [(ngModel)]="type"><option value="Expense">Despesa</option><option value="Income">Receita</option></select>
        <input type="number" [(ngModel)]="amount" placeholder="Valor" />
        <input [(ngModel)]="description" placeholder="Descrição" />
        <input type="date" [(ngModel)]="occurredAt" />
        <button class="btn" (click)="create()">Lançar</button>
      </div>
    </div>
    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Data</th><th>Descrição</th><th>Conta</th><th>Categoria</th><th>Tipo</th><th>Valor</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (t of txs(); track t.id) {
            <tr>
              <td>{{t.occurredAt}}</td><td>{{t.description}}</td><td>{{t.accountName}}</td><td>{{t.categoryName}}</td>
              <td>{{t.type}}</td><td>{{t.amount | number:'1.2-2'}}</td><td>{{t.status}}</td>
              <td>@if(t.status==='Confirmed'){<button class="btn danger" (click)="voidTx(t.id)">Estornar</button>}</td>
            </tr>
          }
        </tbody>
      </table>
      @if (!txs().length) { <div class="empty">Nenhum lançamento.</div> }
    </div>
  `
})
export class TransactionsComponent implements OnInit {
  private http = inject(HttpClient);
  txs = signal<Tx[]>([]); accounts = signal<Account[]>([]); categories = signal<Category[]>([]);
  accountId=''; categoryId=''; type='Expense'; amount=0; description=''; occurredAt=new Date().toISOString().slice(0,10);
  ngOnInit() { this.load(); this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`).subscribe(d=>this.accounts.set(d)); this.http.get<Category[]>(`${environment.apiBaseUrl}/categories`).subscribe(d=>this.categories.set(d)); }
  load() { this.http.get<{items:Tx[]}>(`${environment.apiBaseUrl}/transactions?page=1&pageSize=50`).subscribe(d => this.txs.set(d.items)); }
  create() {
    this.http.post<Tx>(`${environment.apiBaseUrl}/transactions`, { accountId:this.accountId, categoryId:this.categoryId||null, type:this.type, amount:this.amount, description:this.description, occurredAt:this.occurredAt })
      .subscribe(() => { this.description=''; this.amount=0; this.load(); });
  }
  voidTx(id:string) { this.http.post(`${environment.apiBaseUrl}/transactions/${id}/void`, {}).subscribe(() => this.load()); }
}