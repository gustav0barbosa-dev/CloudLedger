import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SavingGoal } from '../../models/dashboard.model';

@Component({
  selector: 'app-savings-goals',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  template: `
    <div class="aside-panel">
      <div class="card-head">
        <h3>Metas</h3>
        <a class="link" routerLink="/goals">Ver metas</a>
      </div>

      @for (goal of goals(); track goal.id) {
        <div class="goal">
          <div class="label">
            <span>{{ goal.name }}</span>
            <span>{{ goal.progressPercent }}%</span>
          </div>
          <div class="bar"><div [style.width.%]="goal.progressPercent"></div></div>
          <div class="amount">{{ goal.accumulated | currency: 'BRL' }} de {{ goal.targetAmount | currency: 'BRL' }}</div>
        </div>
      } @empty {
        <div class="empty">Nenhuma meta cadastrada.</div>
      }
    </div>
  `,
  styles: [`
    .goal { padding: 8px 0; border-bottom: 1px solid #303037; }
    .goal:last-child { border-bottom: none; }
    .label { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 6px; }
    .bar { height: 5px; background: #2a2a31; border-radius: 4px; overflow: hidden; }
    .bar div { height: 100%; background: linear-gradient(90deg, var(--green), var(--cyan)); }
    .amount { color: var(--muted); font-size: 10px; margin-top: 4px; }
  `]
})
export class SavingsGoalsComponent {
  readonly goals = input<SavingGoal[]>([]);
}
