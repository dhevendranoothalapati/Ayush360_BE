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
```

### Access tokens (RS256)

- The **Identity API** signs tokens with an RSA **private** key. In Development it is created automatically on
  first run at `%LOCALAPPDATA%\Ayush360\identity-dev-signing-key.pem` (outside the repo). On Azure it comes from
  Key Vault secret `Jwt--SigningKeyPem`.
- Other APIs (HMS, ...) only get the **public** key, from `GET /.well-known/jwks.json` on the Identity API
  (`Jwt:JwksUrl`; Development: `http://localhost:5077/.well-known/jwks.json`). They can check tokens but never create them.
- Key rotation: put the new private key in `Jwt:SigningKeyPem`, move the old key's PUBLIC key into
  `Jwt:PreviousPublicKeysPem` for one token lifetime (30 min), then remove it. Module APIs pick up new keys automatically.

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
- [ ] `core` tables + real login / refresh / `/api/auth/me`
- [ ] `registration` tables + the 13 Front Desk endpoints the React app uses
- [ ] Row-level security per tenant, audit log, Application Insights exporter
