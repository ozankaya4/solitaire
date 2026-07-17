# Builds the Solitaire API as a container (the SPA is deployed separately and
# proxied to /api — see DEPLOYMENT.md). Build context must be the repo root
# because the API embeds the curated level ladder from frontend/src/levels.
#
#   docker build -t solitaire-api .
#   docker run -p 8080:8080 -e PORT=8080 solitaire-api

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) — copy just the projects' sources.
COPY backend/ ./backend/
# Single source of truth for the curated Klondike level->seed table (EmbeddedResource).
COPY frontend/src/levels/klondike.levels.json ./frontend/src/levels/klondike.levels.json

RUN dotnet publish backend/src/Solitaire.Api/Solitaire.Api.csproj -c Release -o /app

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
# WebApplication.CreateBuilder() watches appsettings.json for changes via inotify.
# In restricted container sandboxes (e.g. Render) that watcher segfaults the
# process on startup (exit 139) before any app code runs. Config is baked into the
# image and never changes at runtime, so turn the watcher off. Belt-and-braces:
# force polling for any other file watcher rather than inotify.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
# The platform (Render/Railway/Fly) injects PORT; Program.cs binds to it.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Solitaire.Api.dll"]
