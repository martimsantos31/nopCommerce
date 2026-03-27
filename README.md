# nopCommerce — OpenTelemetry Observability

University assignment (Software Architecture course): adding OpenTelemetry observability to **nopCommerce**, a production ASP.NET Core e-commerce platform. The instrumented flow is **"Customer places an order"** (Basket → Order → Payment → Inventory).

---

## Documents

| Document | Description |
|----------|-------------|
| [ANALYSIS.md](ANALYSIS.md) | Architecture analysis, instrumentation strategy, custom metrics, PII policy |
| [REPORT.md](REPORT.md) | How to build, run, and observe the stack; load test instructions |
| [CRITIQUE.md](CRITIQUE.md) | Critical reflection on what helped and hindered instrumentation |

## Assessment Artifacts

| Artifact | Location |
|----------|----------|
| Architecture diagram | [assessment/diagrams/architecture.png](assessment/diagrams/architecture.png) |
| Grafana dashboard JSON | [assessment/observability/grafana/dashboards/checkout-flow.json](assessment/observability/grafana/dashboards/checkout-flow.json) |
| Dashboard screenshots | [assessment/dashboards/screenshots/](assessment/dashboards/screenshots/) |
| k6 load test script | [assessment/load-test/checkout-flow.js](assessment/load-test/checkout-flow.js) |
| Prometheus config | [assessment/observability/prometheus.yml](assessment/observability/prometheus.yml) |
| Presentation | [Presentation/](Presentation/) |

## Quick Start

```bash
docker compose up --build
```

See [REPORT.md](REPORT.md) for first-time setup, dashboard navigation, and load testing.
