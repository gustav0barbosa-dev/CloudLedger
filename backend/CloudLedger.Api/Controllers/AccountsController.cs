using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize]
[Produces("application/json")]
public sealed class AccountsController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly ICurrentUser _currentUser;

    public AccountsController(AccountService accountService, ICurrentUser currentUser)
    {
        _accountService = accountService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List([FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await _accountService.ListAsync(_currentUser.RequireUserId(), includeArchived, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _accountService.GetAsync(_currentUser.RequireUserId(), id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await _accountService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, account);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> Update(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await _accountService.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken));

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await _accountService.ArchiveAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken cancellationToken)
    {
        await _accountService.UnarchiveAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _accountService.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }
}
