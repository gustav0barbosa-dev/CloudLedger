using CloudLedger.Application.Abstractions;

namespace CloudLedger.Worker.Workers;

/// <summary>
/// Correlation id por escopo de processamento (sempre que um evento é tratado).
/// </summary>
public sealed class WorkerCorrelationContext : ICorrelationContext
{
    public WorkerCorrelationContext() => CorrelationId = Guid.NewGuid().ToString("N");

    public string CorrelationId { get; }
}
