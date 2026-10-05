# Inventory extraction — impact assessment

**Scope:** everything in this repository that would move, change, or need a seam if the Inventory module were extracted into its own service. No code has been moved; this document is the pre-extraction record.

**Baseline commit:** `1107aff` (`main`). All file/line references are against that commit.

**Method:** full read of `src/`, `client-app/src/` and `tests/`, plus `grep -rni inventory` across the tree; endpoint shapes were captured by running the API locally against the SQLite seed data and calling each route with `curl`; table DDL was read from the SQLite file EF Core creates via `EnsureCreated()`.

---

## 1. Summary

| Area | Count | Notes |
|---|---|---|
| Controllers referencing Inventory | 1 direct (`InventoryController`) + 1 indirect (`OrdersController` via `OrderService`) | `ProductsController` returns Inventory data in its payload via `ProductService` |
| Services referencing Inventory | 3 (`InventoryService`, `OrderService`, `ProductService`) | `OrderService` writes stock; `ProductService` only reads |
| EF Core entities | 1 owned (`InventoryItem`) + 1 coupled (`Product.Inventory` navigation) | Single `AppDbContext` for all modules |
| Database tables | 1 owned (`InventoryItems`), 1 shared FK target (`Products`) | `Orders`/`OrderItems` have **no** FK to `InventoryItems`; coupling is in code, not schema |
| Public endpoints under `/api/inventory` | 4 | All anonymous; no versioning |
| Angular components | 1 direct (`InventoryListComponent`) + 1 indirect (`ProductListComponent` reads `product.inventory`) + nav/route entries | |
| Tests referencing Inventory | 2 of 3 xUnit tests (`OrderServiceTests`) | Test project does not compile on `main` — see §9 |
| Orders call sites that become HTTP/RPC | 1 block in `OrderService.CreateOrderAsync` (read + check + decrement, per line item) | Inside the same transaction as order insert today |
| Dead feature flags / config toggles | 2 files (`environment.ts`, `environment.prod.ts`) | Removed in a separate PR; no `if` branches or tests existed for them |

---

## 2. Controllers

| Controller | File | References Inventory how | Extraction impact |
|---|---|---|---|
| `InventoryController` | `src/OrderManager.Api/Controllers/InventoryController.cs` | Owns all 4 `/api/inventory` routes; injects `InventoryService`; defines `RestockRequest` record (line 38) | **Moves** to the new service wholesale |
| `OrdersController` | `src/OrderManager.Api/Controllers/OrdersController.cs` | No direct reference. `POST /api/orders` → `OrderService.CreateOrderAsync`, which mutates `InventoryItems` (see §8) | Stays; its `POST` becomes dependent on the Inventory service being reachable |
| `ProductsController` | `src/OrderManager.Api/Controllers/ProductsController.cs` | No direct reference. All GET routes return `Product` with `inventory` populated by `ProductService` (`Include(p => p.Inventory)`) | Stays; payload shape changes unless Products composes Inventory data over HTTP (see §7) |
| `CustomersController` | `src/OrderManager.Api/Controllers/CustomersController.cs` | None | None |

---

## 3. Services

| Service | File | Inventory reference | Lines | Read / Write | Extraction impact |
|---|---|---|---|---|---|
| `InventoryService` | `src/OrderManager.Api/Services/InventoryService.cs` | Owns `InventoryItems` DbSet; `GetAllInventoryAsync`, `GetInventoryByProductIdAsync`, `RestockAsync`, `GetLowStockItemsAsync` | 16–41 | R/W | **Moves**. All four methods `Include(i => i.Product)` except `RestockAsync` — the new service needs product name/sku/price either replicated or fetched from Products |
| `OrderService` | `src/OrderManager.Api/Services/OrderService.cs` | `_context.InventoryItems.FirstOrDefaultAsync(...)`, stock check, `inventory.QuantityOnHand -= quantity` | 49–55 | R/W | Becomes an HTTP call (§8). Loses same-transaction atomicity with `Orders`/`OrderItems` insert at line 66–67 |
| `ProductService` | `src/OrderManager.Api/Services/ProductService.cs` | `.Include(p => p.Inventory)` in `GetAllProductsAsync`, `GetProductByIdAsync`, `GetProductsByCategoryAsync` | 18, 23, 35 | R | Include must be removed or replaced with a fan-out read to Inventory |
| `CustomerService` | `src/OrderManager.Api/Services/CustomerService.cs` | None | — | — | None |

