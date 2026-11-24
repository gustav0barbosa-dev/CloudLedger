import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Recurring { id:string; description:string; amount:number; frequency:string; nextDueDate:string; isActive:boolean; accountName:string; }
interface Account { id:string; name:string; }
interface Category { id:string; name:string; kind:string; }

@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="page-head"><h1>Pagamentos recorrentes</h1></div>
    <div class="card">
      <div class="form-row">
        <select [(ngModel)]="accountId"><option value="">Conta</option>@for(a of accounts(); track a.id){<option [value]="a.id">{{a.name}}</option>}</select>
        <select [(ngModel)]="categoryId"><option value="">Categoria</option>@for(c of categories(); track c.id){<option [value]="c.id">{{c.name}}</option>}</select>
        <input [(ngModel)]="description" placeholder="Descrição" />
        <input type="number" [(ngModel)]="amount" placeholder="Valor" />
        <select [(ngModel)]="frequency"><option value="Monthly">Mensal</option><option value="Weekly">Semanal</option><option value="Yearly">Anual</option></select>
        <input type="date" [(ngModel)]="nextDueDate" />
        <button class="btn" (click)="create()">Criar</button>
      </div>
    </div>
    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Descrição</th><th>Conta</th><th>Valor</th><th>Frequência</th><th>Próx. vencimento</th><th>Status</th></tr></thead>
        <tbody>
          @for (r of items(); track r.id) {
            <tr><td>{{r.description}}</td><td>{{r.accountName}}</td><td>{{r.amount | number:'1.2-2'}}</td><td>{{r.frequency}}</td><td>{{r.nextDueDate}}</td><td>{{r.isActive?'Ativo':'Inativo'}}</td></tr>
          }
        </tbody>
      </table>
      @if (!items().length) { <div class="empty">Nenhum pagamento recorrente.</div> }
    </div>
  `
})
export class RecurringComponent implements OnInit {
  private http = inject(HttpClient);
  items = signal<Recurring[]>([]); accounts = signal<Account[]>([]); categories = signal<Category[]>([]);
  accountId=''; categoryId=''; description=''; amount=0; frequency='Monthly'; nextDueDate=new Date().toISOString().slice(0,10);
  ngOnInit() { this.load(); this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`).subscribe(d=>this.accounts.set(d)); this.http.get<Category[]>(`${environment.apiBaseUrl}/categories`).subscribe(d=>this.categories.set(d.filter(c=>c.kind==='Expense'))); }
  load() { this.http.get<Recurring[]>(`${environment.apiBaseUrl}/recurring-payments?activeOnly=false`).subscribe(d => this.items.set(d)); }
  create() { this.http.post<Recurring>(`${environment.apiBaseUrl}/recurring-payments`, { accountId:this.accountId, categoryId:this.categoryId||null, description:this.description, amount:this.amount, frequency:this.frequency, nextDueDate:this.nextDueDate }).subscribe(() => { this.description=''; this.amount=0; this.load(); }); }
}