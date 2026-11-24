using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Categories;

public enum CategoryKind
{
    Income = 0,
    Expense = 1
}

public sealed class Category : Entity
{
    private Category()
    {
    }

    private Category(Guid? userId, string name, CategoryKind kind, bool isDefault, string? color, string? icon)
    {
        UserId = userId;
        Name = Guard.NotEmpty(name, "nome da categoria", 80);
        Kind = kind;
        IsDefault = isDefault;
        Color = NormalizeColor(color);
        Icon = icon is null ? null : Guard.NotEmpty(icon, "ícone", 16);
    }

    /// <summary>Categorias padrão são globais (UserId nulo) e não podem ser editadas pelo usuário.</summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CategoryKind Kind { get; private set; }

    public bool IsDefault { get; private set; }

    public string? Color { get; private set; }

    public string? Icon { get; private set; }

    public static Category CreateDefault(string name, CategoryKind kind, string? color = null, string? icon = null) =>
        new(null, name, kind, true, color, icon);

    public static Category CreateCustom(Guid userId, string name, CategoryKind kind, string? color = null, string? icon = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("category.user_required", "A categoria precisa estar vinculada a um usuário.");
        }

        return new Category(userId, name, kind, false, color, icon);
    }

    public void Rename(string name, string? color, string? icon)
    {
        if (IsDefault || UserId is null)
        {
            throw new DomainException("category.default_readonly", "Categorias padrão não podem ser alteradas.");
        }

        Name = Guard.NotEmpty(name, "nome da categoria", 80);
        Color = NormalizeColor(color);
        Icon = icon is null ? null : Guard.NotEmpty(icon, "ícone", 16);
    }

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var value = color.Trim();
        if (value.Length != 7 || !value.StartsWith('#'))
        {
            throw new DomainException("category.color", "A cor deve estar no formato hexadecimal (#RRGGBB).");
        }

        return value.ToUpperInvariant();
    }
}
