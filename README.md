# sample

## Observability (local development)

The `docker-compose` stack spins up a Grafana LGTM-style pipeline using
**Alloy** (OTLP collector) -> **Tempo** (traces) + **Prometheus** (metrics)
with **Grafana** as the visualization layer.

### Services & ports

| Service       | Container  | Port(s)        | Purpose                                  |
|---------------|------------|----------------|------------------------------------------|
| `sample.api`  | `api`      | 8080/8081      | .NET API, exports OTLP to Alloy          |
| `alloy`       | `alloy`    | 4317, 4318, 12345 | OTLP receiver (gRPC+HTTP), Alloy UI    |
| `tempo`       | `tempo`    | 3200           | Trace storage + query API                |
| `loki`        | `loki`     | 3100           | Log storage + query API                  |
| `prometheus`  | `prometheus` | 9090        | Metrics TSDB + remote-write receiver     |
| `grafana`     | `grafana`  | 3000           | Dashboards (`admin` / `admin`)           |

### Pipeline

```
.NET API --(OTLP HTTP/protobuf)--> Alloy --+--> Tempo (traces)
                                            +--> Prometheus (remote_write) -+
.NET API (stdout JSON logs) -> Alloy (docker.sock) -> Loki -----------------+
                                                                           |
                                                                          Grafana
```

### Endpoints

- **Grafana**: http://localhost:3000 (`admin` / `admin`)
  - Datasources `Prometheus`, `Tempo` and `Loki` are auto-provisioned (see
    `.docker/grafana/provisioning/datasources/datasources.yml`).
  - The **Sample API Observability** dashboard is auto-provisioned under the
    `Observability` folder (see
    `.docker/grafana/dashboards/sample-api.json`). Open it directly at
    http://localhost:3000/d/sample-api-observability.
- **Tempo API**: http://localhost:3200
- **Loki API**: http://localhost:3100
- **Prometheus**: http://localhost:9090
- **Alloy UI**: http://localhost:12345

### .NET instrumentation

The API registers OpenTelemetry in
`backend/src/Sample.Api/Extensions/ObservabilityExtensions.cs` and is wired
from `Program.cs` via `builder.Services.AddObservability(builder.Configuration)`.

Configuration lives in `appsettings.json` under `Otel:` and is overridable
through the standard environment variables `OTEL_*` (see
`.docker/docker-compose.yml` and `.docker/docker-compose.override.yml`):

```
Otel__ServiceName=sample-api
Otel__Endpoint__Traces=http://alloy:4318/v1/traces
Otel__Endpoint__Metrics=http://alloy:4318/v1/metrics
```

> When running the API **outside** Docker (e.g. `dotnet run` from the host),
> use `http://localhost:4318/...` instead - that's the default in
> `appsettings.json`, so no overrides are needed.

### Custom spans

Use the static `ActivitySource` exposed by `ObservabilityExtensions`:

```csharp
using Sample.Api.Extensions;

using var activity = ObservabilityExtensions.ActivitySource.StartActivity("do-work");
// ... work ...
```

### Logs (.NET -> Loki)

The API writes logs to stdout as JSON (`builder.Logging.AddJsonConsole(...)` in
`Program.cs`). Alloy tails every container's stdout via the Docker socket
(`loki.source.docker`) and pushes them to Loki with the labels `level`,
`logger` and (when present) `app`.

In Grafana Explore -> Loki, query e.g.:

```
{container="api"} | json | line_format "{{.level}} {{.logger}}: {{.message}}"
```

If a log line contains `"trace_id":"<id>"`, the Loki datasource's
`derivedFields` will render a clickable link that jumps straight into Tempo
with that trace id - this is the same wiring used by the **API Logs** panel of
the provisioned dashboard.

### Sample API dashboard

The dashboard **Sample API Observability** (uid `sample-api-observability`) is
provisioned automatically and contains:

| Panel                    | Source      | What it shows                                          |
|--------------------------|-------------|--------------------------------------------------------|
| Request Rate (req/s)     | Prometheus  | `sum(rate(http_server_request_duration_seconds_count))`|
| Success Rate             | Prometheus  | non-5xx / total                                        |
| Latency p99              | Prometheus  | `histogram_quantile(0.99, ...)`                        |
| 5xx Error Rate           | Prometheus  | `sum(rate(...{http_status_code=~"5.."}))`             |
| Request Rate by Route    | Prometheus  | stacked by `http_method`/`http_route`                 |
| Latency Percentiles      | Prometheus  | p50/p95/p99 by route                                   |
| API Logs                 | Loki        | `{container="api"}` live tail                         |
| Recent Traces            | Tempo       | TraceQL `{resource.service.name = "sample-api"}`       |

Direct URL: http://localhost:3000/d/sample-api-observability
