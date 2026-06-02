# SOLUTION — Design & Trade-offs

## Overview

A single ASP.NET Core (.NET 8) project, folder-layered by responsibility:

```
Controllers → Services → Data (Dapper/SQL Server)
                  ↘ Soap (ICurrencyRateService) → CoreWCF-hosted SOAP service
```

- **Ingestion flow:** controller → `TradeIngestionService` validates → calls `ICurrencyRateService`
  for the rate → computes notional in base currency → persists idempotently via `TradeRepository`.
- **Reporting flow:** controller → `ReportingService` → one set-based SQL query → JSON.

Base currency is fixed to **USD**. Everything SOAP-related sits behind one interface so the
transport is swappable without touching the core.

## API choices

| Decision | Choice | Why |
|---|---|---|
| Endpoint style | MVC controllers | Conventional, easy to read/test; Swagger UI included. |
| JSON casing | `snake_case` | Matches the payload shapes in the brief (`external_id`, `trade_time`, `total_qty`…). |
| Ingest semantics | `201` on first create, `200` on idempotent resubmit | A resubmit is a **success**, not a conflict — it returns the stored trade so the caller can reconcile. (`409` was considered but is hostile for an at-least-once upstream.) |
| Report range | `from`/`to` dates, `to` day fully included | Intuitive for operations; implemented as a half-open `[from, to+1day)` window so boundaries are exact. |
| Avg price | Quantity-weighted: `SUM(qty*price)/SUM(qty)` | A plain mean of per-trade prices is misleading when trade sizes differ. |
| Data access | Dapper + hand-written SQL | The brief wants the report computed **in the database**; Dapper keeps the SQL (and the idempotency/concurrency mechanics) explicit and easy to audit. |

## Schema

Two tables (DDL in [`Sql/schema.sql`](./src/TradeCapture.Api/Sql/schema.sql)):

- **`CurrencyRates`** (`FromCurrency`, `ToCurrency`, `Rate`, `AsOf`, PK on all three) — the
  reference data the SOAP service owns; the seed populates it.
- **`Trades`** — the captured, enriched trade. Key columns:
  - `ExternalId` with a **`UNIQUE` constraint** — the idempotency key.
  - `Notional`, `NotionalBase`, `RateUsed`, `RateAsOf` — enrichment **persisted at ingest time**.
  - `CHECK` constraints (`Side IN ('BUY','SELL')`, `Quantity > 0`, `Price > 0`) as a last line of defence.
  - `IX_Trades_TradeTime` (including `Account`, `Symbol`) to support the report scan.

**Why store `NotionalBase` instead of computing it in the report:** trade reporting should be
point-in-time correct — the notional reflects the rate *as it was when the trade was captured*,
not today's rate. The report still aggregates set-based in SQL; it just sums an enriched column.

## Correctness: idempotency, concurrency, atomicity

These three guarantees from the brief are the heart of the design.

**1. Repeated submissions → no duplicate visible trades.**
Correctness lives in the database, not in app-level "check-then-insert" (which races). The insert
is *insert-if-absent*:

```sql
INSERT INTO dbo.Trades (...) SELECT @...
WHERE NOT EXISTS (SELECT 1 FROM dbo.Trades WHERE ExternalId = @ExternalId);
```

If `@@ROWCOUNT = 0`, the trade already existed → we re-select and return it as `AlreadyExists`.
**First write wins:** a resubmit with a different payload does not overwrite the original.

**2. Simultaneous submissions → consistent outcome.**
The `UNIQUE(ExternalId)` constraint is the backstop. If two requests both pass the `NOT EXISTS`
check at the same instant, exactly one insert succeeds; the loser raises a duplicate-key error
(SQL `2627`/`2601`), which `TradeRepository` catches and converts into the same "already exists,
return the stored row" outcome. The caller never sees a `500`, and only one row can ever exist.

**3. Failed submission → no partial visible effects.**
Each ingest runs in a single explicit transaction (`READ COMMITTED`). The **SOAP call happens
before the transaction opens** — it is a read, and we never want a network call holding a
transaction open. If enrichment fails, no transaction is started and nothing is persisted. If the
insert fails, the transaction rolls back. Either way there are no partial, visible effects.

> Ordering note: because the duplicate-loser re-selects inside its own transaction, under
> `READ COMMITTED` it briefly waits for the winner to commit, then reads the committed row —
> so the response always reflects a fully-stored trade.

The concurrency guarantee is demonstrated live by `scripts/concurrency-check.ps1`, which fires N
simultaneous identical submissions and confirms a single resulting report row.

## SOAP integration approach

The enrichment seam is `ICurrencyRateService`. Two implementations sit behind it, selected by the
`Enrichment:Provider` config setting — nothing in the ingestion/reporting code is aware of which is
active, or that SOAP is involved at all. That decoupling is the whole point.

