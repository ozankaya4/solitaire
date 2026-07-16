# Deploying Solitaire for free

This guide takes you from the local project to a live, public app — **no credit
card required** at any step. Follow the phases in order; each ends with a
**Checkpoint** you can verify. If you get stuck, tell me the **phase and step
number** (e.g. "stuck at Phase 2, step 5") and paste any error.

---

## How the pieces fit together

Three free services, one per layer:

| Layer | Service | Free tier | Why |
|-------|---------|-----------|-----|
| Database | **Neon** | Always-on Postgres, 0.5 GB | Cardless, doesn't sleep |
| Backend API | **Render** | Web service (Docker) | Cardless, runs .NET via the `Dockerfile` |
| Frontend | **Netlify** | Static hosting + proxy | Cardless, one-click from Git |

```
  Browser ──HTTPS──► Netlify (your SPA)
                        │  requests to /api/*  are proxied server-side
                        ▼
                     Render (Solitaire API)  ──► Neon (Postgres)
```

**The important trick:** the browser only ever talks to your Netlify URL. Netlify
proxies `/api/*` to Render behind the scenes (configured in `netlify.toml`). So
from the browser's point of view everything is one origin — which is why the
secure `SameSite=Strict` login cookie keeps working with **no CORS setup and no
code changes**. You do not deploy the frontend and backend as two separate
origins the browser sees.

**Cold starts:** Render's free service sleeps after ~15 min idle and takes
~30–60 s to wake. The first request after idle may be slow or time out once —
just retry. This is the main downside of "free" and is fine for a hobby project.

---

## Phase 0 — Put the code on GitHub

Render and Netlify both build from a GitHub repo, so this comes first.

1. Create a new **empty** repository on GitHub (github.com → New repository). Name
   it e.g. `solitaire`. Don't add a README/.gitignore (the project already has them).
2. In the project folder, push your existing `main` branch:
   ```bash
   git remote add origin https://github.com/<your-username>/solitaire.git
   git add .
   git commit -m "Auth + leaderboard + deployment config"
   git push -u origin main
   ```
   (If `origin` already exists, skip `git remote add`.)

**Checkpoint 0:** You can see your code on github.com, including `Dockerfile`,
`netlify.toml`, and the `backend/` and `frontend/` folders.

> Security note: never commit secrets. The database password lives only in
> Render's environment variables (Phase 2), never in the repo.

---

## Phase 1 — Database (Neon)

1. Go to **https://neon.tech** and sign up (GitHub login is easiest — no card).
2. Click **Create project**. Pick any name and the region closest to you.
   Postgres version default is fine.
3. After it's created, open **Dashboard → Connect** (or "Connection Details").
   You'll see a connection string. **Copy the "Connection string" for the
   pooled connection.** It looks like:
   ```
   postgresql://neondb_owner:XXXX@ep-cool-name-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require
   ```
4. **Convert it to the .NET (Npgsql) format** — the app needs key/value form, not
   the URL form. Take the pieces from the URL and build this single line:
   ```
   Host=ep-cool-name-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=XXXX;SSL Mode=Require;Trust Server Certificate=true
   ```
   - `Host` = the part after `@` and before `/`
   - `Database` = the part after the last `/` (before `?`), usually `neondb`
   - `Username` / `Password` = the part before `@` (split on `:`)

   Save this line somewhere temporary — it's your `ConnectionStrings__DefaultConnection`.

**Checkpoint 1:** You have a Neon connection string in the `Host=...;Database=...`
format. (Don't worry about creating tables — the app runs its migrations
automatically on first boot.)

---

## Phase 2 — Backend API (Render)

1. Go to **https://render.com** and sign up with GitHub (no card for the free tier).
2. **New → Web Service**, then connect your `solitaire` GitHub repo.
3. Configure:
   - **Language / Runtime:** Docker (Render auto-detects the `Dockerfile`).
   - **Root Directory / Build Context:** leave blank (repo root — the Dockerfile
     needs the whole repo).
   - **Dockerfile Path:** `./Dockerfile`
   - **Instance Type:** **Free**.
   - **Health Check Path:** `/health`
