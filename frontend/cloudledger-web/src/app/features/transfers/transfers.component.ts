import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Transfer { id:string; sourceAccountName:string; destinationAccountName:string; amount:number; occurredAt:string; status:string; description?:string; }
interface Account { id:string; name:string; }

@Component({
  selector: 'app-transfers',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="page-head"><h1>Transferências</h1></div>
    <div class="card">
      <div class="form-row">
        <select [(ngModel)]="sourceId"><option value="">Origem</option>@for(a of accounts(); track a.id){<option [value]="a.id">{{a.name}}</option>}</select>
        <select [(ngModel)]="destId"><option value="">Destino</option>@for(a of accounts(); track a.id){<option [value]="a.id">{{a.name}}</option>}</select>
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
          @for (t of transfers(); track t.id) {
            <tr><td>{{t.occurredAt}}</td><td>{{t.sourceAccountName}}</td><td>{{t.destinationAccountName}}</td><td>{{t.amount | number:'1.2-2'}}</td><td>{{t.status}}</td>
            <td>@if(t.status==='Confirmed'){<button class="btn danger" (click)="voidT(t.id)">Estornar</button>}</td></tr>
          }
        </tbody>
      </table>
      @if (!transfers().length) { <div class="empty">Nenhuma transferência.</div> }
    </div>
  `
})
export class TransfersComponent implements OnInit {
  private http = inject(HttpClient);
  transfers = signal<Transfer[]>([]); accounts = signal<Account[]>([]);
  sourceId=''; destId=''; amount=0; occurredAt=new Date().toISOString().slice(0,10); description='';
  ngOnInit() { this.load(); this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`).subscribe(d=>this.accounts.set(d)); }
  load() { this.http.get<{items:Transfer[]}>(`${environment.apiBaseUrl}/transfers?page=1&pageSize=50`).subscribe(d => this.transfers.set(d.items)); }
  create() { this.http.post<Transfer>(`${environment.apiBaseUrl}/transfers`, { sourceAccountId:this.sourceId, destinationAccountId:this.destId, amount:this.amount, occurredAt:this.occurredAt, description:this.description }).subscribe(() => { this.amount=0; this.load(); }); }
  voidT(id:string) { this.http.post(`${environment.apiBaseUrl}/transfers/${id}/void`, {}).subscribe(() => this.load()); }
}