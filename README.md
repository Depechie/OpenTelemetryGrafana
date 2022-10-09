# OpenTelemetry together with the Grafana stack

This repository is a work in progress, about having an observability stack for your microservices environment.

Components used:

- ASP.NET Web API for demo services
- RabbitMq as message queue
- OpenTelemetry Collector as middle man for tracing
- BlackBox and Prometheus for service metrics and health checks
- Loki for log aggregation
- Tempo for tracing aggregation
- Grafana for overall dashboarding
