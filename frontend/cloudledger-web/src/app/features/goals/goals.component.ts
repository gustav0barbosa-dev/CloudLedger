import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../../core/api-client.service';
import { ToastService } from '../../core/toast.service';

interface Goal {
  id: string;
  name: string;
  targetAmount: number;
  accumulated: number;
  progressPercent: number;
  status: string;
  deadline?: string;
  accountId?: string;
}
interface Movement {
  id: string;
  amount: number;
  type: string;
  occurredAt: string;
}
interface Account { id: string; name: string }

@Component({
  selector: 'app-goals',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  template: `
    <div class="page-head"><h1>Metas financeiras</h1></div>

    <div class="card">
      <div class="card-head"><strong>Nova meta</strong></div>
      <div class="form-row">
        <input [(ngModel)]="name" placeholder="Nome da meta" />
        <input type="number" [(ngModel)]="target" placeholder="Valor alvo" />
        <input type="date" [(ngModel)]="deadline" />
        <select [(ngModel)]="accountId"><option value="">Sem conta</option>@for (account of accounts(); track account.id) {<option [value]="account.id">{{ account.name }}</option>}</select>
        <button class="btn" (click)="create()">Criar meta</button>
      </div>
    </div>

    <div class="card" style="margin-top:12px">
      <table class="table">
        <thead><tr><th>Meta</th><th>Alvo</th><th>Acumulado</th><th>Progresso</th><th>Prazo</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (goal of goals(); track goal.id) {
            <tr>
              <td>{{ goal.name }}</td><td>{{ goal.targetAmount | currency: 'BRL' }}</td>
              <td>{{ goal.accumulated | currency: 'BRL' }}</td><td>{{ goal.progressPercent }}%</td>
              <td>{{ goal.deadline || '—' }}</td><td>{{ goal.status }}</td>
              <td>
                <button class="link" (click)="toggleMovements(goal)">{{ expanded()[goal.id] ? 'Ocultar' : 'Movimentos' }}</button>
                @if (goal.status === 'Active') {
                  <button class="btn danger small" (click)="cancel(goal)">Cancelar meta</button>
                }
              </td>
            </tr>
            @if (expanded()[goal.id]) {
              <tr class="details">
                <td colspan="7">
                  <div class="form-row">
                    <input type="number" [(ngModel)]="movementAmount[goal.id]" placeholder="Valor" />
                    <input type="date" [(ngModel)]="movementDate[goal.id]" />
                    <button class="btn secondary small" (click)="move(goal, 'Deposit')">Aportar</button>
                    <button class="btn danger small" (click)="move(goal, 'Withdrawal')">Retirar</button>
                  </div>
                  <table class="table inner">
                    <thead><tr><th>Data</th><th>Tipo</th><th>Valor</th></tr></thead>
                    <tbody>
                      @for (movement of movements()[goal.id] || []; track movement.id) {
                        <tr><td>{{ movement.occurredAt }}</td><td>{{ movement.type }}</td><td>{{ movement.amount | currency: 'BRL' }}</td></tr>
                      }
                    </tbody>
                  </table>
                  @if (!(movements()[goal.id] || []).length) { <div class="empty">Nenhum movimento registrado.</div> }
                </td>
              </tr>
            }
          }
        </tbody>
      </table>
      @if (!goals().length) { <div class="empty">Nenhuma meta.</div> }
    </div>
  `,
  styles: [`
    .small { padding:5px 9px; font-size:11px; margin-left:5px; }
    .details td { background:#111114; }
    .inner { margin-top:0; }
  `]
})
export class GoalsComponent implements OnInit {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly goals = signal<Goal[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly expanded = signal<Record<string, boolean>>({});
  readonly movements = signal<Record<string, Movement[]>>({});
  readonly movementAmount: Record<string, number> = {};
  readonly movementDate: Record<string, string> = {};

  name = ''; target = 0; deadline = ''; accountId = '';

  ngOnInit(): void {
    this.api.get<Account[]>('/accounts').subscribe(items => this.accounts.set(items));
    this.load();
  }

  load(): void {
    this.api.get<Goal[]>('/goals').subscribe(items => this.goals.set(items));
  }

  create(): void {
    this.api.post<Goal>('/goals', {
      name: this.name,
      targetAmount: this.target,
      deadline: this.deadline || null,
      accountId: this.accountId || null
    }).subscribe(() => {
      this.toasts.success('Meta criada.');
      this.name = '';
      this.target = 0;
      this.deadline = '';
      this.load();
    });
  }

  cancel(goal: Goal): void {
    if (!confirm(`Cancelar a meta "${goal.name}"?`)) {
      return;
    }
    this.api.post(`/goals/${goal.id}/cancel`).subscribe(() => {
      this.toasts.success('Meta cancelada.');
      this.load();
    });
  }

  toggleMovements(goal: Goal): void {
    const isExpanded = !!this.expanded()[goal.id];
    this.expanded.update(state => ({ ...state, [goal.id]: !isExpanded }));
    if (!this.movementDate[goal.id]) {
      this.movementDate[goal.id] = new Date().toISOString().slice(0, 10);
    }
    if (!isExpanded) {
      this.loadMovements(goal.id);
    }
  }

  private loadMovements(id: string): void {
    this.api.get<Movement[]>(`/goals/${id}/movements`).subscribe(items => {
      this.movements.update(state => ({ ...state, [id]: items }));
    });
  }

  move(goal: Goal, type: 'Deposit' | 'Withdrawal'): void {
    const amount = this.movementAmount[goal.id] || 0;
    const occurredAt = this.movementDate[goal.id] || new Date().toISOString().slice(0, 10);

    this.api.post(`/goals/${goal.id}/movements`, { amount, type, occurredAt }).subscribe(() => {
      this.toasts.success(type === 'Deposit' ? 'Aporte registrado.' : 'Retirada registrada.');
      this.movementAmount[goal.id] = 0;
      this.loadMovements(goal.id);
      this.load();
    });
  }
}
