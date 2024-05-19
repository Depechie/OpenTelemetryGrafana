namespace otel.AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

public static class ResourceBuilderExtensions
{
    private const string DashboardOtlpUrlVariableName = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";

    // Adds the dashboard OTLP endpoint URL to the environment variables of the resource with the specified name.
    public static IResourceBuilder<T> WithDashboardEndpoint<T>(this IResourceBuilder<T> builder, string name)
        where T : IResourceWithEnvironment
    {
        var configuration = builder.ApplicationBuilder.Configuration;

        return builder.WithEnvironment(context =>
        {
            var t = context.ExecutionContext;
            if (context.ExecutionContext.IsPublishMode)
            {
                // Runtime only
                return;
            }

            var url = configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
            context.EnvironmentVariables[name] = builder.Resource is ContainerResource
                ? ReplaceLocalhostWithContainerHost(url, configuration)
                : url;
        });
    }

    public static IResourceBuilder<T> WithLokiPushUrl<T>(this IResourceBuilder<T> builder, string name, EndpointReference lokiEndpoint)
        where T : IResourceWithEnvironment
    {
        return builder.WithEnvironment(context =>
        {
            var t = context.ExecutionContext;
            // "manifest" is IsRunMode == false, no? i.e. it's DistributedApplicationOperation.Publish mode, which generates the manifest
            //manifest == IsPublish mode
            // if (context.PublisherName == "manifest")
            if (context.ExecutionContext.IsPublishMode)
            {
                context.EnvironmentVariables[name] = $"{lokiEndpoint.Url}/loki/api/v1/push";
                return;
            }

            // https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/lokiexporter#getting-started
            var url = lokiEndpoint.Url + "/loki/api/v1/push";

            context.EnvironmentVariables[name] = builder.Resource is ContainerResource
            ? ReplaceLocalhostWithContainerHost(url, builder.ApplicationBuilder.Configuration)
            : url;
        });
    }


    private static string ReplaceLocalhostWithContainerHost(string value,ConfigurationManager configuration )
    {
        // https://stackoverflow.com/a/43541732/45091

        // This configuration value is a workaround for the fact that host.docker.internal is not available on Linux by default.
        var hostName = configuration["AppHost:ContainerHostname"] ?? /*_dcpInfo?.Containers?.ContainerHostName ??*/ "host.docker.internal";;

        return value.Replace("localhost", hostName, StringComparison.OrdinalIgnoreCase)
                    .Replace("127.0.0.1", hostName)
                    .Replace("[::1]", hostName);
    }
}