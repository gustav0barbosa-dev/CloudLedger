import { Component, computed, input, signal } from '@angular/core';
import { SeriesPoint } from '../../models/dashboard.model';

@Component({
  selector: 'app-financial-chart',
  standalone: true,
  template: `
    <div class="card chart-card">
      <div class="card-head">
        <strong>Evolução financeira</strong>
        <div class="chart-actions">
          @for (option of options; track option.value) {
            <button
              type="button"
              class="period-btn"
              [class.active]="period() === option.value"
              (click)="period.set(option.value)">
              {{ option.label }}
            </button>
          }
        </div>
      </div>

      <div class="legend">
        <span><i class="swatch income"></i> Receitas</span>
        <span><i class="swatch expense"></i> Despesas</span>
      </div>

      @let points = visible();
      @if (points.length) {
        <div class="chart-wrap">
          <svg viewBox="0 0 600 200" preserveAspectRatio="none" role="img" aria-label="Gráfico de evolução financeira">
            <polygon [attr.points]="areaPoints(points, 'income')" fill="rgba(67,224,173,.12)" />
            <polygon [attr.points]="areaPoints(points, 'expense')" fill="rgba(255,112,125,.10)" />
            <polyline [attr.points]="linePoints(points, 'income')" fill="none" stroke="var(--green)" stroke-width="2" />
            <polyline [attr.points]="linePoints(points, 'expense')" fill="none" stroke="var(--red)" stroke-width="2" />
            @for (point of points; track point.date) {
              <circle [attr.cx]="x(point, points)" [attr.cy]="y(point.income, points)" r="2.5" fill="var(--green)">
                <title>{{ point.date }} — receita {{ point.income }}</title>
              </circle>
              <circle [attr.cx]="x(point, points)" [attr.cy]="y(point.expense, points)" r="2.5" fill="var(--red)">
                <title>{{ point.date }} — despesa {{ point.expense }}</title>
              </circle>
            }
          </svg>
        </div>
        <div class="axis-labels">
          <span>{{ points[0].date }}</span>
          <span>{{ points[points.length - 1].date }}</span>
        </div>
      } @else {
        <div class="empty chart-empty">Sem movimentações no período selecionado.</div>
      }
    </div>
  `,
  styles: [`
    .chart-card { height: 330px; margin-bottom: 12px; padding: 16px; }
    .chart-actions { display: flex; gap: 6px; }
    .period-btn { background: #222226; border: 1px solid #34343b; color: var(--muted); border-radius: 6px; font-size: 11px; padding: 5px 9px; }
    .period-btn.active { background: var(--purple); border-color: var(--purple); color: #fff; }
    .legend { display: flex; gap: 16px; font-size: 11px; color: var(--muted); margin-bottom: 6px; }
    .swatch { display: inline-block; width: 8px; height: 8px; border-radius: 2px; margin-right: 6px; }
    .swatch.income { background: var(--green); }
    .swatch.expense { background: var(--red); }
    .chart-wrap { height: 220px; }
    .chart-wrap svg { width: 100%; height: 100%; overflow: visible; }
    .axis-labels { display: flex; justify-content: space-between; color: var(--muted); font-size: 10px; }
    .chart-empty { display: grid; place-items: center; height: 220px; }
  `]
})
export class FinancialChartComponent {
  readonly series = input<SeriesPoint[]>([]);
  readonly period = signal<number>(30);

  readonly options = [
    { label: '7 dias', value: 7 },
    { label: '30 dias', value: 30 },
    { label: '90 dias', value: 90 },
    { label: 'Tudo', value: 0 }
  ];

  readonly visible = computed(() => {
    const all = this.series();
    const days = this.period();
    return days === 0 ? all : all.slice(-days);
  });

  private max(points: SeriesPoint[]): number {
    return Math.max(...points.map(point => Math.max(point.income, point.expense)), 1);
  }

  x(point: SeriesPoint, points: SeriesPoint[]): number {
    return (points.indexOf(point) / Math.max(points.length - 1, 1)) * 600;
  }

  y(value: number, points: SeriesPoint[]): number {
    return 200 - (value / this.max(points)) * 180;
  }

  linePoints(points: SeriesPoint[], field: 'income' | 'expense'): string {
    return points
      .map(point => `${this.x(point, points)},${this.y(point[field], points)}`)
      .join(' ');
  }

  areaPoints(points: SeriesPoint[], field: 'income' | 'expense'): string {
    return `0,200 ${this.linePoints(points, field)} 600,200`;
  }
}
