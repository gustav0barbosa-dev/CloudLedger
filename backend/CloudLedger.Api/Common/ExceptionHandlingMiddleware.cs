using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CloudLedger.Api.Common;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            ValidationAppException => (StatusCodes.Status400BadRequest, "Erro de validação"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status >= 500)
        {
            _logger.LogError(exception, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogInformation(
                "Requisição rejeitada ({Status}) em {Method} {Path}: {Message}",
                status,
                context.Request.Method,
                context.Request.Path,
                exception.Message);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Ocorreu um erro inesperado. Tente novamente mais tarde." : exception.Message,
            Instance = context.Request.Path
        };

        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey];

        if (exception is AppException appException)
        {
            problem.Extensions["code"] = appException.Code;
        }

        if (exception is ValidationAppException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