DI registration: `builder.Services.AddScoped<InventoryService>()` — `src/OrderManager.Api/Program.cs:13`.

---

## 4. EF Core entities and DbContext

| Type | File | Inventory coupling | Extraction impact |
|---|---|---|---|
| `InventoryItem` | `src/OrderManager.Api/Models/InventoryItem.cs` | Owned entity. `ProductId` (FK) + `Product` navigation (`null!`) | Moves. `Product` navigation cannot survive a DB split; replace with `ProductId` only (plus denormalised name/sku if the UI needs them) |
| `Product` | `src/OrderManager.Api/Models/Product.cs:12` | `public InventoryItem? Inventory { get; set; }` — back-navigation | Remove from the monolith's `Product`; this silently changes the JSON of every `/api/products` response (the `inventory` property disappears) |
| `Order`, `OrderItem`, `Customer` | `src/OrderManager.Api/Models/` | No navigation to `InventoryItem` | None at the entity level |
| `AppDbContext` | `src/OrderManager.Api/Data/AppDbContext.cs` | `DbSet<InventoryItem> InventoryItems` (line 14); `OnModelCreating` one-to-one `Product ↔ InventoryItem` via `HasForeignKey<InventoryItem>(e => e.ProductId)` (lines 50–54) | DbSet + mapping move to a new `InventoryDbContext`; mapping of `Product.Inventory` removed here |
| `SeedData` | `src/OrderManager.Api/Data/SeedData.cs:32–40` | Seeds one `InventoryItem` per seeded `Product` using the generated `Product.Id` | Seeding becomes cross-service: Inventory seed needs the product IDs that Products assigns. Also `SeedData.Initialize` is the fixture for every test (§9) |

---

## 5. Database tables shared with Orders and Products

Schema as created by `EnsureCreated()` (SQLite). EF uses the DbSet names as table names.

| Table | Owner after extraction | Columns | Relationship to Inventory | Shared with |
|---|---|---|---|---|
| `InventoryItems` | **Inventory** | `Id` PK, `ProductId` INT NOT NULL, `QuantityOnHand`, `ReorderLevel`, `WarehouseLocation`, `LastRestocked` | — | `FK_InventoryItems_Products_ProductId → Products(Id) ON DELETE CASCADE`; `IX_InventoryItems_ProductId` UNIQUE |
| `Products` | Products | `Id`, `Name`, `Description`, `Category`, `Price`, `Sku`, `CreatedAt` | FK target of `InventoryItems.ProductId` | Read by Inventory (`Include(i => i.Product)`), Orders (`OrderItems.ProductId`), Products |
| `Orders` | Orders | `Id`, `CustomerId`, `OrderDate`, `Status`, `TotalAmount`, `ShippingAddress` | None in schema | Orders, Customers |
| `OrderItems` | Orders | `Id`, `OrderId`, `ProductId`, `Quantity`, `UnitPrice` | None in schema; `OrderService` decrements `InventoryItems.QuantityOnHand` in the **same `SaveChangesAsync`** that inserts these rows | Orders, Products |
| `Customers` | Customers | … | None | Orders |

Key facts for the split:

- There is **no FK or join between `Orders`/`OrderItems` and `InventoryItems`**. The Orders↔Inventory coupling is purely procedural (`OrderService` lines 49–55) and transactional (one `SaveChangesAsync` at line 67).
- `InventoryItems.ProductId` has a hard FK with `ON DELETE CASCADE` to `Products`. When `InventoryItems` moves to its own database this FK is lost; deleting a product will no longer remove its stock row, and `ProductId` becomes an unenforced reference.
- The one-to-one is enforced by `IX_InventoryItems_ProductId UNIQUE`; keep that index in the new store.

---

## 6. Public endpoints under `/api/inventory`

Routes are produced by `[Route("api/[controller]")]`; ASP.NET Core routing is case-insensitive (`/api/inventory` and `/api/Inventory` both work; Swagger emits `/api/Inventory`). No auth, no versioning, no pagination. JSON is `System.Text.Json` with `ReferenceHandler.IgnoreCycles` (`Program.cs:15–16`), camelCase.

