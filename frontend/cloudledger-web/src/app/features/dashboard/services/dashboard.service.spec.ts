import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { DashboardData } from '../models/dashboard.model';
import { environment } from '../../../../environments/environment';

describe('DashboardService', () => {
  let service: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('busca o dashboard consolidado na API', () => {
    const payload: DashboardData = {
      summary: {
        from: '2026-11-01', to: '2026-11-30', totalBalance: 100, income: 50,
        expense: 20, savings: 30, incomeChangePercent: 1, expenseChangePercent: 2,
        savingsChangePercent: 3, balanceChangePercent: 4
      },
      series: [],
      expensesByCategory: [],
      recentTransactions: [],
      upcomingPayments: [],
      goals: []
    };

    let result: DashboardData | undefined;
    service.getDashboard().subscribe(value => (result = value));

    http.expectOne(`${environment.apiBaseUrl}/dashboard`).flush(payload);

    expect(result?.summary.totalBalance).toBe(100);
  });
});
