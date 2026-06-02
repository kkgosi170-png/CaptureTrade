# TradeCapture — Back Office Trade Capture & Reporting

A small back-office system that captures trades over HTTP, enriches them with a currency rate
from a (SOAP) reference-data service, persists them idempotently in SQL Server, and exposes a
database-computed reporting endpoint.

> **Stack:** C# / .NET 8 (ASP.NET Core MVC controllers) · Dapper · SQL Server Express · Windows.
> The SOAP enrichment currently runs as an in-process **stub** behind an interface; see
> [`SOLUTION.md`](./SOLUTION.md) for how a real WCF/CoreWCF endpoint plugs in (Phase 2).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server Express** running locally as `.\SQLEXPRESS` with Windows authentication
  (the default in `appsettings.json`).

If your instance name or auth differs, edit the connection string in
[`src/TradeCapture.Api/appsettings.json`](./src/TradeCapture.Api/appsettings.json), or override it
without touching source via user-secrets / environment variable:

```powershell
# environment variable override (note the double underscore)
$env:ConnectionStrings__TradeCapture = "Server=.\SQLEXPRESS;Database=TradeCapture;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

> No credentials are stored in source. Windows authentication is used by default; if you switch
> to SQL auth, keep the password out of the repo (user-secrets or an environment variable).

## Run

```powershell
cd src/TradeCapture.Api
dotnet run
```

On startup the app **creates the `TradeCapture` database if missing** and applies the idempotent
schema + seed scripts (`Sql/schema.sql`, `Sql/seed.sql`). No manual DB setup needed. The app
listens on **`http://localhost:5000`** (and `https://localhost:5001`), configured in
`Properties/launchSettings.json`.

## Try it

**Easiest — Swagger UI:** browse to **<http://localhost:5000/swagger>**. Every endpoint is listed
with editable example payloads; click *Try it out* → *Execute*. (Swagger is enabled in the
Development environment, which `launchSettings.json` sets by default.)

Sample requests are also in
[`src/TradeCapture.Api/TradeCapture.Api.http`](./src/TradeCapture.Api/TradeCapture.Api.http)
(open in VS / VS Code REST Client), or use PowerShell:

```powershell
# Ingest a trade (first time -> 201 Created)
Invoke-RestMethod -Uri http://localhost:5000/api/trades -Method Post -ContentType application/json -Body '{
  "external_id":"T-001","account":"ACC-123","symbol":"MSFT","side":"BUY",
  "quantity":100,"price":310.25,"trade_time":"2025-01-15T10:30:00Z","currency":"USD"}'

# Resubmit the same external_id -> 200 OK, no duplicate created

# Report grouped by account + symbol over a date range
Invoke-RestMethod -Uri "http://localhost:5000/api/reports/trades?from=2025-01-15&to=2025-01-16"
```

### Demonstrate idempotency under concurrency

With the API running, fire many simultaneous identical submissions and confirm one row results:

```powershell
./scripts/concurrency-check.ps1 -BaseUrl http://localhost:5000 -Count 25
```

You should see a mix of `201`/`200` responses but a **single** report row with `total_qty = 100`.

## Test

```powershell
dotnet test
```

Unit tests cover the enrichment math, idempotent resubmit behaviour, validation, and the report's
date-range translation. (They use in-memory fakes and need no database.)

## API summary

| Method & path | Purpose | Success | Errors |
|---|---|---|---|
| `POST /api/trades` | Ingest one trade (idempotent on `external_id`) | `201` new, `200` already existed | `400` validation, `422` no rate |
| `GET /api/reports/trades?from=&to=` | Account+symbol report over a date range | `200` JSON | `400` bad/missing dates |

Dates are `YYYY-MM-DD`; the range includes the whole `to` day.

## Project layout

```
src/TradeCapture.Api/
  Program.cs            DI wiring, JSON (snake_case), Swagger, startup DB init
  Controllers/          MVC controllers (TradesController, ReportsController)
  Services/             ingestion + reporting logic, domain exceptions
  Data/                 Dapper repository, connection factory, DbInitializer
  Soap/                 ICurrencyRateService seam + stub implementation
  Models/               request/response/domain records
  Sql/                  idempotent schema.sql + seed.sql
tests/TradeCapture.Tests/   xUnit unit tests
scripts/concurrency-check.ps1
```
