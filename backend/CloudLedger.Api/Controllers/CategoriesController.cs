using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Application.Services;
using CloudLedger.Domain.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(CategoryService categoryService, ICurrentUser currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List([FromQuery] CategoryKind? kind, CancellationToken cancellationToken) =>
        Ok(await _categoryService.ListAsync(_currentUser.RequireUserId(), kind, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await _categoryService.UpdateAsync(_currentUser.RequireUserId(), id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(_currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }
}
