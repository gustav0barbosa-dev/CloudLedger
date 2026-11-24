import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface ReportExport {
  fileName: string;
  url: string;
  expiresAtUtc: string;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page-head"><h1>Relatórios</h1></div>

    <div class="card">
      <div class="card-head"><strong>Exportar lançamentos</strong></div>
      <p class="muted">Gera um arquivo CSV com os lançamentos confirmados do período.</p>
      <div class="form-row">
        <input type="date" [(ngModel)]="from" title="De" />
        <input type="date" [(ngModel)]="to" title="Até" />
        <button class="btn" (click)="exportCsv()">Exportar CSV</button>
      </div>

      @if (export(); as report) {
        <div class="result">
          <strong>{{ report.fileName }}</strong>
          <span>Disponível até {{ report.expiresAtUtc | date: 'dd/MM/yyyy HH:mm' }}</span>
          <a class="btn secondary" [href]="downloadUrl(report.url)" download>Baixar arquivo</a>
        </div>
      }
    </div>
  `,
  styles: [`
    .muted { color:var(--muted); font-size:12px; }
    .result { display:flex; align-items:center; gap:14px; flex-wrap:wrap; background:#1b1b20; border:1px solid var(--line); border-radius:7px; padding:12px; margin-top:10px; font-size:12px; }
    .result span { color:var(--muted); }
  `]
})
export class ReportsComponent {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly export = signal<ReportExport | null>(null);

  from = new Date(Date.UTC(new Date().getFullYear(), new Date().getMonth(), 1)).toISOString().slice(0, 10);
  to = new Date().toISOString().slice(0, 10);

  exportCsv(): void {
    this.api.post<ReportExport>('/reports/transactions/export', {}, {
      from: this.from,
      to: this.to
    }).subscribe({
      next: result => {
        this.export.set(result);
        this.toasts.success('Relatório gerado com sucesso.');
      }
    });
  }

  downloadUrl(url: string): string {
    if (url.startsWith('http') || url.startsWith('/')) {
      return url;
    }
    return `/${url}`;
  }
}
