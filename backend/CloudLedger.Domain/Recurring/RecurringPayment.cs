using CloudLedger.Domain.Common;

namespace CloudLedger.Domain.Recurring;

/// <summary>
/// Pagamento recorrente previsto (RN-009). Não gera débito automático:
/// apenas projeta ocorrências que o usuário pode marcar como pagas.
/// </summary>
public sealed class RecurringPayment : Entity
{
    private RecurringPayment()
    {
    }

    public RecurringPayment(
        Guid userId,
        Guid accountId,
        Guid? categoryId,
        string description,
        decimal amount,
        RecurrenceFrequency frequency,
        DateOnly nextDueDate)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("recurring.user_required", "O pagamento recorrente precisa estar vinculado a um usuário.");
        }

        if (accountId == Guid.Empty)
        {
            throw new DomainException("recurring.account_required", "O pagamento recorrente precisa estar vinculado a uma conta.");
        }

        UserId = userId;
        AccountId = accountId;
        CategoryId = categoryId;
        Description = Guard.NotEmpty(description, "descrição", 200);
        Amount = Guard.PositiveMoney(amount, "valor");
        Frequency = frequency;
        NextDueDate = nextDueDate;
        IsActive = true;
    }

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    public DateOnly NextDueDate { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        Guid accountId,
        Guid? categoryId,
        string description,
        decimal amount,
        RecurrenceFrequency frequency,
        DateOnly nextDueDate)
    {
        if (accountId == Guid.Empty)
        {
            throw new DomainException("recurring.account_required", "O pagamento recorrente precisa estar vinculado a uma conta.");
        }

        AccountId = accountId;
        CategoryId = categoryId;
        Description = Guard.NotEmpty(description, "descrição", 200);
        Amount = Guard.PositiveMoney(amount, "valor");
        Frequency = frequency;
        NextDueDate = nextDueDate;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public DateOnly NextDateAfter(DateOnly date) => Frequency switch
    {
        RecurrenceFrequency.Weekly => date.AddDays(7),
        RecurrenceFrequency.Monthly => AddMonthsClamped(date, 1),
        RecurrenceFrequency.Yearly => AddMonthsClamped(date, 12),
        _ => throw new DomainException("recurring.frequency", "Periodicidade inválida.")
    };

    public bool IsDueDate(DateOnly date)
    {
        if (date < NextDueDate)
        {
            return false;
        }

        var current = NextDueDate;
        while (current < date)
        {
            current = NextDateAfter(current);
        }

        return current == date;
    }

    /// <summary>Avança a próxima data para a primeira ocorrência posterior à data informada.</summary>
    public void AdvanceAfter(DateOnly date)
    {
        while (NextDueDate <= date)
        {
            NextDueDate = NextDateAfter(NextDueDate);
        }
    }

    /// <summary>
    /// Projeta ocorrências no intervalo [from, to]. O limite de 366 ocorrências
    /// evita consultas abusivas em recorrências semanais com intervalos longos.
    /// </summary>
    public IReadOnlyList<DateOnly> ProjectOccurrences(DateOnly from, DateOnly to, int maxOccurrences = 366)
    {
        if (to < from)
        {
            throw new DomainException("recurring.invalid_period", "A data final deve ser posterior à inicial.");
        }

        var occurrences = new List<DateOnly>();
        if (!IsActive)
        {
            return occurrences;
        }

        var current = NextDueDate;
        while (current < from)
        {
            current = NextDateAfter(current);
        }

        while (current <= to && occurrences.Count < maxOccurrences)
        {
            occurrences.Add(current);
            current = NextDateAfter(current);
        }

        return occurrences;
    }

    private static DateOnly AddMonthsClamped(DateOnly date, int months)
    {
        var target = date.AddMonths(months);
        return target;
    }
}
