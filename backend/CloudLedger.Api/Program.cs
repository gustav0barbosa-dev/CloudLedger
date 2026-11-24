using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CloudLedger.Api.Common;
using CloudLedger.Application;
using CloudLedger.Application.Abstractions;
using CloudLedger.Application.Common.Options;
using CloudLedger.Infrastructure;
using CloudLedger.Infrastructure.Persistence;
using CloudLedger.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

var jsonLogs = builder.Configuration.GetValue("Logging:Json", false);
var httpsRedirectionEnabled = builder.Configuration.GetValue("HttpsRedirection:Enabled", true);

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

    if (jsonLogs)
    {
        configuration.WriteTo.Console(new JsonFormatter(renderMessage: true));
    }
    else
    {
        configuration.WriteTo.Console();
    }
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value is { Errors.Count: > 0 })
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Valor inválido." : error.ErrorMessage)
                    .ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Erro de validação",
            Detail = "Um ou mais campos são inválidos.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["correlationId"] = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey];

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey deve ser configurada com pelo menos 32 bytes (variável de ambiente Jwt__SigningKey).");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy("spa", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 10;
options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CloudLedger API",
        Version = "v1",
        Description = "API de gestão financeira pessoal demonstrativa. Nenhuma movimentação é real."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o access token JWT."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var healthChecks = builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);

if (!string.IsNullOrWhiteSpace(builder.Configuration["Mongo:ConnectionString"]))
{
    healthChecks.AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CloudLedger API v1"));
}
else
{
    app.UseHsts();

    // Quando o TLS é terminado no CloudFront, a origem ECS recebe HTTP;
    // o redirecionamento é desligado via HttpsRedirection__Enabled=false.
    if (httpsRedirectionEnabled)
    {
        app.UseHttpsRedirection();
    }
}

app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var reportsPath = Path.Combine(AppContext.BaseDirectory, "reports");
Directory.CreateDirectory(reportsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(reportsPath),
    RequestPath = "/api/v1/reports/files"
});

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var provider = app.Configuration["Database:Provider"] ?? "Postgres";

    if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    await DatabaseSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        scope.ServiceProvider.GetRequiredService<IClock>(),
        app.Environment.IsDevelopment());
}

app.Run();

public partial class Program
{
}
