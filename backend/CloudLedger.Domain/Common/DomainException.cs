namespace CloudLedger.Domain.Common;

/// <summary>
/// Exceção de regra de negócio. Deve ser traduzida pela API para HTTP 400/422.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
