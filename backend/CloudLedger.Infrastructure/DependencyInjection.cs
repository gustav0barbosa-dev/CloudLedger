using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using CloudLedger.Application;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Options;
using CloudLedger.Infrastructure.Auditing;
using CloudLedger.Infrastructure.Aws;
using CloudLedger.Infrastructure.Persistence;
using CloudLedger.Infrastructure.Persistence.Repositories;
using CloudLedger.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CloudLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var provider = configuration["Database:Provider"] ?? "Postgres";

            if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                var databaseName = configuration["Database:InMemoryDatabaseName"];
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    databaseName = "cloudledger";
                }
                options.UseInMemoryDatabase(databaseName);
                return;
            }

            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqliteConnection = configuration.GetConnectionString("Sqlite") ?? "Data Source=cloudledger.db";
                options.UseSqlite(sqliteConnection);
                return;
            }

            var connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IAccountBalanceReader, AccountBalanceReader>();
        services.AddScoped<IRecurringPaymentRepository, RecurringPaymentRepository>();
        services.AddScoped<IRecurringOccurrenceRepository, RecurringOccurrenceRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        AddAuditing(services, configuration);
        AddAwsAdapters(services, configuration);

        return services;
    }

    private static void AddAuditing(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>() ?? new MongoOptions();

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddSingleton<InMemoryAuditStore>();
            services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<InMemoryAuditStore>());
            services.AddSingleton<IAuditReader>(sp => sp.GetRequiredService<InMemoryAuditStore>());
            return;
        }

        // Timeouts curtos: a auditoria é best-effort e nunca deve segurar uma requisição
        // por dezenas de segundos quando o MongoDB está indisponível.
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        settings.ConnectTimeout = TimeSpan.FromSeconds(2);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        services.AddSingleton<IMongoClient>(_ => new MongoClient(settings));
        services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(options.Database));
        services.AddSingleton<MongoAuditStore>();
        services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<MongoAuditStore>());
        services.AddSingleton<IAuditReader>(sp => sp.GetRequiredService<MongoAuditStore>());
    }

    private static void AddAwsAdapters(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AwsOptions.SectionName);
        var options = section.Get<AwsOptions>() ?? new AwsOptions();

        if (!options.Enabled)
        {
            services.AddSingleton<IDomainEventPublisher, LoggingDomainEventPublisher>();
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            services.AddSingleton<IReportStorage, LocalReportStorage>();
            return;
        }

        var credentials = AwsClientFactory.Credentials(options);

        services.AddSingleton<IAmazonSQS>(_ => credentials is null
            ? new AmazonSQSClient(AwsClientFactory.SqsConfig(options))
            : new AmazonSQSClient(credentials, AwsClientFactory.SqsConfig(options)));

        services.AddSingleton<IAmazonDynamoDB>(_ => credentials is null
            ? new AmazonDynamoDBClient(AwsClientFactory.DynamoConfig(options))
            : new AmazonDynamoDBClient(credentials, AwsClientFactory.DynamoConfig(options)));

        services.AddSingleton<IAmazonS3>(_ => credentials is null
            ? new AmazonS3Client(AwsClientFactory.S3Config(options))
            : new AmazonS3Client(credentials, AwsClientFactory.S3Config(options)));

        services.AddSingleton<IDomainEventPublisher, SqsDomainEventPublisher>();
        services.AddSingleton<IIdempotencyStore, DynamoIdempotencyStore>();
        services.AddSingleton<IReportStorage, S3ReportStorage>();
    }
}
