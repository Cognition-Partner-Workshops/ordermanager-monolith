# Strangler-Fig Migration Workflow (.NET → Java)

This document defines how the incremental migration is executed after **Phase 0**
(the orchestrator skeleton, facade, and parity harness) is in place. It is the
operating manual for the per-module child sessions and the orchestrator that
merges them.

> **Golden rule:** the Angular SPA is never edited. Every cutover happens behind
> the facade (`facade/`), verified by the parity harness (`parity/`).

## Decision stated to every child session
- **The orchestrator owns the shared JPA entities and repositories.** All five
  entities (`Customer`, `Product`, `InventoryItem`, `Order`, `OrderItem`) and
  their repositories already exist in `server-java` and are parity-correct at the
  field level (see `parity/README.md`). **Child sessions do not redefine entities
  or the Flyway schema** — they add/adjust only `service` + `controller` + tests
  for their route group. If a genuine entity change is unavoidable, coordinate it
  through the orchestrator so parallel sessions don't conflict.
- Databases are **not** a single shared file — each backend owns its own schema
  (EF PascalCase vs Hibernate snake_case). Do not try to point both at one file.

## Per-module loop
Each module (Customers, Products, Inventory, Orders) is one child session:

1. **Branch** from the Phase 0 skeleton branch (`devin/1773358765-initial-monolith`).
2. **Implement** the route group's `service` + `@RestController` so its endpoints
   match the .NET contract (includes, status codes, JSON shape). Add JUnit unit
   tests and, where useful, `@SpringBootTest` integration tests.
3. **Gate:** run `./parity/run_parity.sh` and make **your** route group's contract
   endpoints pass. The report labels each endpoint `CONTRACT`/`diverge`; only
   error-handling `diverge` rows may stay red.
4. **Deliver** the branch (PR). The orchestrator reviews + merges.
5. **Cut over:** the orchestrator flips the route group to Java behind the facade
   and reloads:
   ```bash
   ./facade/flip-route.sh <group> java --reload
   ```
   then re-runs the harness end-to-end and smoke-tests the SPA through `:8080`.

## Ordering & parallelism
- **Customers, Products, Inventory** can proceed in parallel (Inventory and
  Products share the `Product` entity, already owned by the orchestrator).
- **Orders must go last** — it depends on Customer/Product/Inventory and is the
  write-heavy group; flipping it last minimizes the concurrent-write window on
  SQLite (single-writer).

## Outstanding parity work by module (as of Phase 0)
16/20 contract endpoints already pass. Remaining gaps are all "Java returns more
nested data than .NET" and belong to the module services:

- **Orders (Phase 4)** — `GET /api/orders`, `GET /api/orders/{id}`: don't fetch
  `product.inventory` (match .NET's Customer + Items→Product includes).
  `PATCH /api/orders/{id}/status`: return the order as .NET does (reloaded without
  includes → `customer:null`, `items:[]`), or confirm the SPA is unaffected and
  record the deviation.
- **Inventory (Phase 3)** — `POST /api/inventory/product/{id}/restock`: return the
  item without the nested `product` (match .NET, which does no `Include(Product)`).

## Phase 5 — retire .NET (orchestrator)
Once all four groups point at Java and the harness is green: transfer DB
seeding/ownership to Java (`DataSeeder`), remove `src/OrderManager.Api` and
`tests/OrderManager.Api.Tests`, point `spa` at Java (`flip-route.sh spa java`),
and update `README.md` build/run instructions (Maven instead of `dotnet`).
