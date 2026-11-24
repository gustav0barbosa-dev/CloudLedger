using System.Globalization;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Planning;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/recurring-payments")]
[Authorize]
[Produces("application/json")]
public sealed class RecurringPaymentsController : ControllerBase
{
    private readonly RecurringPaymentService _recurringPaymentService;
    private readonly ICurrentUser _currentUser;

    public RecurringPaymentsController(RecurringPaymentService recurringPaymentService, ICurrentUser currentUser)
    {
        _recurringPaymentService = recurringPaymentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringPaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecurringPaymentDto>>> List(
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken) =>
        Ok(await _recurringPaymentService.ListAsync(_currentUser.RequireUserId(), activeOnly, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(RecurringPaymentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecurringPaymentDto>> Create(
        CreateRecurringPaymentRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await _recurringPaymentService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RecurringPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringPaymentDto>> Update(
        Guid id,
        UpdateRecurringPaymentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _recurringPaymentService.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken));

    [HttpGet("{id:guid}/occurrences")]
    [ProducesResponseType(typeof(IReadOnlyList<OccurrenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OccurrenceDto>>> Occurrences(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(3);
        return Ok(await _recurringPaymentService.GetOccurrencesAsync(_currentUser.RequireUserId(), id, start, end, cancellationToken));
    }

    /// <summary>Marca a ocorrência como paga e gera um único lançamento de despesa (idempotente).</summary>
    [HttpPost("{id:guid}/occurrences/{date}/pay")]
    [ProducesResponseType(typeof(OccurrenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccurrenceDto>> Pay(Guid id, string date, CancellationToken cancellationToken) =>
        Ok(await _recurringPaymentService.PayOccurrenceAsync(_currentUser.RequireUserId(), id, ParseDate(date), cancellationToken));

    [HttpPost("{id:guid}/occurrences/{date}/skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Skip(Guid id, string date, CancellationToken cancellationToken)
    {
        await _recurringPaymentService.SkipOccurrenceAsync(_currentUser.RequireUserId(), id, ParseDate(date), cancellationToken);
        return NoContent();
    }

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ValidationAppException("date", "Data inválida. Use o formato yyyy-MM-dd.");
}
