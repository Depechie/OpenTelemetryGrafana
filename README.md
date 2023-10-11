# OpenTelemetry together with the Grafana stack

This repository is a work in progress, about having an observability stack for your microservices environment.

If you want to know/learn more, also check out my Pluralsight course on the subject here [https://www.pluralsight.com/library/courses/opentelemetry-grafana-observability](https://www.pluralsight.com/library/courses/opentelemetry-grafana-observability)

### Components used:

- ASP.NET Web API for demo services
- RabbitMq as message queue
- OpenTelemetry Collector as middle man for tracing and logs
- BlackBox and Prometheus for service metrics and health checks
- Loki for log aggregation
- Tempo for tracing aggregation
- Grafana for overall dashboarding

### Remarks:

For alert provisioning we can extract the JSON through use of the Grafana API: [https://grafana.com/docs/grafana/latest/developers/http_api/alerting_provisioning/](https://grafana.com/docs/grafana/latest/developers/http_api/alerting_provisioning/)

This sample is sending logs from SeriLog to OpenTelemetry in OTLP format and OpenTelemetry will pass it along to Loki.

### Usage:

```
docker compose -f docker-compose-mac.yml up -d
docker compose -f docker-compose-mac.yml down
```
