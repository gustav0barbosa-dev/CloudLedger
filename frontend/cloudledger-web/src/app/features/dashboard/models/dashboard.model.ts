export interface DashboardSummary {
  from: string;
  to: string;
  totalBalance: number;
  income: number;
  expense: number;
  savings: number;
  incomeChangePercent: number;
  expenseChangePercent: number;
  savingsChangePercent: number;
  balanceChangePercent: number;
}

export interface SeriesPoint {
  date: string;
  income: number;
  expense: number;
}

export interface CategoryExpense {
  categoryId?: string;
  categoryName: string;
  color?: string;
  total: number;
  count: number;
  percentage: number;
}

export interface RecentTransaction {
  id: string;
  description: string;
  type: string;
  amount: number;
  occurredAt: string;
  accountName: string;
  categoryName?: string;
}

export interface UpcomingPayment {
  recurringPaymentId: string;
  description: string;
  amount: number;
  dueDate: string;
  frequency: string;
  accountName: string;
}

export interface SavingGoal {
  id: string;
  name: string;
  targetAmount: number;
  accumulated: number;
  progressPercent: number;
  status: string;
  deadline?: string;
}

export interface DashboardData {
  summary: DashboardSummary;
  series: SeriesPoint[];
  expensesByCategory: CategoryExpense[];
  recentTransactions: RecentTransaction[];
  upcomingPayments: UpcomingPayment[];
  goals: SavingGoal[];
}