| Method & route | Handler | Request | Success response | Error behaviour |
|---|---|---|---|---|
| `GET /api/inventory` | `InventoryController.GetAll` → `GetAllInventoryAsync` | none | `200` `InventoryItem[]` (with `product` populated) | — |
| `GET /api/inventory/product/{productId}` | `GetByProduct` → `GetInventoryByProductIdAsync` | path `productId:int` | `200` `InventoryItem` (with `product`) | `404` empty body when no row |
| `POST /api/inventory/product/{productId}/restock` | `Restock` → `RestockAsync` | path `productId:int`; body `{ "quantity": int }` (`RestockRequest`) | `200` `InventoryItem` **with `product: null`** (no Include on this path); `lastRestocked` set to `UtcNow` (`Z` suffix) | **`500`** (unhandled `ArgumentException`) when no inventory row for the product. No validation of negative/zero quantity |
| `GET /api/inventory/low-stock` | `GetLowStock` → `GetLowStockItemsAsync` | none | `200` `InventoryItem[]` where `quantityOnHand <= reorderLevel` (with `product`) | — |

### `InventoryItem` response shape (observed)

```json
{
  "id": 1,
  "productId": 1,
  "product": {
    "id": 1, "name": "Widget A", "description": "Standard widget",
    "category": "Widgets", "price": 9.99, "sku": "WGT-001",
    "createdAt": "2026-10-05T07:33:40.6608386",
    "inventory": null,
    "orderItems": []
  },
  "quantityOnHand": 50,
  "reorderLevel": 10,
  "warehouseLocation": "A-01",
  "lastRestocked": "2026-10-05T07:33:40.7964449"
}
```

Contract notes the new service must honour (or consciously break with a version bump):

- `product` is the full `Product` entity, including the always-null `inventory` back-reference and an always-empty `orderItems` array (artifacts of serialising the EF entity). Consumers today: `InventoryListComponent` reads only `product.name`.
- `lastRestocked`/`createdAt` are serialised without a timezone suffix when read from SQLite, but with `Z` when freshly set in-process (restock response). Clients already tolerate both.
- Error semantics are leaky (`500` on unknown product in restock). An extracted service will probably want `404`; that is a contract change for any caller that currently retries on 5xx.

### `RestockRequest` (Swagger schema)

```json
{ "type": "object", "properties": { "quantity": { "type": "integer", "format": "int32" } } }
```

---

## 7. Angular components

| Component / file | Inventory reference | Extraction impact |
|---|---|---|
| `InventoryListComponent` — `client-app/src/app/modules/inventory/inventory-list.component.ts` | `this.http.get<any[]>('/api/inventory')` (line 24); template binds `i.product?.name`, `i.quantityOnHand`, `i.reorderLevel`, `i.warehouseLocation`, `i.lastRestocked`; low-stock CSS class when `quantityOnHand <= reorderLevel` | URL moves to the new service (or a gateway). Depends on `product.name` being embedded in the inventory payload |
| `ProductListComponent` — `client-app/src/app/modules/products/product-list.component.ts:15` | `{{p.inventory?.quantityOnHand ?? 'N/A'}}` "Stock" column, sourced from `GET /api/products` | Will show `N/A` for every row once `Product.Inventory` is removed, unless Products composes stock from the Inventory service or the UI makes a second call |
| `app.routes.ts:8` | `{ path: 'inventory', loadComponent: () => import('./modules/inventory/inventory-list.component') }` | Unchanged if the UI stays a single SPA; becomes a micro-frontend boundary otherwise |
| `app.component.ts:14` | `<a routerLink="/inventory">Inventory</a>` nav link | None |
| `OrderListComponent`, `CustomerListComponent` | None | None |

All components use untyped `any[]` payloads and relative URLs (`/api/...`); `environment.apiUrl` is **not** used anywhere (see §10). Routing inventory calls to a different host therefore needs either a reverse proxy in front of both services or the introduction of a base-URL configuration the app does not currently read.

---

## 8. Orders call sites that turn into HTTP calls

All in `OrderService.CreateOrderAsync` (`src/OrderManager.Api/Services/OrderService.cs`), executed **per line item** inside the `foreach` at line 44:

