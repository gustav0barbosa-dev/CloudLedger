import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Goal { id:string; name:string; targetAmount:number; accumulated:number; progressPercent:number; status:string; deadline?:string; }
interface Account { id:string; name:string; }

@Component({
  selector: 'app-goals',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="page-head"><h1>Metas de economia</h1></div>
    <div class="card">
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome da meta" />
        <input type="number" [(ngModel)]="target" placeholder="Valor alvo" />
        <input type="date" [(ngModel)]="deadline" />
        <select [(ngModel)]="accountId"><option value="">Sem conta</option>@for(a of accounts(); track a.id){<option [value]="a.id">{{a.name}}</option>}</select>
        <button class="btn" (click)="create()">Criar meta</button>
      </div>
    </div>
    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Meta</th><th>Alvo</th><th>Acumulado</th><th>Progresso</th><th>Prazo</th><th>Status</th></tr></thead>
        <tbody>
          @for (g of goals(); track g.id) {
            <tr><td>{{g.name}}</td><td>{{g.targetAmount | number:'1.2-2'}}</td><td>{{g.accumulated | number:'1.2-2'}}</td><td>{{g.progressPercent}}%</td><td>{{g.deadline}}</td><td>{{g.status}}</td></tr>
          }
        </tbody>
      </table>
      @if (!goals().length) { <div class="empty">Nenhuma meta.</div> }
    </div>
  `
})
export class GoalsComponent implements OnInit {
  private http = inject(HttpClient);
  goals = signal<Goal[]>([]); accounts = signal<Account[]>([]);
  name=''; target=0; deadline=''; accountId='';
  ngOnInit() { this.load(); this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`).subscribe(d=>this.accounts.set(d)); }
  load() { this.http.get<Goal[]>(`${environment.apiBaseUrl}/goals`).subscribe(d => this.goals.set(d)); }
  create() { this.http.post<Goal>(`${environment.apiBaseUrl}/goals`, { name:this.name, targetAmount:this.target, deadline:this.deadline||null, accountId:this.accountId||null }).subscribe(() => { this.name=''; this.target=0; this.load(); }); }
}