using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using otel.QueueCommon;
using otel.ServiceWorker;
using RabbitMQ.Client;
using Serilog;

// Log the startup information
var builder = Host.CreateApplicationBuilder(args);

// Add Aspire
builder.AddServiceDefaults();
builder.AddRabbitMQ(Bus.Host);

var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

if(!string.IsNullOrWhiteSpace(otelEndpoint))
    builder.Services.AddSerilog(config =>
    {
        config.ReadFrom.Configuration(builder.Configuration)
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = $"{otelEndpoint}/v1/logs";
            options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"]
            };
        });
    });
else
    builder.Services.AddSerilog(config =>
    {
        config.ReadFrom.Configuration(builder.Configuration);
    });

// builder.Services.AddSingleton<IBus>(sp => RabbitMQFactory.CreateBus(sp.GetService<IConnection>()));
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger>();
logger.Information("Starting up...");
logger.Information("OTEL_EXPORTER_OTLP_ENDPOINT: {endpoint}", otelEndpoint);

app.Run();