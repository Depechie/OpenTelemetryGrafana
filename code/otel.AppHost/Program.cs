using otel.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var loki = builder
    .AddContainer("loki", "grafana/loki", "2.9.8")
    .WithEndpoint(targetPort: 3100, port: 3100, name: "http", scheme: "http")
    .WithEndpoint(targetPort: 9096, port: 9096, name: "grpc", scheme: "http")
    .WithBindMount("../config/loki.yml", "/etc/loki/local-config.yaml")
    .WithVolume("loki", "/data/loki")
    .WithArgs("-config.file=/etc/loki/local-config.yaml");

var tempo = builder
    .AddContainer("tempo", "grafana/tempo", "2.4.2")
    .WithEndpoint(targetPort: 3200, port: 3200, name: "http", scheme: "http")
    .WithEndpoint(targetPort: 4317, port: 4007, name: "otlp", scheme: "http")
    .WithBindMount("../config/tempo.yml", "/etc/tempo.yaml")
    .WithVolume("tempo", "/tmp/tempo")
    .WithArgs("-config.file=/etc/tempo.yaml");

var otel = builder
    .AddContainer("otel", "otel/opentelemetry-collector-contrib", "0.100.0")
    .WithEndpoint(targetPort: 4317, port: 4317,  name: "grpc", scheme: "http") // Have to put the schema to HTTP otherwise the C# will complain about the OTEL_EXPORTER_OTLP_ENDPOINT variable
    .WithEndpoint(targetPort: 55679, port: 9200, name: "zpages", scheme: "http")
    .WithEndpoint(targetPort: 8888, port: 8888,  name: "prometheus-services", scheme: "http")
    .WithEndpoint(targetPort: 8889, port: 8889,  name: "prometheus-collector", scheme: "http")
    .WithBindMount("../config/otel.yml", "/etc/otel-collector-config.yaml")
    .WithArgs("--config=/etc/otel-collector-config.yaml")
    .WithLokiPushUrl("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("otlp"))
    .WithDashboardEndpoint("DASHBOARD_URL");

var messaging = builder.AddRabbitMQ("messaging");

var basketAPI = builder.AddProject<Projects.otel_Basket_API>("basket-api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithReference(messaging);

var catalogAPI = builder.AddProject<Projects.otel_Catalog_API>("catalog-api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"));

var serviceWorker = builder.AddProject<Projects.otel_ServiceWorker>("serviceworker")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithReference(messaging);

builder
    .AddContainer("blackbox", "prom/blackbox-exporter", "v0.25.0")
    .WithEndpoint(targetPort: 9115, port: 9115, name: "http", scheme: "http")
    .WithBindMount("../config/", "/etc/blackbox/")
    .WithArgs("--config.file=/etc/blackbox/blackbox.yml");

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v2.52.0")
    .WithEndpoint(targetPort: 9090, port: 9090, name: "http", scheme: "http")
    .WithBindMount("../config/prometheus.yml", "/etc/prometheus/prometheus.yml")
    .WithVolume("prometheus", "/prometheus")
    .WithReference(otel.GetEndpoint("prometheus-services"));
 
    // .WithEnvironment("BASKET_URL", basketAPI.GetEndpoint("http"))
    // .WithEnvironment("CATALOG_URL", catalogAPI.GetEndpoint("http"))
    // .WithArgs("--config.file=/etc/prometheus/prometheus.yml", "--enable-feature=expand-external-labels");

builder.AddContainer("grafana", "grafana/grafana", "11.0.0")
    .WithEndpoint(targetPort: 3000, port: 3000, name: "http", scheme: "http")
    .WithBindMount("../config/grafana/provisioning", "/etc/grafana/provisioning")
    .WithVolume("grafana-data", "/var/lib/grafana")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("http"))
    .WithEnvironment("PROMETHEUS_URL", prometheus.GetEndpoint("http"));

builder.Build().Run();