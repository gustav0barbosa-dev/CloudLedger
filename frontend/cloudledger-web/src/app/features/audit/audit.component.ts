import { DatePipe, JsonPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';

interface AuditEvent {
  id: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  occurredAt: string;
  metadata: Record<string, string | null>;
}

interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [FormsModule, DatePipe, JsonPipe],
  template: `
    <div class="page-head"><h1>Auditoria</h1></div>

    <div class="card">
      <div class="form-row">
        <input type="date" [(ngModel)]="from" title="De" />
        <input type="date" [(ngModel)]="to" title="Até" />
        <input [(ngModel)]="action" placeholder="Ação (ex.: TransactionCreated)" />
        <button class="btn" (click)="search()">Filtrar</button>
        <button class="btn secondary" (click)="clear()">Limpar</button>
      </div>

      @if (error(); as message) {
        <div class="error-box">{{ message }}</div>
      }

      <table class="table">
        <thead><tr><th>Data</th><th>Ação</th><th>Recurso</th><th>Detalhes</th></tr></thead>
        <tbody>
          @for (event of events(); track event.id) {
            <tr>
              <td>{{ event.occurredAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
              <td>{{ event.action }}</td>
              <td>{{ event.resourceType }}</td>
              <td><code>{{ event.metadata | json }}</code></td>
            </tr>
          }
        </tbody>
      </table>
      @if (!events().length && !error()) { <div class="empty">Nenhum evento de auditoria encontrado.</div> }

      @if (total() > pageSize) {
        <div class="pager">
          <button class="btn secondary" [disabled]="page === 1" (click)="changePage(-1)">Anterior</button>
          <span>Página {{ page }} de {{ totalPages() }}</span>
          <button class="btn secondary" [disabled]="page >= totalPages()" (click)="changePage(1)">Próxima</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .error-box { color:var(--red); background:#2a161c; border:1px solid #5b2733; padding:10px; border-radius:7px; margin-bottom:12px; font-size:12px; }
    code { font-size:10px; color:#b9b7c1; }
    .pager { display:flex; align-items:center; gap:12px; justify-content:flex-end; margin-top:14px; font-size:12px; }
  `]
})
export class AuditComponent implements OnInit {
  private readonly api = inject(ApiClient);

  readonly events = signal<AuditEvent[]>([]);
  readonly total = signal(0);
  readonly error = signal<string | null>(null);

  from = '';
  to = '';
  action = '';
  page = 1;
  readonly pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  search(): void {
    this.page = 1;
    this.load();
  }

  clear(): void {
    this.from = '';
    this.to = '';
    this.action = '';
    this.search();
  }

  changePage(delta: number): void {
    this.page += delta;
    this.load();
  }

  load(): void {
    this.api.get<Paged<AuditEvent>>('/audit-events', {
      from: this.from ? `${this.from}T00:00:00Z` : undefined,
      to: this.to ? `${this.to}T23:59:59Z` : undefined,
      action: this.action || undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: result => {
        this.events.set(result.items);
        this.total.set(result.total);
        this.error.set(null);
      },
      error: (error: { status?: number }) => {
        this.events.set([]);
        this.error.set(error.status === 503
          ? 'O serviço de auditoria está temporariamente indisponível. Tente novamente mais tarde.'
          : 'Não foi possível consultar a auditoria.');
      }
    });
  }
}