| Line(s) | Today (in-process, same DbContext) | After extraction | Semantics at risk |
|---|---|---|---|
| 49–50 | `_context.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId)` → `InvalidOperationException("No inventory record for product {id}")` | `GET inventory/product/{productId}` → map `404` to the same exception | Error type/message currently surfaces as a `500` from `POST /api/orders` |
| 52–53 | `if (inventory.QuantityOnHand < quantity) throw InvalidOperationException("Insufficient stock for {product.Name}. Available: {qty}")` | Either client-side check on the fetched quantity (racy) or a new server-side **reserve/decrement** endpoint that fails atomically | Check-then-act race between two concurrent orders becomes possible across services; today it is protected only by SQLite's single-writer lock |
| 55 | `inventory.QuantityOnHand -= quantity;` (persisted by the single `SaveChangesAsync` at line 67 along with the `Order` + `OrderItems`) | `POST inventory/product/{productId}/decrement` (does not exist yet — only `restock` exists) | **Loss of atomicity**: if the order insert fails after stock was decremented, stock is lost; if decrement fails after order insert, oversell. Needs compensation (saga) or outbox |
| 67 | `await _context.SaveChangesAsync()` commits Orders, OrderItems and InventoryItems together | Commits Orders/OrderItems only | Multi-item orders: today all-or-nothing across N products; afterwards N independent HTTP decrements |

Required new Inventory API surface for Orders (does not exist today): an idempotent **decrement / reserve** operation keyed by `productId` and `quantity` that returns a conflict status on insufficient stock. The existing `restock` endpoint is the only mutation and only adds.

Secondary call site — serialisation leak: the `POST /api/orders` response currently embeds `items[].product.inventory` (observed: `quantityOnHand: 53` after the test order), because the tracked `InventoryItem` is fixed up into `Product.Inventory` within the same DbContext. After extraction this property disappears from the Orders response. No client reads it today.

---

## 9. Baseline the extraction has to hold

Captured on `main` @ `1107aff`, .NET SDK 10.0.401 (targets `net8.0`), Node 20.20.2, Angular CLI 17.3.17.

| Check | Command | Result | Notes |
|---|---|---|---|
| API build (Debug) | `dotnet build src/OrderManager.Api/OrderManager.Api.csproj` | **0 warnings, 0 errors** | `Nullable` + `ImplicitUsings` enabled; no analyzers/`TreatWarningsAsErrors` configured |
| API build (Release) | `dotnet build … -c Release` | **0 warnings, 0 errors** | |
| Solution build | `dotnet build OrderManager.sln` | **FAILS — 6 × CS0246** in `tests/OrderManager.Api.Tests/OrderServiceTests.cs` (`Fact`/`FactAttribute` not found) | Test file has no `using Xunit;` and the csproj has no `<Using Include="Xunit" />`. Several unmerged branches already exist for this fix (`devin/*-fix-test-missing-xunit-import`, `devin/*-fix-missing-xunit-using`, `devin/*-nm2-add-xunit-import`) |
| Tests (as on `main`) | `dotnet test OrderManager.sln` | **0 discovered / 0 run** — build failure above | |
| Tests (with the one-line `using Xunit;` fix applied locally, not committed) | `dotnet test OrderManager.sln` | **3 total, 3 passed, 0 failed, 0 skipped**, 0 build warnings | `GetAllOrders_ReturnsEmptyList_WhenNoOrders`, `CreateOrder_DeductsInventory`, `CreateOrder_ThrowsOnInsufficientStock` — the last two assert Inventory behaviour through `OrderService` and use the EF InMemory provider with `SeedData.Initialize` |
| Angular build | `npx ng build` (outputs to `src/OrderManager.Api/wwwroot`) | **Success, 0 warnings**; 4 lazy chunks incl. `inventory-list-component` (1.39 kB) | `wwwroot/` is not git-ignored; the build drops untracked files into the API project |
| Angular type-check | `npx tsc -p tsconfig.app.json --noEmit` | **clean** (`strict`, `strictTemplates` on) | |
| Angular lint | `npx ng lint` | **Not configured** — no `lint` target in `angular.json`, no ESLint/`@angular-eslint` installed, no `.eslintrc`/`.prettierrc`/`.editorconfig` | There is no lint gate to hold; adding one is a pre-extraction option |
| Angular unit tests | `npx ng test` | **Not configured** — no `test` target in `angular.json`, 0 `*.spec.ts` files, Karma/Jasmine not installed (`package.json` `test` script is dead) | |
| Backend lint/format | — | No `.editorconfig`, no `dotnet format` configuration, no analyzers | |
| CI | — | No `.github/workflows`, no pre-commit hooks | Nothing enforces the above automatically |

### Tests referencing Inventory

