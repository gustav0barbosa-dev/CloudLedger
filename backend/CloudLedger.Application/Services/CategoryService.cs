using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Finance;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Categories;

namespace CloudLedger.Application.Services;

public sealed class CategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public CategoryService(
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IAuditWriter audit,
        ICorrelationContext correlation,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CategoryKind? kind, CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(userId, kind, cancellationToken);
        return categories.Select(ToDto).ToList();
    }

    public async Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = Category.CreateCustom(userId, request.Name, request.Kind, request.Color, request.Icon);
        _categories.Add(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.CategoryCreated, nameof(Category), category.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", category.Name), ("kind", category.Kind.ToString())),
            cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid userId, Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Categoria");

        if (category.IsDefault || category.UserId is null)
        {
            throw new ForbiddenException("Categorias padrão não podem ser alteradas.");
        }

        category.Rename(request.Name, request.Color, request.Icon);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.CategoryUpdated, nameof(Category), category.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", category.Name)),
            cancellationToken);

        return ToDto(category);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException("Categoria");

        if (category.IsDefault || category.UserId is null)
        {
            throw new ForbiddenException("Categorias padrão não podem ser removidas.");
        }

        if (await _categories.IsInUseAsync(userId, id, cancellationToken))
        {
            throw new ConflictException("category.in_use", "Esta categoria está em uso e não pode ser removida.");
        }

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.CategoryDeleted, nameof(Category), category.Id, _correlation.CorrelationId, _clock.UtcNow,
                ("name", category.Name)),
            cancellationToken);
    }

    private static CategoryDto ToDto(Category category) => new(
        category.Id,
        category.Name,
        category.Kind,
        category.IsDefault,
        category.Color,
        category.Icon);
}
