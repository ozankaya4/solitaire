# Solitaire backend

ASP.NET Core (.NET 10) Minimal API: EF Core data layer, ASP.NET Core Identity
(cookie auth), and the security baseline.

## Projects

- **Solitaire.Engine** — pure game logic (also used to validate migrated guest saves).
- **Solitaire.Api** — HTTP boundary: data layer, auth, security.
- **Solitaire.Engine.Tests**, **Solitaire.Api.Tests** — unit + integration tests.

## Database providers

Selected by configuration, defaulting by environment:

| Environment | Default provider | Schema applied at startup |
| ----------- | ---------------- | ------------------------- |
| Development / Testing | **SQLite** | `EnsureCreated()` (from the model) |
| Production | **PostgreSQL** (Npgsql) | `Migrate()` (EF migrations) |

Override with `Database:Provider` = `Sqlite` \| `Postgres`. **Migrations are
generated for the production provider (Npgsql)** via a design-time factory
(`AppDbContextFactory`), so they always emit Postgres-compatible DDL regardless of
the runtime provider. Primary keys are GUID/string (no DB sequences) to stay
portable.

```bash
# Regenerate / add a migration (targets Postgres):
dotnet ef migrations add <Name> --project src/Solitaire.Api

# Preview the exact Postgres DDL without a database:
dotnet ef migrations script --idempotent --project src/Solitaire.Api
```

### Testing the production (Postgres) provider locally

```bash
docker compose up -d db      # starts Postgres 17 (see docker-compose.yml)

# PowerShell — point the API at Postgres and run it:
$env:Database__Provider = "Postgres"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=solitaire;Username=solitaire;Password=solitaire"
dotnet run --project src/Solitaire.Api   # applies migrations on startup
```

## Environment variables

Config keys map to env vars with `__` replacing `:` (ASP.NET Core convention).

| Variable | Purpose | Example |
| -------- | ------- | ------- |
| `ASPNETCORE_ENVIRONMENT` | `Development` \| `Production` \| `Testing`. Drives provider default, HSTS, HTTPS redirection, cookie `Secure`. | `Production` |
| `Database__Provider` | Force the DB provider. | `Postgres` |
| `ConnectionStrings__DefaultConnection` | DB connection string. Required for Postgres. | `Host=…;Database=…;Username=…;Password=…` |
| `Cors__AllowedOrigins__0`, `…__1`, … | Allowed frontend origin(s) for CORS (credentials mode). | `https://app.example.com` |
| `ASPNETCORE_URLS` | Bind addresses. | `http://+:8080` |
| `ASPNETCORE_HTTPS_PORT` | Target port for HTTPS redirection behind a proxy. | `443` |

## Secrets

- **Locally:** [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
  (id `solitaire-api`) — never commit secrets:
  ```bash
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;…" --project src/Solitaire.Api
  ```
  The dev SQLite file needs no secret. The `docker-compose.yml` credentials are
  local-dev only.
- **In production:** supply `ConnectionStrings__DefaultConnection` (and any others)
  as **environment variables / a secret store** injected by the platform. Nothing
  sensitive lives in `appsettings*.json`.
- **Data Protection keys** (used to encrypt the auth cookie): in production persist
  the key ring to a durable, shared location (mounted volume / Redis / key vault)
  so cookies survive restarts and scale-out. The default (per-instance ephemeral
  keys) is fine for a single dev instance only.

## Session strategy — cookie auth (and why)

Auth uses ASP.NET Core Identity's **application cookie**, configured
**`HttpOnly` + `Secure` + `SameSite=Strict`**. Rationale for a same-site PWA:

- The cookie is invisible to JavaScript (`HttpOnly`) → not exfiltratable via XSS,
  unlike a JWT stored in `localStorage`/`sessionStorage`.
- `SameSite=Strict` means the browser never attaches it to cross-site requests →
  the primary CSRF vector is closed at the browser.
- We didn't choose JWTs: storing them in JS-readable storage is XSS-exposed, and
  storing them in a cookie reintroduces the same CSRF considerations as cookie
  auth without the ergonomics. Cookies + Identity also give rotation, revocation
  (security stamp), and sliding expiration for free.

**CSRF:** mitigated by `SameSite=Strict` + CORS locked to the known origin + the
API accepting only `application/json` (cross-site HTML forms cannot send that
content type). Anti-forgery tokens (double-submit cookie) are additionally wired
(`GET /api/auth/csrf`) and enforced on authenticated state-changing endpoints
(e.g. `POST /api/auth/logout`). Login/register are exempt (pre-session; login CSRF
is not meaningful and is covered by `SameSite=Strict`).

## Endpoints

- `POST /api/auth/register` — `{ username, email, password, guestData? }`. Creates
  the account, optionally migrates guest data (once), signs in.
- `POST /api/auth/login` — `{ usernameOrEmail, password, rememberMe? }`.
- `POST /api/auth/logout` — authenticated + anti-forgery.
- `GET /api/auth/me` — current user (401 if unauthenticated).
- `GET /api/auth/csrf` — issues an anti-forgery token.
- `GET /health`.
