# Rent Tracker V2 (C# / .NET 10 + PostgreSQL)

The same rent tracker as the Node version, rebuilt in C#. The React frontend is carried over, with amounts in dollars.

Documentation:

- [docs/USER_GUIDE.md](docs/USER_GUIDE.md): how to use every page
- [docs/TECHNOLOGY.md](docs/TECHNOLOGY.md): technologies, project layout, database and how to run it

- `src/RentTracker.Core`: pure rules (months, tenancy overlaps, payment states, room rent states) and the monthly, profit and expense reports. No database code, so they are easy to test.
- `src/RentTracker.Api`: ASP.NET Core minimal APIs, EF Core with Npgsql (PostgreSQL). Serves the React build from `../../frontend/dist` and listens on `http://127.0.0.1:3001`.
- `tests/RentTracker.Core.Tests`: xUnit tests for the rules and reports.

## Requirements

1. .NET 10 SDK (LTS): https://dotnet.microsoft.com/download/dotnet/10.0
2. Node.js 20 or later, for the frontend build.
3. PostgreSQL 16, either:
   - with Docker: `docker compose up -d` in this folder (listens on port 5433), or
   - a local install with a database called `rentdb_v2` (change `Port=5433` below to `Port=5432`).

To load sample data (5 rows per table): `Get-Content seed.sql | docker exec -i rentdb-pg psql -U postgres -d rentdb_v2`

## Run it

PowerShell, from this folder:

```powershell
# 1. Database connection (set your own password; do not commit it)
$env:ConnectionStrings__RentDb = "Host=localhost;Port=5433;Database=rentdb_v2;Username=postgres;Password=postgres"

# 2. Tests
dotnet test tests/RentTracker.Core.Tests

# 3. Frontend build
cd frontend
npm install
npm run build
cd ..

# 4. API (creates the tables on first run)
cd src/RentTracker.Api
dotnet run
```

Open http://127.0.0.1:3001.

For frontend development with hot reload, run `npm run dev` in `frontend` (port 5173). The API allows that origin by default.

## Status

Ported from the Express API:

- Renters, properties, rooms (room map data), tenancies (create, edit, end, change tenant, delete duplicate), payments, status, expenses, categories.
- Reports: monthly, profit and expenses, as JSON and CSV.
- Dashboard: `/dashboard`, `/due-soon`, `/heatmap`.
- Tenant statement: `/renters/{id}/statement`.

Not yet ported (the React pages that use them will show an error until they are):

- renter photo routes (`/renters/{id}/photo`)

The code was written without a compiler to hand. If `dotnet build` reports errors, send them back and they will be fixed.
