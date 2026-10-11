# Ayush360 – Deployment guide (Docker → Azure)

Handoff for the DevOps engineer. Everything below is already in the two repos; what's needed is the Azure
infrastructure and a CI/CD pipeline that builds the images and rolls them out.

- Backend repo `Ayush360_BE`: `Dockerfile` (3 targets), `deploy/docker-compose.yml`, `deploy/migrate.sh`
- Frontend repo `Ayush360_FE`: `Dockerfile`, `docker/nginx/*`

---

## 1. What runs

```
              Internet (https://hms.example.in)
                         │
                ┌────────▼─────────┐  external ingress, port 8080
                │  web  (Nginx)    │  React app + reverse proxy:
                │  ayush360-web    │    /api/auth/* → identity      /api/* → hms
                └───┬──────────┬───┘
       internal     │          │     internal
   ┌────────────────▼─┐    ┌───▼──────────────────┐
   │ identity (API)   │◄───┤ hms (API)            │  hms downloads identity's public keys
   │ ayush360-identity│jwks│ ayush360-hms         │  (/.well-known/jwks.json) to check tokens
   └────────┬─────────┘    └───┬──────────────────┘
            │                  │
         ┌──▼──────────────────▼──┐      ┌──────────────────────────┐
         │ Azure Database for     │◄─────┤ migrate (Container Apps  │ run once per release,
         │ PostgreSQL (database   │      │ Job) ayush360-migrator   │ before the APIs roll out
         │ "ayush360")            │      └──────────────────────────┘
         └────────────────────────┘
```

| Image | Built from | Port | Ingress | Notes |
|---|---|---|---|---|
| `ayush360-web` | FE `Dockerfile` | 8080 | **External** (custom domain + TLS) | Non-root Nginx. Only public entry point. Security headers (CSP, HSTS, X-Frame-Options...) set here. |
| `ayush360-identity` | BE `Dockerfile --target identity` | 8080 | Internal only | Login, refresh, tokens (RS256), users, roles, licences. Schema `core`. |
| `ayush360-hms` | BE `Dockerfile --target hms` | 8080 | Internal only | Front desk, doctor, queue. Schemas `hospital`, `registration`, `doctor`. |
| `ayush360-migrator` | BE `Dockerfile --target migrator` | – | – | One-off job: applies EF Core migrations (Identity first, then HMS) and exits 0. |

The browser only talks to the web container (one origin): no CORS, and the refresh-token cookie
(`ayush360_rt`, HttpOnly, Secure, SameSite=Strict, path `/api/auth`) stays first-party.

## 2. Build the images

```bash
# Backend (from the Ayush360_BE root)
docker build --platform linux/amd64 --target identity -t <acr>.azurecr.io/ayush360-identity:<tag> .
docker build --platform linux/amd64 --target hms      -t <acr>.azurecr.io/ayush360-hms:<tag> .
docker build --platform linux/amd64 --target migrator -t <acr>.azurecr.io/ayush360-migrator:<tag> .

# Frontend (from the Ayush360_FE root)
docker build --platform linux/amd64 -t <acr>.azurecr.io/ayush360-web:<tag> .
```

Use one tag per release for all four (e.g. the git commit SHA). Frontend build-time options (baked into the JS,
change = rebuild): `--build-arg VITE_IDLE_TIMEOUT_MINUTES=15` (auto sign-out after inactivity; 0 = off).

## 3. Azure resources (suggested)

