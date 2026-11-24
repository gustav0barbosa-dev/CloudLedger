using System.Globalization;
using System.Text;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Abstractions.Persistence;
using CloudLedger.Application.Common;
using CloudLedger.Application.Common.Exceptions;
using CloudLedger.Application.Contracts.Dashboard;
using CloudLedger.Domain.Audit;
using CloudLedger.Domain.Transactions;

namespace CloudLedger.Application.Services;

public sealed class ReportService
{
    private const int ExportPageSize = 100;
    private const int MaxExportPages = 200;

    private readonly ITransactionRepository _transactions;
    private readonly IReportStorage _storage;
    private readonly IAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public ReportService(
        ITransactionRepository transactions,
        IReportStorage storage,
        IAuditWriter audit,
        ICorrelationContext correlation,
        IClock clock)
    {
        _transactions = transactions;
        _storage = storage;
        _audit = audit;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<ReportExportDto> ExportTransactionsAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ValidationAppException("period", "A data inicial deve ser anterior à data final.");
        }

        if (to.DayNumber - from.DayNumber > 366)
        {
            throw new ValidationAppException("period", "O período máximo para exportação é de 366 dias.");
        }

        var csv = new StringBuilder();
        csv.Append('\uFEFF');
        csv.AppendLine("Data;Tipo;Categoria;Conta;Descricao;Valor;Status");

        var page = 1;
        while (page <= MaxExportPages)
        {
            var result = await _transactions.ListAsync(
                userId,
                new TransactionQuery(from, to, null, null, null, null, TransactionStatus.Confirmed, page, ExportPageSize, "occurredAt", "asc"),
                cancellationToken);

            foreach (var item in result.Items)
            {
                csv.Append(item.OccurredAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
                    .Append(item.Type == TransactionType.Income ? "Receita" : "Despesa").Append(';')
                    .Append(Escape(item.CategoryName ?? "Sem categoria")).Append(';')
                    .Append(Escape(item.AccountName)).Append(';')
                    .Append(Escape(item.Description)).Append(';')
                    .Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                    .Append("Confirmada")
                    .AppendLine();
            }

            if (page >= result.TotalPages)
            {
                break;
            }

            page++;
        }

        var fileName = $"transacoes-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
        var url = await _storage.SaveAsync(fileName, "text/csv", csv.ToString(), cancellationToken);

        await _audit.WriteAsync(
            AuditEventFactory.Create(userId, AuditActions.ReportExported, "Report", null, _correlation.CorrelationId, _clock.UtcNow,
                ("from", from.ToString("yyyy-MM-dd")), ("to", to.ToString("yyyy-MM-dd")), ("fileName", fileName)),
            cancellationToken);

        return new ReportExportDto(fileName, url, _clock.UtcNow.AddMinutes(15));
    }

    private static string Escape(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
