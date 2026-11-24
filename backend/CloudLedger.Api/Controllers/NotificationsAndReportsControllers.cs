using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Dashboard;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(NotificationService notificationService, ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] bool unreadOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _notificationService.ListAsync(_currentUser.RequireUserId(), unreadOnly, page, pageSize, cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(await _notificationService.CountUnreadAsync(_currentUser.RequireUserId(), cancellationToken));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> MarkAllRead(CancellationToken cancellationToken) =>
        Ok(await _notificationService.MarkAllAsReadAsync(_currentUser.RequireUserId(), cancellationToken));
}

[ApiController]
[Route("api/v1/audit-events")]
[Authorize]
[Produces("application/json")]
public sealed class AuditEventsController : ControllerBase
{
    private readonly AuditQueryService _auditQueryService;
    private readonly ICurrentUser _currentUser;

    public AuditEventsController(AuditQueryService auditQueryService, ICurrentUser currentUser)
    {
        _auditQueryService = auditQueryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _auditQueryService.ListAsync(_currentUser.RequireUserId(), from, to, action, page, pageSize, cancellationToken));
}

[ApiController]
[Route("api/v1/reports")]
[Authorize]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly ICurrentUser _currentUser;

    public ReportsController(ReportService reportService, ICurrentUser currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    /// <summary>Exporta os lançamentos do período em CSV para armazenamento privado (S3) ou local.</summary>
    [HttpPost("transactions/export")]
    [ProducesResponseType(typeof(ReportExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportExportDto>> ExportTransactions(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _reportService.ExportTransactionsAsync(_currentUser.RequireUserId(), from, to, cancellationToken));
}
