using CloudLedger.Application;
using CloudLedger.Application.Abstractions;
using CloudLedger.Infrastructure;
using CloudLedger.Worker.Workers;
using Serilog;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);

var jsonLogs = builder.Configuration.GetValue("Logging:Json", false);

builder.Services.AddSerilog(configuration =>
{
    configuration
        .ReadFrom.Configuration(builder.Configuration)
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
builder.Services.AddScoped<ICorrelationContext, WorkerCorrelationContext>();
builder.Services.AddScoped<IDomainEventProcessor, DomainEventProcessor>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<SqsEventConsumer>();

var host = builder.Build();
await host.RunAsync();
