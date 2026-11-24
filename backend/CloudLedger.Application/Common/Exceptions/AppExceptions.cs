namespace CloudLedger.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string resource) : base("not_found", $"{resource} não encontrado.")
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Você não tem permissão para esta operação.") : base("forbidden", message)
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Credenciais inválidas.") : base("unauthorized", message)
    {
    }
}

public sealed class InfrastructureUnavailableException : AppException
{
    public InfrastructureUnavailableException(string service)
        : base("infrastructure_unavailable", $"O serviço {service} está temporariamente indisponível.")
    {
    }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string field, string message) : base("validation", message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public ValidationAppException(IDictionary<string, string[]> errors) : base("validation", "Um ou mais campos são inválidos.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
