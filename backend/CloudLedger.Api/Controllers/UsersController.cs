using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Contracts.Auth;
using CloudLedger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ICurrentUser _currentUser;

    public UsersController(UserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken) =>
        Ok(await _userService.GetAsync(_currentUser.RequireUserId(), cancellationToken));

    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await _userService.UpdateProfileAsync(_currentUser.RequireUserId(), request, cancellationToken));

    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ChangePasswordAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return NoContent();
    }
}
