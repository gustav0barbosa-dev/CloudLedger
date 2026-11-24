using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Application.Services;
using CloudLedger.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
[Produces("application/json")]
public sealed class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ICurrentUser _currentUser;

    public TransactionsController(TransactionService transactionService, ICurrentUser currentUser)
    {
        _transactionService = transactionService;
        _currentUser = currentUser;
    }

    /// <summary>Lista lançamentos com filtros por período, tipo, categoria, conta, texto e status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] TransactionType? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? accountId,
        [FromQuery] string? q,
        [FromQuery] TransactionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "occurredAt",
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        var query = new TransactionQuery(from, to, type, categoryId, accountId, q, status, page, pageSize, sortBy, sortDirection);
        return Ok(await _transactionService.ListAsync(_currentUser.RequireUserId(), query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _transactionService.GetAsync(_currentUser.RequireUserId(), id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, transaction);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken) =>
        Ok(await _transactionService.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken));

    /// <summary>Estorno lógico: preserva o lançamento e o marca como cancelado.</summary>
    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken)
    {
        await _transactionService.VoidAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }
}
