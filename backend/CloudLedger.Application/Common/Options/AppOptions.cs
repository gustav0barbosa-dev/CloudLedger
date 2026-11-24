namespace CloudLedger.Application.Common.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CloudLedger";

    public string Audience { get; set; } = "CloudLedger.Web";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    /// <summary>Quando falso, os adapters AWS são substituídos por implementações locais.</summary>
    public bool Enabled { get; set; }

    public string Region { get; set; } = "us-east-1";

    public string? ServiceUrl { get; set; }

    public bool UseLocalStack { get; set; }

    public string SqsQueueName { get; set; } = "cloudledger-domain-events";

    public string DynamoTableName { get; set; } = "cloudledger-idempotency";

    public string S3BucketName { get; set; } = "cloudledger-reports";
}

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>
    /// Quando falso (ou quando não há connection string), a auditoria usa o armazenamento em memória.
    /// Útil para desenvolvimento local sem Docker e para testes automatizados.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "cloudledger_audit";
}