4. Open **Advanced → Environment Variables** and add these three:

   | Key | Value |
   |-----|-------|
   | `ConnectionStrings__DefaultConnection` | *(the Npgsql string from Checkpoint 1)* |
   | `Database__Provider` | `Postgres` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   (Render sets `PORT` for you — the app already reads it. Don't set it yourself.)
5. Click **Create Web Service**. The first build takes a few minutes (it compiles
   the .NET app in Docker). Watch the **Logs** tab.
6. When it's live, Render shows a URL like `https://solitaire-api-xxxx.onrender.com`.
   **Copy it.** Open `<that-url>/health` in your browser — you should see
   `{"status":"ok"}`.

**Checkpoint 2:** `https://<your-render-app>.onrender.com/health` returns
`{"status":"ok"}`, and the logs show the app started and applied migrations
(look for EF Core migration lines, no red errors).

> If the build fails on the database at startup, double-check the connection
> string format (Phase 1, step 4) — the `postgresql://` URL form will not work;
> it must be the `Host=...;` form.

---

## Phase 3 — Point the frontend proxy at your API

1. Open `netlify.toml` in the repo. Find the proxy block and replace the
   placeholder host with your Render URL from Checkpoint 2:
   ```toml
   [[redirects]]
     from = "/api/*"
     to = "https://solitaire-api-xxxx.onrender.com/api/:splat"
     status = 200
     force = true
   ```
   Keep the `/api/:splat` suffix exactly as-is.
2. Commit and push:
   ```bash
   git add netlify.toml
   git commit -m "Point API proxy at Render"
   git push
   ```

**Checkpoint 3:** `netlify.toml` on GitHub shows your real Render URL in the
`to = ` line.

---

## Phase 4 — Frontend (Netlify)

1. Go to **https://netlify.com** and sign up with GitHub (no card).
2. **Add new site → Import an existing project → GitHub**, pick your `solitaire` repo.
3. Netlify reads `netlify.toml` automatically, so the build settings should
   pre-fill:
   - **Base directory:** `frontend`
   - **Build command:** `npm run build`
   - **Publish directory:** `frontend/dist`
   Leave them as detected. No environment variables are needed (the app calls its
   own origin, so `VITE_API_URL` stays unset in production).
4. Click **Deploy**. After a minute or two you'll get a URL like
   `https://random-name-123.netlify.app`.

**Checkpoint 4:** Opening your `*.netlify.app` URL loads the Solitaire main menu
and you can start and play a game (guest mode works fully offline).

---

## Phase 5 — Verify the full stack (auth + leaderboard)

On your live `*.netlify.app` site:

1. Click **Sign in → Sign up**. Create an account (name = 3–32 letters/numbers/
   `. _ -`, a valid email, password ≥ 8 chars). It should log you in and close.
   - If the very first attempt hangs, Render is cold-starting — wait ~40 s and retry.
2. Play and **win a Klondike level** (levels 1–45 are the ranked ones). On the win
   screen you should see **"Ranked #N"**.
3. Open **Leaderboard** from the main menu — your name should appear with your level.
4. **Sign out**, refresh, and confirm you're signed out (guest state).

**Checkpoint 5:** You registered, won a level, and saw yourself on the
leaderboard — on the public URL. 🎉 You're deployed.

---

## Environment variable reference (Render)

| Variable | Required | Example / Notes |
|----------|----------|-----------------|
| `ConnectionStrings__DefaultConnection` | ✅ | `Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true` |
| `Database__Provider` | ✅ | `Postgres` |
| `ASPNETCORE_ENVIRONMENT` | ✅ | `Production` |
| `PORT` | ⛔ auto | Set by Render; the app reads it. Don't add it. |
| `Cors__AllowedOrigins__0` | optional | Only needed if you ever call the API cross-origin (the proxy avoids this). Set to your `https://...netlify.app` if so. |

---

## Troubleshooting

**Login "works" locally but not on the live site.** The proxy line in
`netlify.toml` is wrong or not deployed. Check Phase 3 — the `to =` must be your
exact Render URL ending in `/api/:splat`, and you must have pushed + let Netlify
redeploy.

**First request after a while is very slow / times out once.** Expected: Render
free spins down when idle. Retry; it wakes in ~30–60 s. (Optional: a free uptime
pinger like UptimeRobot hitting `/health` every 10 min keeps it warm.)

**Render build fails.** Open the build logs. If it's the embedded level file, the
build context must be the repo **root** (Phase 2, step 3 — leave Root Directory
blank). If it's NuGet/restore, just retry the deploy (transient).

**500 on register/login, logs mention the database.** Connection string format
(Phase 1, step 4). Must be `Host=...;` key/value form with `SSL Mode=Require`.

**Netlify build fails on `npm run build`.** Usually a Node version mismatch —
`netlify.toml` pins `NODE_VERSION = "20"`; make sure that block is present.

**Leaderboard submit silently does nothing after a win.** Only levels **1–45**
(Klondike) and any Spider level are rankable, and you must be **signed in** when
you win. A win claimed as a level whose deal doesn't match is rejected by the
anti-cheat by design.

---

## Costs & limits (all free tiers)

- **Neon:** 0.5 GB storage, always-on. Plenty for a leaderboard.
- **Render:** 512 MB RAM, sleeps after 15 min idle, 750 instance-hours/month.
- **Netlify:** 100 GB bandwidth/month, generous build minutes.

Nothing here bills you or asks for a card on the free tiers. If you later want no
cold starts, Render's paid instance (~$7/mo) stays always-on; everything else can
remain free.

---

## Updating the app later

Both services auto-deploy on `git push` to `main`:
- Push backend changes → Render rebuilds and redeploys.
- Push frontend changes → Netlify rebuilds and redeploys.

Database schema changes ship as EF Core migrations and apply automatically on the
next backend boot.
