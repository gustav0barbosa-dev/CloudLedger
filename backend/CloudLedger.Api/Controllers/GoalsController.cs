using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Contracts.Planning;
using CloudLedger.Application.Services;
using CloudLedger.Domain.Goals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/goals")]
[Authorize]
[Produces("application/json")]
public sealed class GoalsController : ControllerBase
{
    private readonly GoalService _goalService;
    private readonly ICurrentUser _currentUser;

    public GoalsController(GoalService goalService, ICurrentUser currentUser)
    {
        _goalService = goalService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GoalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GoalDto>>> List([FromQuery] GoalStatus? status, CancellationToken cancellationToken) =>
        Ok(await _goalService.ListAsync(_currentUser.RequireUserId(), status, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _goalService.GetAsync(_currentUser.RequireUserId(), id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(GoalDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GoalDto>> Create(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = await _goalService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = goal.Id }, goal);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoalDto>> Update(Guid id, UpdateGoalRequest request, CancellationToken cancellationToken) =>
        Ok(await _goalService.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(GoalDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GoalDto>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _goalService.CancelAsync(_currentUser.RequireUserId(), id, cancellationToken));

    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType(typeof(IReadOnlyList<GoalMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GoalMovementDto>>> ListMovements(Guid id, CancellationToken cancellationToken) =>
        Ok(await _goalService.ListMovementsAsync(_currentUser.RequireUserId(), id, cancellationToken));

    [HttpPost("{id:guid}/movements")]
    [ProducesResponseType(typeof(GoalMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GoalMovementDto>> AddMovement(
        Guid id,
        CreateGoalMovementRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await _goalService.AddMovementAsync(_currentUser.RequireUserId(), id, request, cancellationToken));
}
