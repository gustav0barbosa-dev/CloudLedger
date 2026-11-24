using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Models;
using CloudLedger.Application.Contracts.Dashboard;

namespace CloudLedger.Application.Services;

public sealed class AuditQueryService
{
    private readonly IAuditReader _auditReader;

    public AuditQueryService(IAuditReader auditReader)
    {
        _auditReader = auditReader;
    }

    public async Task<PagedResult<AuditEventDto>> ListAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);
        var result = await _auditReader.ListAsync(userId, fromUtc, toUtc, action, normalizedPage, normalizedSize, cancellationToken);

        return new PagedResult<AuditEventDto>(
            result.Items.Select(x => new AuditEventDto(x.Id, x.Action, x.ResourceType, x.ResourceId, x.OccurredAt, x.Metadata)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems);
    }
}
