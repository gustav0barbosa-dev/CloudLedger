using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CloudLedger.IntegrationTests;

public sealed class CloudLedgerWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"cloudledger-test-{Guid.NewGuid():N}";

    public CloudLedgerWebApplicationFactory()
    {
        // Garante que as configurações de teste prevaleçam sobre appsettings.Development.json.
        Environment.SetEnvironmentVariable("Database__Provider", "InMemory");
        Environment.SetEnvironmentVariable("Database__InMemoryDatabaseName", DatabaseName);
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
        Environment.SetEnvironmentVariable("Mongo__Enabled", "false");
        Environment.SetEnvironmentVariable("Mongo__ConnectionString", "");
        Environment.SetEnvironmentVariable("Aws__Enabled", "false");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "1000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
                ["Database:InMemoryDatabaseName"] = DatabaseName,
                ["Database:AutoMigrate"] = "true",
                ["Mongo:Enabled"] = "false",
                ["Mongo:ConnectionString"] = "",
                ["Aws:Enabled"] = "false",
                ["RateLimiting:Auth:PermitLimit"] = "1000"
            };

            config.AddInMemoryCollection(settings);
        });
    }
}