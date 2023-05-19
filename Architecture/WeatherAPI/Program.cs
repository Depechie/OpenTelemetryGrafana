using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using WeatherAPI.Services;
using WeatherAPI.Services.Interfaces;

namespace WeatherAPI
{
    public class Program
    {
        public static ConfigurationManager Configuration { get; private set; }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Configuration = builder.Configuration;

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "OpenTelemetryGrafana", Version = "v1" });
            });
            builder.Services.AddHealthChecks()
                .AddUrlGroup(new Uri("https://localhost:5501/health"), name: "Location API");

            builder.Services.AddHttpClient<ILocationService, LocationService>();

            builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = "http://localhost:4317/v1/logs";
                    options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.GrpcProtobuf;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = Configuration.GetValue<string>("Otlp:ServiceName")
                    };
                }));

            builder.Services.AddRouting(options => options.LowercaseUrls = true);

            Action<ResourceBuilder> appResourceBuilder =
                resource => resource
                    .AddTelemetrySdk()
                    .AddService(Configuration.GetValue<string>("Otlp:ServiceName"));

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(appResourceBuilder)
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("APITracing")
                    //.AddConsoleExporter()
                    .AddOtlpExporter(options => options.Endpoint = new Uri(Configuration.GetValue<string>("Otlp:Endpoint")))
                )
                .WithMetrics(builder => builder
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = new Uri(Configuration.GetValue<string>("Otlp:Endpoint"))));

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenTelemetryGrafana v1"));
            }

            //app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseSerilogRequestLogging();

            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });
            app.MapControllers();

            app.Run();
        }
    }
}