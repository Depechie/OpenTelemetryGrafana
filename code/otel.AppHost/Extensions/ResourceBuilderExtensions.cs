namespace otel.AppHost;

using Aspire.Hosting.Utils;

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
            if (context.PublisherName == "manifest")
            {
                // Runtime only
                return;
            }

            var url = configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
            context.EnvironmentVariables[name] = builder.Resource is ContainerResource
                ? HostNameResolver.ReplaceLocalhostWithContainerHost(url, configuration)
                : url;
        });
    }

    public static IResourceBuilder<T> WithLokiPushUrl<T>(this IResourceBuilder<T> builder, string name, EndpointReference lokiEndpoint)
        where T : IResourceWithEnvironment
    {
        return builder.WithEnvironment(context =>
        {
            if (context.PublisherName == "manifest")
            {
                context.EnvironmentVariables[name] = $"{lokiEndpoint.UriString}/loki/api/v1/push";
                return;
            }

            // https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/lokiexporter#getting-started
            var url = lokiEndpoint.UriString + "/loki/api/v1/push";

            context.EnvironmentVariables[name] = builder.Resource is ContainerResource
            ? HostNameResolver.ReplaceLocalhostWithContainerHost(url, builder.ApplicationBuilder.Configuration)
            : url;
        });
    }
}