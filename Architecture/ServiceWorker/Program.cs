using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace ServiceWorker
{
    internal class Program
    {
        private static IConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            CreateHostBuilder(args).Build().Run();

            //TODO: Glenn - https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-and-dotnet-core/
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOpenTelemetry()
                        .WithTracing(builder =>
                        {
                            builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Configuration.GetValue<string>("Otlp:ServiceName")))
                                .AddSource("APITracing")
                                .AddOtlpExporter(options => options.Endpoint = new Uri(Configuration.GetValue<string>("Otlp:Endpoint")));
                        });
                    services.AddHostedService<Worker>();
                })
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));
    }
}