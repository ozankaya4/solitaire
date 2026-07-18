# Deployment

> ⚠️ Never paste real passwords or connection strings into this file — it is
> committed to the repository. Use placeholders only.

## Architecture: one origin

Everything is served by **one Render web service** built from the repo-root
`Dockerfile`:

```
Browser ──HTTPS──► Render container
                     ├── /            the built SPA (frontend/dist → wwwroot)
                     ├── /api/*       the ASP.NET Core API
                     └── /health      health check
                            │
                            ▼
                       Neon (Postgres)
```

The Docker build compiles the frontend (Node stage) and the backend (.NET
stage), and the API serves the SPA from `wwwroot`. One origin means: no CORS in
production, no proxy, and the auth cookie is first-party with
`SameSite=Strict`.

> History: the SPA originally lived on Netlify with a `/api/*` proxy. It was
> migrated here because `*.netlify.app` is blocked/tampered with by some ISPs
> (notably in Türkiye), which made the site unreachable for its main audience.

## Services

| What | Where | Notes |
|------|-------|-------|
| App + API | Render (free, Docker) | Auto-deploys on push to `main` |
| Database | Neon (free Postgres) | Migrations apply automatically on boot |

## Render configuration

- **Build**: Docker, context = repo root, `Dockerfile Path = ./Dockerfile`
- **Health Check Path**: `/health`
- **Environment variables** (double underscores are mandatory — a single
  underscore is silently ignored by .NET and the app falls back to SQLite):

| Key | Value |
|-----|-------|
| `ConnectionStrings__DefaultConnection` | `Host=<neon-direct-host>;Database=neondb;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true` |
| `Database__Provider` | `Postgres` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Use Neon's **direct** connection host (no `-pooler`) — the pooler breaks EF
Core migrations.

## Cold starts (free tier)

Render free sleeps after ~15 min idle; the next request takes ~30–60 s. The
frontend retries API calls to ride this out, and the PWA serves the cached
shell to returning visitors — but the *first ever* visit during a cold start is
slow. Optional mitigation: a free uptime monitor (e.g. UptimeRobot) pinging
`https://<your-app>.onrender.com/health` every 5–10 minutes keeps it awake
(one always-on free service fits within Render's 750 instance-hours/month).

## Local development (unchanged)

- API: `dotnet run` in `backend/src/Solitaire.Api` (SQLite, port 5080)
- SPA: `npm run dev` in `frontend` (Vite, port 5173, talks to 5080 via
  `VITE_API_URL` in `.env.development`; CORS allows this origin in dev)
- The container-only SPA hosting is skipped locally (no `wwwroot`).

## Troubleshooting

- **Exit 139 / segfault on start**: the config file-watcher crashes in
  Render's sandbox; the Dockerfile disables it (`DOTNET_hostBuilder__reloadConfigOnChange=false`). Don't remove those ENV lines.
- **`/health` 200 but every API call 500s with `no such table` (SQLite)**: the
  env var names lost their double underscores → app silently fell back to
  SQLite. Fix the names, redeploy.
- **`Npgsql ... 42P01 relation does not exist`**: migrations ran through the
  Neon pooler. Wipe the schema (`DROP SCHEMA public CASCADE; CREATE SCHEMA
  public;`), switch to the direct host, redeploy.
- **`Cannot load library libgssapi_krb5.so.2` in logs**: harmless (Npgsql
  probing Kerberos); ignore.
- **Everyone logged out after a deploy**: should not happen anymore
  (DataProtection keys persist in the DB); if it does, check the
  `DataProtectionKeys` table exists.
