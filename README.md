# Solitaire

A web-based Solitaire game. This repository is a monorepo skeleton.

## Structure

```
.
├── backend/            ASP.NET Core (.NET 10) solution
│   ├── Solitaire.sln
│   ├── Directory.Build.props        # shared: net10.0, nullable, warnings-as-errors
│   ├── src/
│   │   ├── Solitaire.Api/           # Minimal API — HTTP boundary
│   │   └── Solitaire.Engine/        # pure game logic (no ASP.NET dependency)
│   └── tests/
│       └── Solitaire.Engine.Tests/  # xUnit tests for the engine
├── frontend/           React + Vite + TypeScript app (hand-written CSS)
├── shared/             cross-language JSON test vectors (engine parity)
├── .github/workflows/  CI (build + test backend, lint/type-check/build frontend)
├── .editorconfig       C# + TS style rules
├── .gitignore
└── LICENSE             MIT
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)

## Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Solitaire.Api   # serves GET /health
```

Nullable reference types and `TreatWarningsAsErrors` are enabled for all
projects via `Directory.Build.props`.

## Frontend

```bash
cd frontend
npm install
npm run dev          # start Vite dev server
npm run lint         # ESLint
npm run type-check   # tsc (strict, noUncheckedIndexedAccess)
npm run build        # type-check + production build
npm run format       # Prettier
```

Dependencies are deliberately minimal: **motion** for animation and
**i18next / react-i18next** for localization. No component/UI library. CSS is
hand-written.

## License

[MIT](LICENSE) © 2026 Ozan Kaya
