namespace CloudLedger.Worker.Workers;

/// <summary>
/// Worker de fundação. Na fase de eventos AWS será substituído pelo consumidor
/// de SQS com idempotência (DynamoDB) e DLQ.
/// </summary>
public sealed class WorkerHeartbeat : BackgroundService
{
    private readonly ILogger<WorkerHeartbeat> _logger;

    public WorkerHeartbeat(ILogger<WorkerHeartbeat> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CloudLedger.Worker iniciado — consumidores de eventos serão habilitados na fase AWS.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                _logger.LogDebug("CloudLedger.Worker ativo em {UtcNow:O}", DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("CloudLedger.Worker encerrado.");
    }
}
