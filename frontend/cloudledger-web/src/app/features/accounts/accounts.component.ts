import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Account { id:string; name:string; type:string; currency:string; openingBalance:number; balance:number; isArchived:boolean; }

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="page-head"><h1>Contas</h1></div>
    <div class="card">
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome da conta" />
        <select [(ngModel)]="type"><option value="Checking">Corrente</option><option value="Savings">Poupança</option><option value="Cash">Dinheiro</option><option value="Investment">Investimento</option></select>
        <input type="number" [(ngModel)]="opening" placeholder="Saldo inicial" />
        <button class="btn" (click)="create()">Criar</button>
      </div>
    </div>
    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Nome</th><th>Tipo</th><th>Moeda</th><th>Saldo inicial</th><th>Saldo</th></tr></thead>
        <tbody>
          @for (a of accounts(); track a.id) {
            <tr><td>{{a.name}}</td><td>{{a.type}}</td><td>{{a.currency}}</td><td>{{a.openingBalance | number:'1.2-2'}}</td><td>{{a.balance | number:'1.2-2'}}</td></tr>
          }
        </tbody>
      </table>
      @if (!accounts().length) { <div class="empty">Nenhuma conta cadastrada.</div> }
    </div>
  `
})
export class AccountsComponent implements OnInit {
  private http = inject(HttpClient);
  accounts = signal<Account[]>([]);
  name=''; type='Checking'; opening=0;
  ngOnInit() { this.load(); }
  load() { this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`).subscribe(d => this.accounts.set(d)); }
  create() {
    this.http.post<Account>(`${environment.apiBaseUrl}/accounts`, { name:this.name, type:this.type, openingBalance:this.opening, currency:'BRL' })
      .subscribe(() => { this.name=''; this.opening=0; this.load(); });
  }
}