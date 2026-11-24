import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { AuthService } from '../../core/auth.service';
import { environment } from '../../../environments/environment';

interface Summary { totalBalance: number; totalIncome: number; totalExpense: number; }
interface Dashboard { summary: Summary; }

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="shell">
      <aside class="sidebar">
        <h2>CloudLedger</h2>
        <nav>
          <a routerLink="/dashboard">Dashboard</a>
          <a routerLink="/accounts">Contas</a>
          <a routerLink="/transactions">Lançamentos</a>
        </nav>
        <button (click)="logout()">Sair</button>
      </aside>
      <main>
        <h1>Dashboard</h1>
        @if (data(); as d) {
          <div class="cards">
            <div class="card"><span>Saldo total</span><strong>{{ d.summary.totalBalance | currency:'BRL' }}</strong></div>
            <div class="card"><span>Receitas</span><strong>{{ d.summary.totalIncome | currency:'BRL' }}</strong></div>
            <div class="card"><span>Despesas</span><strong>{{ d.summary.totalExpense | currency:'BRL' }}</strong></div>
          </div>
        } @else {
          <p>Carregando...</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .shell { display:grid; grid-template-columns:240px 1fr; min-height:100vh; }
    .sidebar { background:#0f172a; color:#e2e8f0; padding:1.5rem; display:flex; flex-direction:column; gap:1rem; }
    .sidebar h2 { margin:0 0 1rem; }
    .sidebar nav { display:flex; flex-direction:column; gap:.5rem; }
    .sidebar nav a { color:#94a3b8; text-decoration:none; padding:.5rem .75rem; border-radius:8px; }
    .sidebar nav a:hover { background:#1e293b; color:#e2e8f0; }
    .sidebar button { margin-top:auto; padding:.6rem; border:none; border-radius:8px; background:#ef4444; color:#fff; cursor:pointer; }
    main { padding:2rem; background:#f1f5f9; }
    .cards { display:grid; grid-template-columns:repeat(3,1fr); gap:1rem; }
    .card { background:#fff; padding:1.25rem; border-radius:12px; box-shadow:0 1px 3px rgba(0,0,0,.1); display:flex; flex-direction:column; gap:.5rem; }
    .card span { color:#64748b; font-size:.85rem; }
    .card strong { font-size:1.4rem; }
  `]
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  data = signal<Dashboard | null>(null);

  ngOnInit() {
    this.http.get<Dashboard>(`${environment.apiBaseUrl}/dashboard`).subscribe(d => this.data.set(d));
  }

  logout() { this.auth.logout(); }
}