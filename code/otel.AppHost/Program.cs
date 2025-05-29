using otel.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .PublishAsContainer();

var otellgtm = builder.AddContainer("otel-lgtm", "grafana/otel-lgtm", "0.11.3")
    .WithEndpoint(targetPort: 4317, port: 4317,  name: "grpc", scheme: "http") // Have to put the schema to HTTP otherwise the C# will complain about the OTEL_EXPORTER_OTLP_ENDPOINT variable
    .WithEndpoint(targetPort: 3000, port: 3000, name: "http", scheme: "http")
    .WithBindMount("../config/otel.yml", "/otel-lgtm/otelcol-config.yaml")
    .WithBindMount("../config/prometheus.yml", "/otel-lgtm/prometheus.yaml")
    .WithBindMount("../config/grafana/provisioning", "/otel-lgtm/grafana/conf/provisioning")
    .WithDashboardEndpoint("DASHBOARD_URL");

var basketAPI = builder.AddProject<Projects.otel_Basket_API>("basket-api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otellgtm.GetEndpoint("grpc"))
    .WithReference(messaging)
    .WaitFor(messaging);

var catalogAPI = builder.AddProject<Projects.otel_Catalog_API>("catalog-api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otellgtm.GetEndpoint("grpc"));

var serviceWorker = builder.AddProject<Projects.otel_ServiceWorker>("serviceworker")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otellgtm.GetEndpoint("grpc"))
    .WithReference(messaging)
    .WaitFor(messaging);

builder
    .AddContainer("blackbox", "prom/blackbox-exporter", "v0.26.0")
    .WithEndpoint(targetPort: 9115, port: 9115, name: "http", scheme: "http")
    .WithBindMount("../config/", "/etc/blackbox/")
    .WithArgs("--config.file=/etc/blackbox/blackbox.yml");

builder.Build().Run();