import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { environment } from '../../../environments/environment';

interface Summary { from:string; to:string; totalBalance:number; income:number; expense:number; savings:number; incomeChangePercent:number; expenseChangePercent:number; savingsChangePercent:number; balanceChangePercent:number; }
interface SeriesPoint { date:string; income:number; expense:number; }
interface CatExp { categoryId?:string; categoryName:string; color?:string; total:number; count:number; percentage:number; }
interface Tx { id:string; description:string; type:string; amount:number; occurredAt:string; accountName:string; categoryName?:string; }
interface Upcoming { recurringPaymentId:string; description:string; amount:number; dueDate:string; frequency:string; accountName:string; }
interface Goal { id:string; name:string; targetAmount:number; accumulated:number; progressPercent:number; status:string; deadline?:string; }
interface Dashboard { summary:Summary; series:SeriesPoint[]; expensesByCategory:CatExp[]; recentTransactions:Tx[]; upcomingPayments:Upcoming[]; goals:Goal[]; }

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe],
  template: `
    <div class="page-head"><h1>Visão geral</h1></div>
    @if (loading()) { <div class="empty">Carregando dashboard...</div> }
    @if (error()) { <div class="empty" style="color:var(--red)">{{ error() }}</div> }
    @if (data(); as d) {
      <div class="stats">
        <div class="card"><div class="stat-label">Saldo total</div><div class="stat-value">{{ d.summary.totalBalance | currency:'BRL' }}</div><div class="change">{{ d.summary.balanceChangePercent }}%</div></div>
        <div class="card"><div class="stat-label">Receitas</div><div class="stat-value">{{ d.summary.income | currency:'BRL' }}</div><div class="change">{{ d.summary.incomeChangePercent }}%</div></div>
        <div class="card"><div class="stat-label">Despesas</div><div class="stat-value">{{ d.summary.expense | currency:'BRL' }}</div><div class="change down">{{ d.summary.expenseChangePercent }}%</div></div>
        <div class="card"><div class="stat-label">Economia</div><div class="stat-value">{{ d.summary.savings | currency:'BRL' }}</div><div class="change">{{ d.summary.savingsChangePercent }}%</div></div>
      </div>

      <div class="card chart-card">
        <div class="card-head"><strong>Evolução financeira</strong></div>
        <div class="chart-wrap">
          <svg viewBox="0 0 600 200" preserveAspectRatio="none">
            @if (d.series.length) {
              <polyline [attr.points]="incomePoints(d.series)" fill="none" stroke="var(--green)" stroke-width="2" />
              <polyline [attr.points]="expensePoints(d.series)" fill="none" stroke="var(--red)" stroke-width="2" />
            }
          </svg>
        </div>
        <div class="axis-labels"><span>{{ d.series[0]?.date }}</span><span>{{ d.series[d.series.length-1]?.date }}</span></div>
      </div>

      <div class="bottom-grid">
        <div class="card history">
          <div class="card-head"><strong>Transações recentes</strong></div>
          <table class="table">
            <thead><tr><th>Data</th><th>Descrição</th><th>Conta</th><th>Tipo</th><th>Valor</th></tr></thead>
            <tbody>
              @for (t of d.recentTransactions; track t.id) {
                <tr><td>{{t.occurredAt}}</td><td>{{t.description}}</td><td>{{t.accountName}}</td><td>{{t.type}}</td><td>{{t.amount | number:'1.2-2'}}</td></tr>
              }
            </tbody>
          </table>
          @if (!d.recentTransactions.length) { <div class="empty">Sem transações.</div> }
        </div>
        <div class="aside-panel">
          <h3>Despesas por categoria</h3>
          @for (c of d.expensesByCategory; track c.categoryName) {
            <div class="aside-row"><span>{{c.categoryName}}</span><span>{{c.percentage}}%</span></div>
          }
          @if (!d.expensesByCategory.length) { <div class="empty">Sem despesas.</div> }
        </div>
      </div>

      <div class="bottom-grid" style="margin-top:12px">
        <div class="card upcoming">
          <div class="card-head"><strong>Próximos pagamentos</strong></div>
          <table class="table">
            <thead><tr><th>Descrição</th><th>Conta</th><th>Vencimento</th><th>Valor</th></tr></thead>
            <tbody>
              @for (p of d.upcomingPayments; track p.recurringPaymentId) {
                <tr><td>{{p.description}}</td><td>{{p.accountName}}</td><td>{{p.dueDate}}</td><td>{{p.amount | number:'1.2-2'}}</td></tr>
              }
            </tbody>
          </table>
          @if (!d.upcomingPayments.length) { <div class="empty">Sem pagamentos próximos.</div> }
        </div>
        <div class="aside-panel">
          <h3>Metas</h3>
          @for (g of d.goals; track g.id) {
            <div class="aside-row"><span>{{g.name}}</span><span>{{g.progressPercent}}%</span></div>
          }
          @if (!d.goals.length) { <div class="empty">Sem metas.</div> }
        </div>
      </div>
    }
  `,
  styles: [`
    .chart-card { height: 310px; margin-bottom: 12px; padding: 16px; }
    .chart-wrap { height: 205px; position: relative; margin-top: 4px; }
    .chart-wrap svg { width:100%; height:100%; overflow:visible; }
    .axis-labels { display:flex; justify-content:space-between; color:#898894; font-size:10px; margin-top:1px; }
    .history, .upcoming { min-height: 250px; }
  `]
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  data = signal<Dashboard | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit() {
    this.http.get<Dashboard>(`${environment.apiBaseUrl}/dashboard`).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => { this.error.set('Falha ao carregar o dashboard.'); this.loading.set(false); }
    });
  }

  incomePoints(series: SeriesPoint[]): string {
    const max = Math.max(...series.map(s => Math.max(s.income, s.expense)), 1);
    return series.map((s, i) => `${(i/(series.length-1||1))*600},${200 - (s.income/max)*180}`).join(' ');
  }
  expensePoints(series: SeriesPoint[]): string {
    const max = Math.max(...series.map(s => Math.max(s.income, s.expense)), 1);
    return series.map((s, i) => `${(i/(series.length-1||1))*600},${200 - (s.expense/max)*180}`).join(' ');
  }
}