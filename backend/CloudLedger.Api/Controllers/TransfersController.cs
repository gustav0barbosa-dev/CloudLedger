using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/transfers")]
[Authorize]
[Produces("application/json")]
public sealed class TransfersController : ControllerBase
{
    private readonly TransferService _transferService;
    private readonly ICurrentUser _currentUser;

    public TransfersController(TransferService transferService, ICurrentUser currentUser)
    {
        _transferService = transferService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TransferDto>>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new TransferQuery(from, to, accountId, page, pageSize);
        return Ok(await _transferService.ListAsync(_currentUser.RequireUserId(), query, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferDto>> Create(CreateTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await _transferService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = transfer.Id }, transfer);
    }

    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken)
    {
        await _transferService.VoidAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }
}
