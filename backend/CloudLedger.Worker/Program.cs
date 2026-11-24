using CloudLedger.Application;
using CloudLedger.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(configuration => configuration
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CloudLedger.Worker.Workers.IDomainEventProcessor, CloudLedger.Worker.Workers.DomainEventProcessor>();
builder.Services.AddHostedService<CloudLedger.Worker.Workers.OutboxDispatcher>();
builder.Services.AddHostedService<CloudLedger.Worker.Workers.SqsEventConsumer>();

var host = builder.Build();
await host.RunAsync();