| Resource | Test / UAT | Production |
|---|---|---|
| Resource group | `rg-ayush360-test` | `rg-ayush360-prod` |
| Container Registry | Basic | Standard (shared is fine) |
| Container Apps environment | Consumption, **VNet-integrated** | Same, workload profiles if needed |
| PostgreSQL Flexible Server | **v18** (17 is fine if 18 isn't offered in the region), Burstable B2s, 32 GB | General Purpose D2ds_v5+, zone-redundant HA, 128 GB+ |
| Key Vault | Standard, RBAC, soft-delete + **purge protection** | Same |
| Log Analytics workspace | yes (Container Apps logs) | yes, 90+ days retention |
| Managed identity | User-assigned, shared by the 3 apps + job: `AcrPull` on ACR, `Key Vault Secrets User` on Key Vault | Same |

Database networking: **private access** (VNet), no public endpoint. Region: Central India / South India
(patient data should stay in India).

### Database

```sql
CREATE DATABASE ayush360;
CREATE ROLE ayush360_app LOGIN PASSWORD '<strong password>';
GRANT ALL ON DATABASE ayush360 TO ayush360_app;
-- PostgreSQL 15+: let the role create its schemas
ALTER DATABASE ayush360 OWNER TO ayush360_app;
```

The migrator creates the schemas and tables. Today the APIs and the migrator use the same login; splitting into an
owner login (migrations) and a read/write-only login (APIs) is a later hardening step.
Backups: Azure PITR, 7 days test / 35 days prod.

## 4. Secrets (Key Vault) → environment variables

Container Apps: add each as a **secret referencing Key Vault**, then map it to the env var. `__` in an env var name
= `:` in .NET configuration.

| Key Vault secret | Env var | Used by | How to create |
|---|---|---|---|
| `db-connection` | `ConnectionStrings__Default` | identity, hms, migrator | `Host=<server>.postgres.database.azure.com;Port=5432;Database=ayush360;Username=ayush360_app;Password=<pw>;Ssl Mode=Require` |
| `jwt-signing-key` | `Jwt__SigningKeyPem` | identity | `openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out key.pem` (whole PEM text) |
| `aadhaar-encryption-key` | `Aadhaar__EncryptionKey` | hms | `openssl rand -base64 32` |
| `aadhaar-hash-key` | `Aadhaar__HashKey` | hms | `openssl rand -base64 32` (a different value) |
| `demo-password` *(test only)* | `DemoData__Password` | identity | any password for the demo users |

> **Aadhaar keys must never be lost or changed.** Stored Aadhaar numbers are encrypted with them; without the
> original keys they can't be read or searched. Keep purge protection on and back the two secrets up offline.
> The JWT signing key can be rotated (see backend README: "Key rotation").

The APIs refuse to start in Production if the signing key or the Aadhaar keys are missing (fail fast, by design).

## 5. Configuration per app

**identity** (`ayush360-identity`)

| Env var | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | secret `db-connection` |
| `Jwt__SigningKeyPem` | secret `jwt-signing-key` |
| `Jwt__SessionHours` | `12` (longest sign-in; renewals don't extend it) |
| `Jwt__AccessTokenMinutes` | `30` (default) |
| `ForwardedHeaders__ForwardLimit` | `2` (see §8) |
| `DemoData__Enabled` + `DemoData__Password` | **test only**: `true` + secret, creates the two demo hospitals and their users. Never in production. |

**hms** (`ayush360-hms`)

| Env var | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | secret `db-connection` |
| `Jwt__JwksUrl` | `https://ayush360-identity.internal.<environment-default-domain>/.well-known/jwks.json` |
| `Aadhaar__EncryptionKey` / `Aadhaar__HashKey` | secrets |
| `ForwardedHeaders__ForwardLimit` | `2` |
| `DemoData__Enabled` | **test only**: `true` (departments, doctor profiles, sample patients for the demo hospitals) |

**web** (`ayush360-web`)

| Env var | Value |
|---|---|
| `IDENTITY_API_URL` | `https://ayush360-identity.internal.<environment-default-domain>` |
| `HMS_API_URL` | `https://ayush360-hms.internal.<environment-default-domain>` |

**migrate job** (`ayush360-migrator`): `ConnectionStrings__Default` = secret `db-connection`. Trigger: manual
(called by the pipeline). Replica timeout 600 s, retry limit 0.

All .NET images already set: port 8080, non-root user, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `TZ=Asia/Kolkata`.

## 6. Probes, sizing, scaling

| App | Readiness | Liveness | CPU / memory | Replicas |
|---|---|---|---|---|
| identity | HTTP `GET /health` :8080 (checks PostgreSQL) | TCP 8080 | 0.5 / 1 Gi | min 1, max 3 |
| hms | HTTP `GET /health` :8080 | TCP 8080 | 0.5 / 1 Gi | min 1, max 5 |
| web | HTTP `GET /healthz` :8080 | HTTP `GET /healthz` | 0.25 / 0.5 Gi | min 1, max 3 |

Keep **min 1** on all three (hospital desks need instant response; no cold starts). Scale rule: HTTP concurrency
~50. `/health` is not reachable from the internet (Nginx only forwards `/api/*`).

## 7. Release pipeline (order matters)

1. Build and push the 4 images with the same tag.
2. Update the migrate job's image to the new tag, **start it, wait for success** (exit code 0). Stop the release if it fails.
3. Roll out `identity`, then `hms`, then `web` with the new tag (single-revision mode).
4. Smoke test: open the site, sign in, open Registration and Patient queue.

Migrations are additive so far (new tables/columns), so the old revision keeps working while the new one starts.

## 8. Things to check on the first deployment

- **Client IP for rate limiting.** Login is limited per IP (60/min; refresh 300/min). Nginx passes the browser's
  IP in `X-Forwarded-For`; Container Apps' internal ingress adds one more hop, hence `ForwardLimit=2`. Check an API
  log line after a login: it should show the real client IP, not a `10.x`/`100.x` address. If it doesn't, adjust
  the number (Docker Compose uses 1).
- **Custom domain + certificate** on the web app only (managed certificate is fine).
- `Ssl Mode=Require` in the connection string.
- Logs: all apps log to the console → Container Apps → Log Analytics. Each line has `corr=<correlation id>
  tenant=<hospital> user=<id>`; the same correlation id is returned to the browser as `X-Correlation-ID`.
  Application Insights traces are not wired yet (OpenTelemetry is in place; adding the Azure Monitor exporter is a
  small code change).
- Production has **no demo data** and no tenant-onboarding screen yet: the first hospital and its admin user are
  created with the onboarding service (to be exposed in the Admin module). For UAT use `DemoData__Enabled=true`.

## 9. Try it locally first (same images)

```powershell
# repos side by side: C:\Ayush360\Backend and C:\Ayush360\Frontend
cd C:\Ayush360\Backend\deploy
copy .env.example .env        # set DB_PASSWORD
docker compose up --build
# http://localhost:8080  ->  demo users, password demo123 (e.g. DN10110 receptionist, SR10310 doctor)
docker compose down           # keeps data; add -v to wipe the database and keys
```

Compose runs the APIs in Development mode (demo data, generated keys stored in the `identity-keys` / `hms-keys`
volumes). PostgreSQL is exposed on `localhost:5433` for pgAdmin.

## 10. Still open before go-live (application side)

- PostgreSQL row-level security per hospital (defence in depth; tenant filtering is already done in the app).
- Audit log of who viewed / changed patient records.
- Admin module: password policy, first-login password change, reset / unlock, hospital onboarding UI.
- Application Insights exporter; alerts on 5xx rate and failed logins.
