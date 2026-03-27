### 3. How to Build, Run, and View the Dashboard

#### Prerequisites

- Docker and Docker Compose
- k6 (for load testing): `brew install k6`

#### Start the Stack

```bash
docker compose up --build
```

This starts:

| Service | URL | Purpose |
|---------|-----|---------|
| nopCommerce | http://localhost:80 | The e-commerce application |
| Jaeger UI | http://localhost:16686 | Trace viewer |
| Grafana | http://localhost:3001 | Dashboard (admin/admin) |
| Prometheus | http://localhost:9090 | Metrics storage |

#### First-Time Setup

1. Open http://localhost:80 — the installation wizard appears
2. Database type: **SQL Server**
3. Connection string (select "Enter raw connection string"):
   ```
   Server=nopcommerce_database;Database=nopcommerce;User Id=sa;Password=nopCommerce_db_password;TrustServerCertificate=True;
   ```
4. Check **"Install sample data"**
5. Click **Install** (takes 1-2 minutes)
6. After installation, go to Admin → Settings → Order Settings → set **Minimum order placement interval** to **0** (for load testing)

#### View the Dashboard

1. Open Grafana at http://localhost:3001
2. Navigate to folder **nopCommerce** → dashboard **Checkout Flow Observability**
3. Place an order manually to see data populate
4. The dashboard auto-refreshes every 10 seconds

#### Verify the Pipeline

- `http://localhost:80/metrics` — Prometheus metrics endpoint (should show `nopcommerce_checkout_*` metrics)
- `http://localhost:16686` — Jaeger UI, select service **NopCommerce**
- `http://localhost:9090/targets` — Prometheus targets, nopcommerce should be **UP**

---

### 4. Load Test

#### Run the Load Test

```bash
k6 run assessment/load-test/checkout-flow.js
```

The k6 script drives the full checkout flow end-to-end:
1. Log in as admin
2. Add a random product to cart
3. Set checkout attributes (gift wrapping = No)
4. Complete one-page checkout (billing → shipping → payment → confirm)

Each iteration triggers `PlaceOrderAsync`, generating traces and all custom metrics.

#### What to Watch During the Load Test

- **Grafana**: checkout step duration, outcome rates, business indicators all populate live
- **Jaeger**: select service "NopCommerce", operation "Checkout PlaceOrder" to see full span waterfall
