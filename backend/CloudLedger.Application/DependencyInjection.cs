using CloudLedger.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CloudLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<TransferService>();
        services.AddScoped<RecurringPaymentService>();
        services.AddScoped<GoalService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<ReportService>();

        return services;
    }
}
