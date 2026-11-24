using CloudLedger.Application.Abstractions;
using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Goals;
using CloudLedger.Domain.Notifications;
using CloudLedger.Domain.Recurring;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using CloudLedger.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CloudLedger.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IClock clock,
        bool includeDemoData,
        CancellationToken cancellationToken = default)
    {
        await SeedDefaultCategoriesAsync(db, cancellationToken);

        if (includeDemoData)
        {
            await DemoDataSeeder.SeedAsync(db, passwordHasher, clock, cancellationToken);
        }
    }

    private static async Task SeedDefaultCategoriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(x => x.UserId == null, cancellationToken))
        {
            return;
        }

        var defaults = new (string Name, CategoryKind Kind, string Color, string Icon)[]
        {
            ("Moradia", CategoryKind.Expense, "#8B42DB", "🏠"),
            ("Alimentação", CategoryKind.Expense, "#176F72", "🍜"),
            ("Mercado", CategoryKind.Expense, "#A52B9A", "🛒"),
            ("Transporte", CategoryKind.Expense, "#3B82F6", "🚗"),
            ("Saúde", CategoryKind.Expense, "#EF4444", "💊"),
            ("Lazer", CategoryKind.Expense, "#F59E0B", "🎮"),
            ("Educação", CategoryKind.Expense, "#10B981", "📚"),
            ("Assinaturas", CategoryKind.Expense, "#6366F1", "📺"),
            ("Outros", CategoryKind.Expense, "#A77C25", "📦"),
            ("Salário", CategoryKind.Income, "#43E0AD", "💼"),
            ("Freelance", CategoryKind.Income, "#42C7DF", "💻"),
            ("Investimentos", CategoryKind.Income, "#8B5CF6", "📈"),
            ("Outros", CategoryKind.Income, "#A77C25", "➕")
        };

        foreach (var (name, kind, color, icon) in defaults)
        {
            db.Categories.Add(Category.CreateDefault(name, kind, color, icon));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Dados fictícios de demonstração. Nunca usar dados financeiros reais (RNF-002).
/// Só é executado em ambiente de desenvolvimento.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoEmail = "demo@cloudledger.dev";
    public const string DemoPassword = "Demo@12345";

    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(x => x.Email == DemoEmail, cancellationToken))
        {
            return;
        }

        var user = new User("Ana Silva", DemoEmail, passwordHasher.Hash(DemoPassword));
        db.Users.Add(user);

        var categories = await db.Categories
            .Where(x => x.UserId == null)
            .ToListAsync(cancellationToken);

        Category Cat(string name, CategoryKind kind) =>
            categories.First(x => x.Name == name && x.Kind == kind);

        var checking = new Account(user.Id, "Conta corrente", AccountType.Checking, 4500m);
        var savings = new Account(user.Id, "Reserva", AccountType.Savings, 6000m);
        var cash = new Account(user.Id, "Carteira", AccountType.Cash, 300m);
        db.Accounts.AddRange(checking, savings, cash);

        var today = clock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        Func<int, DateOnly> dayInMonth = offset => monthStart.AddDays(Math.Min(offset, Math.Max(0, today.DayNumber - monthStart.DayNumber)));

        void AddTransaction(Account account, Category category, TransactionType type, decimal amount, string description, DateOnly date) =>
            db.Transactions.Add(new Transaction(user.Id, account.Id, category.Id, type, amount, description, date));

        // Mês atual
        AddTransaction(checking, Cat("Salário", CategoryKind.Income), TransactionType.Income, 8420m, "Salário mensal", monthStart);
        AddTransaction(checking, Cat("Freelance", CategoryKind.Income), TransactionType.Income, 1200m, "Projeto freelance", dayInMonth(9));
        AddTransaction(checking, Cat("Investimentos", CategoryKind.Income), TransactionType.Income, 180m, "Rendimento CDB", dayInMonth(12));

        AddTransaction(checking, Cat("Moradia", CategoryKind.Expense), TransactionType.Expense, 1530m, "Aluguel", dayInMonth(3));
        AddTransaction(checking, Cat("Mercado", CategoryKind.Expense), TransactionType.Expense, 284.50m, "Mercado do mês", dayInMonth(6));
        AddTransaction(checking, Cat("Mercado", CategoryKind.Expense), TransactionType.Expense, 420.30m, "Mercado", dayInMonth(13));
        AddTransaction(checking, Cat("Mercado", CategoryKind.Expense), TransactionType.Expense, 215.40m, "Mercado", dayInMonth(16));
        AddTransaction(cash, Cat("Alimentação", CategoryKind.Expense), TransactionType.Expense, 50m, "Blue Bottle", dayInMonth(5));
        AddTransaction(cash, Cat("Alimentação", CategoryKind.Expense), TransactionType.Expense, 86.40m, "Restaurante", dayInMonth(7));
        AddTransaction(cash, Cat("Alimentação", CategoryKind.Expense), TransactionType.Expense, 42.10m, "Padaria", dayInMonth(14));
        AddTransaction(checking, Cat("Transporte", CategoryKind.Expense), TransactionType.Expense, 34.90m, "Uber", dayInMonth(8));
        AddTransaction(checking, Cat("Transporte", CategoryKind.Expense), TransactionType.Expense, 280m, "Combustível", dayInMonth(17));
        AddTransaction(checking, Cat("Assinaturas", CategoryKind.Expense), TransactionType.Expense, 19.90m, "Spotify", dayInMonth(9));
        AddTransaction(checking, Cat("Assinaturas", CategoryKind.Expense), TransactionType.Expense, 39.90m, "Apple", dayInMonth(10));
        AddTransaction(checking, Cat("Saúde", CategoryKind.Expense), TransactionType.Expense, 78.30m, "Farmácia", dayInMonth(11));
        AddTransaction(cash, Cat("Lazer", CategoryKind.Expense), TransactionType.Expense, 60m, "Cinema", dayInMonth(12));
        AddTransaction(cash, Cat("Lazer", CategoryKind.Expense), TransactionType.Expense, 120m, "Bar com amigos", dayInMonth(18));
        AddTransaction(checking, Cat("Educação", CategoryKind.Expense), TransactionType.Expense, 199m, "Curso online", dayInMonth(15));
        AddTransaction(checking, Cat("Outros", CategoryKind.Expense), TransactionType.Expense, 133m, "Presentes", dayInMonth(19));

        // Mês anterior — base de comparação dos indicadores
        AddTransaction(checking, Cat("Salário", CategoryKind.Income), TransactionType.Income, 6736m, "Salário mensal", previousMonthStart);
        AddTransaction(checking, Cat("Moradia", CategoryKind.Expense), TransactionType.Expense, 1530m, "Aluguel", previousMonthStart.AddDays(3));
        AddTransaction(checking, Cat("Mercado", CategoryKind.Expense), TransactionType.Expense, 980m, "Mercado", previousMonthStart.AddDays(7));
        AddTransaction(checking, Cat("Mercado", CategoryKind.Expense), TransactionType.Expense, 640m, "Mercado", previousMonthStart.AddDays(18));
        AddTransaction(cash, Cat("Alimentação", CategoryKind.Expense), TransactionType.Expense, 720m, "Alimentação", previousMonthStart.AddDays(10));
        AddTransaction(checking, Cat("Transporte", CategoryKind.Expense), TransactionType.Expense, 480m, "Transporte", previousMonthStart.AddDays(12));
        AddTransaction(checking, Cat("Lazer", CategoryKind.Expense), TransactionType.Expense, 410m, "Lazer", previousMonthStart.AddDays(20));
        AddTransaction(checking, Cat("Saúde", CategoryKind.Expense), TransactionType.Expense, 294m, "Saúde", previousMonthStart.AddDays(22));
        AddTransaction(checking, Cat("Outros", CategoryKind.Expense), TransactionType.Expense, 200m, "Outros", previousMonthStart.AddDays(25));

        db.Transfers.Add(new Transfer(user.Id, checking.Id, savings.Id, 800m, dayInMonth(20), "Reserva do mês"));

        db.RecurringPayments.AddRange(
            new RecurringPayment(user.Id, checking.Id, Cat("Assinaturas", CategoryKind.Expense).Id, "10X Designers", 17m, RecurrenceFrequency.Monthly, today.AddDays(1)),
            new RecurringPayment(user.Id, checking.Id, Cat("Assinaturas", CategoryKind.Expense).Id, "Apple", 41m, RecurrenceFrequency.Monthly, today.AddDays(3)),
            new RecurringPayment(user.Id, checking.Id, Cat("Assinaturas", CategoryKind.Expense).Id, "Spotify", 19.90m, RecurrenceFrequency.Monthly, today.AddDays(4)),
            new RecurringPayment(user.Id, checking.Id, Cat("Outros", CategoryKind.Expense).Id, "PetPlace", 139m, RecurrenceFrequency.Monthly, today.AddDays(7)),
            new RecurringPayment(user.Id, checking.Id, Cat("Moradia", CategoryKind.Expense).Id, "Internet", 120m, RecurrenceFrequency.Monthly, today.AddDays(10)));

        var trip = new SavingGoal(user.Id, "Viagem de férias", 2000m, today.AddMonths(7), savings.Id);
        var car = new SavingGoal(user.Id, "Carro novo", 50000m, today.AddMonths(18), savings.Id);
        db.SavingGoals.AddRange(trip, car);

        db.GoalMovements.AddRange(
            new GoalMovement(trip.Id, 1200m, GoalMovementType.Deposit, monthStart.AddDays(2)),
            new GoalMovement(trip.Id, 258m, GoalMovementType.Withdrawal, monthStart.AddDays(11)),
            new GoalMovement(car.Id, 30000m, GoalMovementType.Deposit, previousMonthStart.AddDays(4)),
            new GoalMovement(car.Id, 2400m, GoalMovementType.Deposit, monthStart.AddDays(6)));

        db.Notifications.AddRange(
            new Notification(user.Id, "Bem-vindo ao CloudLedger", "Sua conta de demonstração está pronta. Explore o dashboard e os lançamentos.", NotificationType.Info),
            new Notification(user.Id, "Pagamento próximo", $"Spotify vence em {today.AddDays(4):dd/MM/yyyy}.", NotificationType.UpcomingPayment, nameof(RecurringPayment), null));

        await db.SaveChangesAsync(cancellationToken);
    }
}
