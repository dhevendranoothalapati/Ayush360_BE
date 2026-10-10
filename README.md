# Ayush360 Backend

ASP.NET Core (.NET 10) + PostgreSQL 18 backend for the Ayush360 multi-hospital HMS.
Frontend: `C:\Ayush360\Frontend` (React). Architecture: modular monolith, one PostgreSQL database,
one schema per module, tenant (hospital) always taken from the token, never from the client.

## Projects

| Project | What it is |
|---|---|
| `src/Ayush360.Platform` | Shared library every API uses: observability (Serilog + OpenTelemetry, `X-Correlation-ID`), JWT validation, `[RequirePermission]` / `[RequireModule]`, `ICurrentUser` (user + tenant), one error format, CORS, `/health` |
| `src/Ayush360.Identity.Api` | Core API: login, tokens, users, roles, permissions, tenants, module licences (schema `core`) |
| `src/Ayush360.Hms.Api` | Hospital modules: Registration / Front Desk first (schema `registration`), then Billing, Doctor, ... |

## One-time setup (secrets never go in Git)

```powershell
cd C:\Ayush360\Backend
# Same database for both APIs
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=ayush360;Username=ayush360_app;Password=<password>" --project src\Ayush360.Identity.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=ayush360;Username=ayush360_app;Password=<password>" --project src\Ayush360.Hms.Api
# Same JWT signing key for both APIs (Identity signs, HMS validates)
$key = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
dotnet user-secrets set "Jwt:SigningKey" $key --project src\Ayush360.Identity.Api
dotnet user-secrets set "Jwt:SigningKey" $key --project src\Ayush360.Hms.Api
```

## Run

```
dotnet build
dotnet run --project src\Ayush360.Identity.Api --launch-profile https   # https://localhost:7263
dotnet run --project src\Ayush360.Hms.Api --launch-profile https        # https://localhost:7109  (second terminal)
```

Test with the `.http` files in each API folder (VS Code "REST Client" extension).

## Rules

1. **Tenant from the token only.** Use `ICurrentUser.RequireTenantId()`. Never accept `tenantId` from the request.
2. **Secure by default.** Every endpoint needs a valid token unless marked `[AllowAnonymous]`.
   Add `[RequireModule("REGISTRATION")]` and `[RequirePermission("PATIENT_EDIT")]` to module endpoints.
3. **Errors:** throw `AppValidationException` / `NotFoundException` / `ForbiddenException` / `ConflictException`.
   Every API returns `{ message, errors?, correlationId }`, which the React app already understands.
4. **Logs:** use `ILogger<T>` with message templates (`"Created {VisitNumber}"`), never string concatenation
   and never log patient details, passwords or tokens.
5. **Modules don't touch each other's tables.** Talk through APIs/contracts/events.

## Status

- [x] Solution, Platform library, Identity + HMS API shells, health checks
- [x] Dev-only token endpoint (`POST /api/dev/token`) to test auth before the users tables exist
- [ ] `core` tables + real login / refresh / `/api/auth/me`
- [ ] `registration` tables + the 13 Front Desk endpoints the React app uses
- [ ] Row-level security per tenant, audit log, Application Insights exporter
