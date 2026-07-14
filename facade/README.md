# Migration Facade (reverse proxy)

The Angular SPA (`client-app/`) calls **relative** API paths (`/api/orders`,
`/api/products`, `/api/inventory`, `/api/customers`) with an empty
`environment.apiUrl`, so it must talk to a **single origin**. This nginx reverse
proxy is that single origin. It lets us cut each `/api` route group over from the
legacy .NET backend to the new Java backend **independently**, without changing
any frontend code — the essence of the strangler-fig migration.

```
                         ┌────────────────────────┐
   browser  ── :8080 ──▶ │   facade (nginx)        │
   (relative /api/*)     │  per-route-group switch │
                         └───────────┬─────┬───────┘
              /api/<group> to .NET ──┘     └── to Java, once a group is cut over
                         .NET :5001              Java :5000
```

## Backends (start these first)
| Backend | Origin | Command |
|---------|--------|---------|
| .NET (legacy) | `http://127.0.0.1:5001` | `ASPNETCORE_URLS=http://localhost:5001 dotnet run --project src/OrderManager.Api/OrderManager.Api.csproj` |
| Java (target) | `http://127.0.0.1:5000` | `cd server-java && ./mvnw spring-boot:run` (defaults to port 5000) |

## Run the facade
```bash
./facade/start.sh        # foreground on :8080 (Ctrl-C to stop)
./facade/stop.sh         # stop a running instance
```
The SPA is reached at `http://localhost:8080`.

## Cut a route group over to Java
Each `/api` group maps to one `proxy_pass` line in `nginx.conf`, tagged with a
`# ROUTE:<group>` marker. Flip exactly one group:
```bash
./facade/flip-route.sh customers java --reload   # Phase 1: Customers -> Java
./facade/flip-route.sh products  java --reload   # Phase 2
./facade/flip-route.sh inventory java --reload   # Phase 3
./facade/flip-route.sh orders    java --reload   # Phase 4
./facade/flip-route.sh customers dotnet          # roll a group back to .NET
```
`groups: customers | products | inventory | orders | spa`. Without `--reload` it
only edits the config; reload/restart nginx to apply. Editing the tagged
`proxy_pass` line by hand and reloading works too.

## Angular dev server
For `ng serve`, point `client-app/proxy.conf.json` at the facade
(`http://localhost:8080`) instead of a single backend, so local dev exercises the
same single-origin routing. The SPA source is **not** modified.

## Retiring .NET (Phase 5)
Once all four groups point at Java, flip `spa` to Java too (Spring Boot serves the
built Angular assets from `src/main/resources/static` with an index.html
fallback). The facade can then be removed, or kept as the single ingress.

Runtime state (`temp/`, `logs/`, `*.pid`) is gitignored.
