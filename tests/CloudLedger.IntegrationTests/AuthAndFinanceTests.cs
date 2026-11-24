using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudLedger.Application.Contracts.Auth;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CloudLedger.IntegrationTests;

public sealed class AuthAndFinanceTests : IClassFixture<CloudLedgerWebApplicationFactory>
{
    private readonly CloudLedgerWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AuthAndFinanceTests(CloudLedgerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo@cloudledger.dev", "Demo@12345"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth.AccessToken;
    }

    [Fact]
    public async Task Login_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo@cloudledger.dev", "Demo@12345"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
    }

    [Fact]
    public async Task Dashboard_ReturnsSummary()
    {
        await LoginAsync();
        var response = await _client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndVoidTransaction_UpdatesBalance()
    {
        await LoginAsync();

        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        var account = accounts!.First();

        var before = account.Balance;

        var create = await _client.PostAsJsonAsync("/api/v1/transactions", new CreateTransactionRequest(
            account.Id, null, CloudLedger.Domain.Transactions.TransactionType.Expense, 50m, "Teste integração", DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var transaction = await create.Content.ReadFromJsonAsync<CloudLedger.Application.Contracts.Finance.TransactionDto>(JsonOptions);

        var afterCreate = await _client.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{account.Id}", JsonOptions);
        Assert.Equal(before - 50m, afterCreate!.Balance);

        var voidResponse = await _client.PostAsync($"/api/v1/transactions/{transaction!.Id}/void", null);
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var afterVoid = await _client.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{account.Id}", JsonOptions);
        Assert.Equal(before, afterVoid!.Balance);
    }

    [Fact]
    public async Task TransactionCreation_PersistsOutboxMessage()
    {
        await LoginAsync();

        var accounts = await _client.GetFromJsonAsync<List<AccountDto>>("/api/v1/accounts", JsonOptions);
        var account = accounts!.First();
        var before = await CountOutboxMessagesAsync();

        var create = await _client.PostAsJsonAsync("/api/v1/transactions", new CreateTransactionRequest(
            account.Id, null, CloudLedger.Domain.Transactions.TransactionType.Expense, 25m, "Teste outbox", DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(before + 1, await CountOutboxMessagesAsync());
    }

    private async Task<int> CountOutboxMessagesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxMessages.CountAsync();
    }

    [Fact]
    public async Task Unauthenticated_Dashboard_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}