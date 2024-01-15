using otel.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddContainer("loki", "grafana/loki", "2.9.2")
    .WithServiceBinding(containerPort: 3100, hostPort: 3100, name: "http", scheme: "http")
    .WithServiceBinding(containerPort: 9096, hostPort: 9096, name: "grpc", scheme: "http")
    .WithVolumeMount("../config/loki.yml", "/etc/loki/local-config.yaml", VolumeMountType.Bind)
    .WithVolumeMount("loki", "/data/loki", VolumeMountType.Named)
    .WithArgs("-config.file=/etc/loki/local-config.yaml");

var tempo = builder.AddContainer("tempo", "grafana/tempo", "2.3.1")
    .WithServiceBinding(containerPort: 3200, hostPort: 3200, name: "http", scheme: "http")
    .WithServiceBinding(containerPort: 4317, hostPort: 4007, name: "otlp", scheme: "http")
    .WithVolumeMount("../config/tempo.yml", "/etc/tempo.yaml", VolumeMountType.Bind)
    .WithVolumeMount("tempo", "/tmp/tempo", VolumeMountType.Named)
    .WithArgs("-config.file=/etc/tempo.yaml");

var otel = builder.AddContainer("otel", "otel/opentelemetry-collector-contrib", "0.91.0")
    .WithServiceBinding(containerPort: 4317, hostPort: 4317, name: "grpc", scheme: "http") // Have to put the schema to HTTP otherwise the C# will complain about the OTEL_EXPORTER_OTLP_ENDPOINT variable
    .WithServiceBinding(containerPort: 55679, hostPort: 9200, name: "zpages", scheme: "http")
    .WithVolumeMount("../config/otel.yml", "/etc/otel-collector-config.yaml", VolumeMountType.Bind)
    .WithArgs("--config=/etc/otel-collector-config.yaml")
    .WithLokiPushUrl("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("otlp"))
    .WithDashboardEndpoint("DASHBOARD_URL");

var messaging = builder.AddRabbitMQ("messaging");

var basketAPI = builder.AddProject<Projects.otel_Basket_API>("basket.api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithReference(messaging);

var catalogAPI = builder.AddProject<Projects.otel_Catalog_API>("catalog.api")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"));

var serviceWorker = builder.AddProject<Projects.otel_ServiceWorker>("serviceworker")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithReference(messaging);

// builder.AddContainer("blackbox", "prom/blackbox-exporter", "v0.24.0")
//     .WithServiceBinding(containerPort: 9115, hostPort: 9115, name: "http", scheme: "http")
//     .WithVolumeMount("../config/blackbox.yml", "/etc/blackbox/blackbox.yml", VolumeMountType.Bind)
//     .WithArgs("--config.file=/etc/blackbox/blackbox.yml");

// var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v2.48.0")
//     .WithServiceBinding(containerPort: 9090, hostPort: 9090, name: "http", scheme: "http")
//     .WithVolumeMount("../config/prometheus.yml", "/etc/prometheus/prometheus.yml", VolumeMountType.Bind)
//     .WithVolumeMount("prometheus", "/prometheus", VolumeMountType.Named);
    // .WithEnvironment("BASKET_URL", basketAPI.GetEndpoint("http"))
    // .WithEnvironment("CATALOG_URL", catalogAPI.GetEndpoint("http"))
    // .WithArgs("--config.file=/etc/prometheus/prometheus.yml", "--enable-feature=expand-external-labels");

builder.AddContainer("grafana", "grafana/grafana", "10.2.1")
    .WithServiceBinding(containerPort: 3000, hostPort: 3000, name: "http", scheme: "http")
    .WithVolumeMount("../config/grafana/provisioning", "/etc/grafana/provisioning", VolumeMountType.Bind)
    .WithVolumeMount("grafana-data", "/var/lib/grafana", VolumeMountType.Named)
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("http"));
    // .WithEnvironment("PROMETHEUS_URL", prometheus.GetEndpoint("http"));

builder.Build().Run();