| Test | File:line | What it pins | Extraction impact |
|---|---|---|---|
| `CreateOrder_DeductsInventory` | `tests/OrderManager.Api.Tests/OrderServiceTests.cs:29–43` | Ordering 5 units reduces `InventoryItems.QuantityOnHand` by 5 via the shared `AppDbContext` | Must be rewritten against a fake/stub Inventory client; cannot read `context.InventoryItems` once the table leaves the Orders DB |
| `CreateOrder_ThrowsOnInsufficientStock` | `OrderServiceTests.cs:45–54` | `InvalidOperationException` when quantity exceeds stock | Same; exception mapping from HTTP conflict must preserve the type (or the test changes) |
| `GetAllOrders_ReturnsEmptyList_WhenNoOrders` | `OrderServiceTests.cs:20–27` | Unrelated to Inventory, but fixture (`CreateContext` → `SeedData.Initialize`) seeds `InventoryItems` | Fixture needs splitting when `SeedData` is split |

There are **no tests for `InventoryService` or `InventoryController`**. Characterisation tests for the 4 endpoints in §6 (including the `404` and `500` cases) should be added **before** moving code so the contract is pinned.

---

## 10. Feature flags and configuration toggles audit

Every configuration read in the codebase was enumerated.

| Toggle / setting | Where defined | Where read | Verdict | Action |
|---|---|---|---|---|
| `environment.production` (`false` / `true`) | `client-app/src/environments/environment.ts`, `environment.prod.ts` | **Nowhere.** No `import … from 'environments/…'` in `src/`; `angular.json` has no `fileReplacements` and no `configurations`, so `environment.prod.ts` is never swapped in. `tsconfig.app.json` only compiles the `main.ts` import graph, so these files are not even in the build | **Dead — never read** | Removed in the flag-cleanup PR. No branches or tests referenced it |
| `environment.apiUrl` (`''`) | same files | **Nowhere.** All components use relative `/api/...` URLs | **Dead — never read** | Removed in the flag-cleanup PR |
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | `Program.cs:8` with fallback `?? "Data Source=ordermanager.db"` | Live. The fallback branch is unreachable while `appsettings.json` ships the key, but it protects a missing-config deployment; identical value either way | Keep |
| `Logging:LogLevel:*` | `appsettings.json` | Framework | Live | Keep |
| `AllowedHosts: "*"` | `appsettings.json` | Framework host-filtering middleware | Live (wildcard, not a toggle) | Keep |
| `angularCompilerOptions.enableI18nLegacyMessageIdFormat: false` | `client-app/tsconfig.json` | Angular compiler | Live compiler option | Keep |
| `package.json` `"test": "ng test"` | `client-app/package.json` | Nobody — no test target exists | Dead script, not a toggle | Left in place; noted in §9 |

No `IFeatureManager`, `IOptions<T>`, `IConfiguration["…"]` reads, `#if` directives, or boolean switches exist in the C# code. The monolith has **no runtime feature flags at all**; the only dead configuration is the Angular `environment` pair.

---

## 11. Risks and decisions to settle before moving code

| # | Risk / decision | Severity | Where it bites |
|---|---|---|---|
| 1 | Loss of transactional stock decrement with order insert (§8) | High | `OrderService.CreateOrderAsync` lines 49–67 |
| 2 | No decrement/reserve endpoint exists; `restock` is the only mutation | High | New Inventory API surface required |
| 3 | `Product.Inventory` navigation removal changes `/api/products` JSON and blanks the UI "Stock" column | Medium | `ProductService` 18/23/35, `ProductListComponent:15` |
| 4 | `InventoryItem.product` embedded `Product` — new service must either call Products or denormalise `name`/`sku` | Medium | `InventoryService` Includes, `InventoryListComponent` |
| 5 | FK `InventoryItems.ProductId → Products` with cascade delete cannot cross databases | Medium | Schema (§5) |
| 6 | Seed data is cross-module (`SeedData.cs:32–40`) and is also the only test fixture | Medium | `SeedData`, `OrderServiceTests.CreateContext` |
| 7 | Test project does not compile on `main`; baseline is effectively "0 tests run" until the `using Xunit;` fix lands | Medium | `OrderServiceTests.cs` |
| 8 | No lint/CI gate on either side; "hold the baseline" is a manual check today | Low | Repo root |
| 9 | Error contract: `500` on restock of unknown product; `POST /api/orders` returns `500` for stock errors | Low | `InventoryController.Restock`, `OrdersController.Create` |
| 10 | Frontend has no API base-URL mechanism (relative URLs; `environment.apiUrl` was dead) — routing to a second host needs a proxy or new config | Low | `client-app/src/app/modules/*` |