**`Soap` (default): real CoreWCF host + WCF client.**
- **Server:** `CurrencyRateSoapService` is hosted in-process by **CoreWCF** at `/soap/currency`
  (SOAP 1.1 / `BasicHttpBinding`), with WSDL published at `/soap/currency?wsdl`. It owns the
  currency reference data — it is the "legacy SOAP service" the API integrates with, and the only
  component that touches the `CurrencyRates` table.
- **Client:** `WcfCurrencyRateService` implements `ICurrencyRateService` by calling that endpoint
  through a `System.ServiceModel.ChannelFactory`, adapting the SOAP DTO to the domain model. A
  missing rate comes back as a nil result, which is mapped to `RateNotFoundException` → HTTP 422.

**`Stub`: in-process fake.** `StubCurrencyRateService` reads the rate table directly with no SOAP.
Kept for fast local runs and to show the seam is a genuine swap — flip one config value:

```jsonc
// appsettings.json
"Enrichment": { "Provider": "Soap" }   // or "Stub"
```

### The CoreWCF / WCF-client contract split (a WCF gotcha worth calling out)

CoreWCF (server) and `System.ServiceModel` (client) each define their *own* `[ServiceContract]` /
`[OperationContract]` attribute types. A single shared interface can't carry both, so there are two
structurally-identical contracts:

- `ICurrencyRateSoapContract` — CoreWCF attributes (server).
- `ICurrencyRateSoapClient` — `System.ServiceModel` attributes (client).

They interoperate because both pin the **same `Name`, `Namespace`, and explicit SOAP `Action`**
(see `CurrencyRateContract`). The data type (`CurrencyRateDto`) *is* shared — `[DataContract]`
lives in `System.Runtime.Serialization`, common to both. In a "consume an external WSDL" scenario
the client contract would instead be generated by `dotnet-svcutil`; sharing it here keeps the
single-project demo self-contained.

*Why stub-first, then SOAP:* building the core against the stub removed the unfamiliar WCF piece
from the critical path; because everything was already behind the seam, adding the real CoreWCF
host and client was an additive, low-risk step with no changes to ingestion or reporting.

### How a real (external) SOAP endpoint would be wired in

The legacy service would run as its own process/deployable. The only changes here would be:
1. Point `Soap:CurrencyServiceUrl` at the external endpoint.
2. Generate the client contract from its published WSDL with `dotnet-svcutil` (replacing the
   hand-written `ICurrencyRateSoapClient`), and align binding/security to match.

The in-process CoreWCF host could then be removed entirely; `WcfCurrencyRateService` and the rest
of the application stay unchanged.

## Operability

- **Turnkey startup:** `DbInitializer` creates the database if missing and runs the idempotent
  `schema.sql` + `seed.sql`, so a fresh clone runs with one `dotnet run`. (In production this would
  be a migrations step — DbUp/EF migrations — rather than app startup.)
- **Seed data:** reference rates + three sample trades so the report returns data immediately.
- **Discoverability:** Swagger UI at `/swagger`; SOAP WSDL at `/soap/currency?wsdl`; ready-to-run
  HTTP and SOAP samples in `TradeCapture.Api.http`.
- **Secrets:** no credentials in source; Windows auth by default, overridable via environment
  variable / user-secrets. `.gitignore` excludes `.env`, `secrets.json`, local app settings.

## Testing

- **Unit tests** (no DB, fast): enrichment math (base and foreign currency), idempotent resubmit
  returns the original, validation rules, and the report date-range translation.
- **SOAP / WCF client:** verified via the SOAP entries in `TradeCapture.Api.http` (direct `GetRate`
  call returns `Rate = 1.09`; unknown currency returns a nil result), and end-to-end by ingesting a
  foreign-currency trade with `Enrichment:Provider = Soap`.
- **Concurrency demonstration:** `scripts/concurrency-check.ps1` exercises the real SQL Server
  constraint, where the idempotency guarantee actually lives.

## Trade-offs & "what I'd do next"

- **DB init on startup** is convenient for a demo but is not how I'd manage schema in production —
  I'd use a dedicated migrations tool run during deployment.
- **SOAP host is in-process.** The CoreWCF service runs inside the same app and the client calls it
  over loopback HTTP. This is a faithful SOAP round-trip but co-located; a real deployment would
  host the legacy service separately (see "how a real SOAP endpoint would be wired in").
- **Synchronous SOAP operation** wrapped in a completed `Task`; a high-throughput client would use
  an async contract and pool channels.
- **Rate lookup hits the DB per call**; a real client would likely cache rates. The seam isolates this.
- **Faults:** a missing rate is signalled by returning a nil result (mapped to `422`); a production
  contract would likely use a typed `FaultContract`.
- **No auth / rate-limiting / observability**, and **batch ingestion** (optional extra) not built —
  out of scope; batch would reuse the same insert-if-absent logic per item.
