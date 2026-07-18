# Builds the whole app as ONE container: the API serves the built SPA from
# wwwroot, so a single origin handles everything (no proxy, no CORS, cookies
# trivially first-party). Build context must be the repo root.
#
#   docker build -t solitaire .
#   docker run -p 8080:8080 -e PORT=8080 solitaire

# ---- frontend build ----
FROM node:22-alpine AS spa
WORKDIR /repo/frontend
# Install deps first for layer caching.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
# The build type-checks the test files too, which import the shared
# cross-language vectors from ../shared — so it must be present.
COPY shared/ /repo/shared/
COPY frontend/ ./
RUN npm run build

# ---- backend build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ ./backend/
# Single source of truth for the curated Klondike level->seed table (EmbeddedResource).
COPY frontend/src/levels/klondike.levels.json ./frontend/src/levels/klondike.levels.json
RUN dotnet publish backend/src/Solitaire.Api/Solitaire.Api.csproj -c Release -o /app

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
# The SPA lands in wwwroot; Program.cs serves it when index.html is present.
COPY --from=spa /repo/frontend/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production
# WebApplication.CreateBuilder() watches appsettings.json via inotify, which
# segfaults (exit 139) in restricted container sandboxes like Render's. Config
# never changes at runtime, so disable the watcher (polling as belt-and-braces).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
# The platform (Render/Railway/Fly) injects PORT; Program.cs binds to it.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Solitaire.Api.dll"]
