using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Contracts.Dashboard;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly ICurrentUser _currentUser;

    public DashboardController(DashboardService dashboardService, ICurrentUser currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    /// <summary>Resumo completo do dashboard para o período informado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _dashboardService.GetAsync(_currentUser.RequireUserId(), from, to, cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _dashboardService.GetSummaryAsync(_currentUser.RequireUserId(), from, to, cancellationToken));

    [HttpGet("series")]
    [ProducesResponseType(typeof(IReadOnlyList<SeriesPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SeriesPointDto>>> Series(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] SeriesGranularity granularity = SeriesGranularity.Day,
        CancellationToken cancellationToken = default) =>
        Ok(await _dashboardService.GetSeriesAsync(_currentUser.RequireUserId(), from, to, granularity, cancellationToken));

    [HttpGet("expenses-by-category")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryExpenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryExpenseDto>>> ExpensesByCategory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _dashboardService.GetExpensesByCategoryAsync(_currentUser.RequireUserId(), from, to, cancellationToken));

    [HttpGet("recent-transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> RecentTransactions(
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default) =>
        Ok(await _dashboardService.GetRecentTransactionsAsync(_currentUser.RequireUserId(), take, cancellationToken));

    [HttpGet("upcoming-payments")]
    [ProducesResponseType(typeof(IReadOnlyList<UpcomingPaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UpcomingPaymentDto>>> UpcomingPayments(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await _dashboardService.GetUpcomingPaymentsAsync(_currentUser.RequireUserId(), days, cancellationToken));

    [HttpGet("goals")]
    [ProducesResponseType(typeof(IReadOnlyList<GoalProgressDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GoalProgressDto>>> Goals(CancellationToken cancellationToken) =>
        Ok(await _dashboardService.GetGoalsAsync(_currentUser.RequireUserId(), cancellationToken));
}
