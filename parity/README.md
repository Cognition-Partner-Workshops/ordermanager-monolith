# Contract / Parity Harness

This is the **acceptance gate** for every route group migrated from the legacy
.NET backend (`src/OrderManager.Api`) to the Java Spring Boot backend
(`server-java`). It captures the .NET JSON responses for the full endpoint
surface and asserts the Java backend returns the **same HTTP status and the same
JSON shape** (for successful responses), so the Angular SPA behaves identically
before and after each cutover.

## Files
- `parity_harness.py` — the request suite + capture/compare/diff engine (stdlib only).
- `run_parity.sh` — builds and boots BOTH backends against isolated, freshly
  seeded databases, then runs the comparison and tears everything down.
- `fixtures/dotnet/responses.json` — committed **golden** .NET capture (baseline).

## Run it
```bash
# Fully automated: build + boot both backends + diff (recommended).
./parity/run_parity.sh
```
Or drive the pieces manually against already-running backends:
```bash
# capture one backend
python3 parity/parity_harness.py capture --base-url http://localhost:5001 --out parity/fixtures/dotnet

# compare live Java against live .NET
python3 parity/parity_harness.py compare --dotnet-url http://localhost:5001 --java-url http://localhost:5000

# compare live Java against the committed golden .NET fixtures (works after .NET is retired)
python3 parity/parity_harness.py compare --dotnet-dir parity/fixtures/dotnet --java-url http://localhost:5000
```
Exit code is `0` only when every **contract** endpoint matches.

## Databases: NOT literally shared
The two backends use **different physical schemas** — EF Core uses PascalCase
columns (`CustomerId`, `ZipCode`, `CreatedAt`); Hibernate + Flyway use snake_case
(`customer_id`, `zip_code`, `created_at`). They therefore cannot read the same
SQLite file directly. The harness runs each backend against its **own** freshly
seeded DB. Because both seed identical logical data in the same insert order,
primary keys line up deterministically; only timestamps differ, and those are
normalized. (`server-java/scripts/migrate-data.sh` copies data between the two
schemas for a real cutover.)

## What "same JSON shape" means (normalization)
To ignore differences that don't affect the Angular contract, both responses are
canonicalized before diffing:
- timestamp values (`createdAt`, `orderDate`, `lastRestocked`) → sentinel (key
  presence is still compared, only the value is ignored);
- `null` values, empty arrays/objects, and `null` array elements are dropped —
  this collapses .NET `ReferenceHandler.IgnoreCycles` artifacts (e.g. `orders:[null]`);
- **error responses (non-2xx) are compared by status only** — the bodies differ
  by framework (.NET `ProblemDetails` vs Spring error JSON) and are not part of
  the contract the SPA reads.

## Current baseline (Phase 0)
`16 / 20` contract endpoints pass. The remaining items are **service-layer
include-strategy** differences to be closed by the module child sessions — in
every case the Java service currently returns *more* nested data than .NET:

| Endpoint | Delta | Owner |
|----------|-------|-------|
| `GET /api/orders`, `GET /api/orders/{id}` | Java includes `items[].product.inventory`; .NET leaves it null (its query fetches Customer + Items→Product only) | Phase 4 (Orders) |
| `PATCH /api/orders/{id}/status` | Java returns the full order graph; .NET returns `customer:null`, `items:[]` (it reloads via `FindAsync` with no includes) | Phase 4 (Orders) |
| `POST /api/inventory/product/{id}/restock` | Java includes `product`; .NET returns it null (no `Include(Product)`) | Phase 3 (Inventory) |

Fixed at the entity/seed layer in Phase 0 (orchestrator owns shared entities):
`createdAt` on Customer/Product, `lineTotal`/`orderId`/`productId` on OrderItem,
`customerId` on Order, and seeded `lastRestocked` on InventoryItem.

## Intentional divergences (reported, never fail the run)
The .NET backend has no exception middleware, so business/validation errors
surface as **HTTP 500**. The Java backend maps them via `@ControllerAdvice`:
- insufficient stock → `409` (.NET `500`)
- unknown customer / product on create/restock → `404` (.NET `500`)

These are intended improvements and are labelled `diverge` in the report.
