using System.Collections.Concurrent;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Common.Options;
using CloudLedger.Domain.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace CloudLedger.Infrastructure.Auditing;

internal sealed class AuditEventDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [BsonElement("resourceId")]
    public string? ResourceId { get; set; }

    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }

    [BsonElement("correlationId")]
    public string? CorrelationId { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, string?> Metadata { get; set; } = new();

    public static AuditEventDocument FromDomain(AuditEvent auditEvent) => new()
    {
        Id = auditEvent.Id.ToString("N"),
        UserId = auditEvent.UserId.ToString("N"),
        Action = auditEvent.Action,
        ResourceType = auditEvent.ResourceType,
        ResourceId = auditEvent.ResourceId?.ToString("N"),
        OccurredAt = auditEvent.OccurredAt,
        CorrelationId = auditEvent.CorrelationId,
        Metadata = new Dictionary<string, string?>(auditEvent.Metadata)
    };

    public AuditEvent ToDomain() => new(
        Guid.TryParseExact(Id, "N", out var id) ? id : Guid.Empty,
        Guid.TryParseExact(UserId, "N", out var userId) ? userId : Guid.Empty,
        Action,
        ResourceType,
        Guid.TryParseExact(ResourceId, "N", out var resourceId) ? resourceId : null,
        OccurredAt,
        CorrelationId,
        Metadata);
}

/// <summary>
/// Trilha de auditoria no MongoDB. A escrita é best-effort: indisponibilidade
/// do Mongo não deve derrubar a operação financeira principal.
/// </summary>
public sealed class MongoAuditStore : IAuditWriter, IAuditReader
{
    private const string CollectionName = "audit_events";

    private readonly IMongoCollection<AuditEventDocument> _collection;
    private readonly ILogger<MongoAuditStore> _logger;

    public MongoAuditStore(IMongoDatabase database, ILogger<MongoAuditStore> logger)
    {
        _collection = database.GetCollection<AuditEventDocument>(CollectionName);
        _logger = logger;
    }

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // A trilha é best-effort: não deve ser cancelada junto com a requisição do cliente.
            await _collection.InsertOneAsync(AuditEventDocument.FromDomain(auditEvent), cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar evento de auditoria {Action} para o usuário {UserId}", auditEvent.Action, auditEvent.UserId);
        }
    }

    public async Task<PagedResult<AuditEvent>> ListAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filters = new List<FilterDefinition<AuditEventDocument>>
            {
                Builders<AuditEventDocument>.Filter.Eq(x => x.UserId, userId.ToString("N"))
            };

            if (fromUtc is not null)
            {
                filters.Add(Builders<AuditEventDocument>.Filter.Gte(x => x.OccurredAt, fromUtc.Value));
            }

            if (toUtc is not null)
            {
                filters.Add(Builders<AuditEventDocument>.Filter.Lte(x => x.OccurredAt, toUtc.Value));
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                filters.Add(Builders<AuditEventDocument>.Filter.Eq(x => x.Action, action));
            }

            var filter = Builders<AuditEventDocument>.Filter.And(filters);
            var total = (int)await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            var documents = await _collection
                .Find(filter)
                .SortByDescending(x => x.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<AuditEvent>(
                documents.Select(x => x.ToDomain()).ToList(),
                page,
                pageSize,
                total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar eventos de auditoria do usuário {UserId}", userId);
            // Não confundir "sem registros" com "MongoDB indisponível".
            throw new InfrastructureUnavailableException("de auditoria");
        }
    }
}

/// <summary>
/// Alternativa em memória para ambientes sem MongoDB (testes e execução mínima).
/// </summary>
public sealed class InMemoryAuditStore : IAuditWriter, IAuditReader
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(auditEvent);
        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditEvent>> ListAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _events.Where(x => x.UserId == userId);

        if (fromUtc is not null)
        {
            query = query.Where(x => x.OccurredAt >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(x => x.OccurredAt <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        var ordered = query.OrderByDescending(x => x.OccurredAt).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<AuditEvent>(items, page, pageSize, ordered.Count));
    }
}

public sealed class LoggingAuditWriter : IAuditWriter
{
    private readonly ILogger<LoggingAuditWriter> _logger;

    public LoggingAuditWriter(ILogger<LoggingAuditWriter> logger) => _logger = logger;

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Audit {Action} {ResourceType}/{ResourceId} user={UserId}",
            auditEvent.Action,
            auditEvent.ResourceType,
            auditEvent.ResourceId,
            auditEvent.UserId);
        return Task.CompletedTask;
    }
}
