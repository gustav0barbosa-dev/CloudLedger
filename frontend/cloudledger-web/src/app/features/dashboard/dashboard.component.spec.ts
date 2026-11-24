import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from './services/dashboard.service';
import { DashboardData } from './models/dashboard.model';

const data: DashboardData = {
  summary: {
    from: '2026-11-01', to: '2026-11-30', totalBalance: 12395, income: 4210,
    expense: 4613, savings: 3680, incomeChangePercent: 1, expenseChangePercent: 2,
    savingsChangePercent: 3, balanceChangePercent: 4
  },
  series: [],
  expensesByCategory: [],
  recentTransactions: [],
  upcomingPayments: [],
  goals: []
};

describe('DashboardComponent', () => {
  it('renderiza os indicadores retornados pela API', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: { getDashboard: () => of(data) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Visão geral');
    expect(text).toContain('Saldo total');
    expect(text).toContain('Economia');
    expect(fixture.componentInstance.data()).toEqual(data);
  });
});
