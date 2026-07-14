# OrderManager Monolith

A .NET 8 + Angular 17 monolith application demonstrating tightly coupled modules sharing a single database. This is the **"before"** state for monolith-to-microservices decomposition demos.

## Architecture

The application contains four tightly coupled modules:

| Module | Description |
|--------|-------------|
| **Orders** | Order creation, status tracking, fulfillment |
| **Products** | Product catalog, pricing, categories |
| **Customers** | Customer profiles, addresses, preferences |
| **Inventory** | Stock levels, warehouse locations, reorder |

All modules share a single SQLite database and are deployed as one unit.

## Tech Stack

- **Backend**: .NET 8, C#, Entity Framework Core, SQLite
- **Frontend**: Angular 17, TypeScript
- **API**: RESTful with Swagger/OpenAPI

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- Angular CLI (`npm install -g @angular/cli`)

### Run the application

```bash
# Restore .NET dependencies
dotnet restore src/OrderManager.Api/OrderManager.Api.csproj

# Install Angular dependencies
cd client-app && npm install && cd ..

# Run the API (serves Angular app too)
dotnet run --project src/OrderManager.Api/OrderManager.Api.csproj
```

The application will be available at `https://localhost:5001`.

## Java Spring Boot Backend

A Java Spring Boot backend is available at `server-java/` as part of an ongoing migration from .NET to Java. It mirrors the same API contract as the .NET backend. See [`MIGRATION_PLAN.md`](MIGRATION_PLAN.md) for the full migration journey and wave definitions.

### Run the Java backend

```bash
cd server-java
./mvnw spring-boot:run
```

The Java backend will be available at `http://localhost:5000`.

### Migration facade (single origin) & parity gate

The migration follows the **strangler-fig** pattern: a reverse-proxy facade
(`facade/`) is the single origin the Angular SPA talks to, and each `/api` route
group is cut over from .NET to Java independently by flipping one proxy rule.
See [`facade/README.md`](facade/README.md).

```bash
# terminal 1: legacy .NET on :5001
ASPNETCORE_URLS=http://localhost:5001 dotnet run --project src/OrderManager.Api/OrderManager.Api.csproj
# terminal 2: Java on :5000
cd server-java && ./mvnw spring-boot:run
# terminal 3: facade on :8080 (the SPA origin)
./facade/start.sh
```

A contract/parity harness (`parity/`) is the acceptance gate for every cutover —
it verifies the Java responses match the .NET responses (status + JSON shape).
See [`parity/README.md`](parity/README.md) and the end-to-end runner:

```bash
./parity/run_parity.sh
```

The per-module cutover process is documented in
[`MIGRATION_WORKFLOW.md`](MIGRATION_WORKFLOW.md).

## Decomposition Targets

This monolith is designed to be decomposed into microservices that conform to the
[platform-engineering-shared-services](https://github.com/Cognition-Partner-Workshops/platform-engineering-shared-services) standard.

See the companion IaC repo: [app_dotnet-angular-monolith-iac](https://github.com/Cognition-Partner-Workshops/app_dotnet-angular-monolith-iac)

## License

MIT
