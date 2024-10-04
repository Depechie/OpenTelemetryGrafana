using otel.Catalog.API;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire
builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

if(!string.IsNullOrWhiteSpace(otelEndpoint))
    builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = $"{otelEndpoint}/v1/logs";
            options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"] ?? string.Empty
            };
        }));
else
    builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.MapDefaultEndpoints();
app.MapEndpoints();
app.UseSerilogRequestLogging();

app.Run();