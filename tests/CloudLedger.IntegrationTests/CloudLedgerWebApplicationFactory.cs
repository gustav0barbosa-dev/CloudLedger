using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CloudLedger.IntegrationTests;

public sealed class CloudLedgerWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"cloudledger-test-{Guid.NewGuid():N}";

    public CloudLedgerWebApplicationFactory()
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = "InMemory";
            Environment.SetEnvironmentVariable("Database__Provider", provider);
        }

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("Database__InMemoryDatabaseName", DatabaseName);
        }

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
            var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "InMemory";
            var settings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider,
                ["Database:AutoMigrate"] = "true",
                ["Mongo:Enabled"] = "false",
                ["Mongo:ConnectionString"] = "",
                ["Aws:Enabled"] = "false",
                ["RateLimiting:Auth:PermitLimit"] = "1000"
            };

            if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                settings["Database:InMemoryDatabaseName"] = DatabaseName;
            }

            config.AddInMemoryCollection(settings);
        });
    }
}