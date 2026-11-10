# Rent Tracker — Technology

How Rent Tracker V2 is built, what each part does, and how to run it. For how to use each page, see [USER_GUIDE.md](USER_GUIDE.md).

## At a glance

```
Browser (React app)
   │  HTTP + JSON, same address (http://127.0.0.1:3001)
   ▼
ASP.NET Core API  ── serves the built React files too
   │  Entity Framework Core + Npgsql
   ▼
PostgreSQL 16 (Docker container "rentdb-pg", port 5433)
```

| Layer | Technology | Version |
|---|---|---|
| Frontend | React | 19 |
| Frontend build | Vite + @vitejs/plugin-react | Vite 7, plugin 5 |
| Backend | C# on .NET, ASP.NET Core minimal APIs | .NET 10 (SDK 10.0.403) |
| Data access | Entity Framework Core with Npgsql | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 |
| Database | PostgreSQL | 16 (Docker image `postgres:16`) |
| Tests | xUnit | 2.9.2 |
| Tooling | Node.js / npm (frontend build), Docker (database) | Node 24 on this machine (20+ works) |

## Project layout

```
rent-tracker V2/
├─ frontend/                  React app
│  ├─ src/App.jsx             All pages and components
│  ├─ src/App.css, index.css  Styles
│  └─ dist/                   Built files served by the API (npm run build)
├─ src/
│  ├─ RentTracker.Api/        Web server and database code
│  │  ├─ Program.cs           Startup: database, JSON, CORS, serves frontend/dist
│  │  ├─ ApiRoutes.cs         Renters, properties, rooms, tenancies, payments,
│  │  │                       status, expenses, reports
│  │  ├─ DashboardRoutes.cs   /dashboard, /due-soon, /heatmap
│  │  ├─ StatementRoutes.cs   /renters/{id}/statement
│  │  └─ Data/                Entities.cs (tables), RentDb.cs (EF Core model)
│  └─ RentTracker.Core/       Business rules with no database code
│     ├─ Rules/               Months, tenancy overlaps, payment states, room colours, money rounding
│     ├─ Reports/             Monthly, profit and expense report builders
│     └─ Csv.cs               CSV export helper
├─ tests/RentTracker.Core.Tests/   xUnit tests for the rules and reports
├─ docs/                      This documentation
├─ seed.sql                   Sample data (5 rows per table)
├─ docker-compose.yml         PostgreSQL in Docker (port 5433)
└─ .claude/                   launch.json + run-api.ps1 (start the app from Claude)
```

## Frontend

- **React 19** single-page app in one file, `frontend/src/App.jsx`. Pages switch by the address hash (`#dashboard`, `#payment`, `#statement` and so on), so no router library is needed.
- Charts (occupancy rings, heatmap) are drawn by hand with SVG and CSS. There are no chart libraries.
- The app talks to the API with `fetch` and JSON.
- **Vite** builds it into `frontend/dist`, which the API serves. During development, `npm run dev` runs Vite on port 5173 with hot reload, and the API allows that address (CORS).
- Money shows as dollars, with cents only when needed (`$1,200`, `$610.50`).

## Backend

- **ASP.NET Core minimal APIs** (.NET 10). Each endpoint is a small function in `ApiRoutes.cs`, `DashboardRoutes.cs` or `StatementRoutes.cs`.
- JSON uses camelCase names. The routes and JSON shapes match the original Node/Express version, so the React app works unchanged.
- Business rules live in **RentTracker.Core**, separate from the web and database code, so they can be tested on their own.
- The server listens on `http://127.0.0.1:3001` (set in `appsettings.json`). It needs the connection string in the environment variable `ConnectionStrings__RentDb`.

### API endpoints

| Area | Endpoints |
|---|---|
| Tenants | `GET/POST /renters`, `PATCH/DELETE /renters/{id}`, `GET /renters/{id}/statement` |
| Properties | `GET/POST /properties`, `DELETE /properties/{id}` |
| Rooms | `GET/POST /rooms`, `PATCH/DELETE /rooms/{id}` |
| Tenancies | `GET/POST /tenancies`, `PATCH/DELETE /tenancies/{id}`, `PATCH /tenancies/{id}/end`, `POST /tenancies/{id}/change-tenant` |
| Payments | `GET/POST /payments`, `PATCH/DELETE /payments/{id}`, `GET /status` |
| Expenses | `GET/POST /expenses`, `DELETE /expenses/{id}`, `GET /categories` |
| Dashboard | `GET /dashboard`, `GET /due-soon`, `GET /heatmap` |
| Reports | `GET /reports/monthly`, `/reports/profit`, `/reports/expenses`, plus `.csv` versions of each |

## Database

**PostgreSQL 16**, accessed through **Entity Framework Core** with the **Npgsql** provider. The tables are created automatically the first time the API starts (`EnsureCreated`). Use EF migrations instead for a real deployment.

| Table | Holds | Key columns |
|---|---|---|
| `Properties` | Buildings / addresses | Name, Address |
| `Rooms` | Rooms in a property | PropertyId, Name (unique per property), Status (AVAILABLE / REPAIR / OFF_MARKET), AskingRent |
| `Renters` | Tenants | Name, Dob, Status (CURRENT / MOVED_OUT), CreatedAt |
| `Tenancies` | Who lives in which room, and when | RoomId, RenterId, StartDate, EndDate (empty = still living there), MonthlyRent, DueDay |
| `Payments` | Rent received | RenterId, Month (YYYY-MM it pays for), Amount, PaidDate |
| `Expenses` | Property costs | PropertyId, Date, Category, Amount, Note |

Money columns are `numeric(10,2)` and dates are `date`. Deleting a property, room or tenant that other records still point to is blocked, so history isn't lost by accident.

## Running the app

Requirements: .NET 10 SDK, Node.js 20+, Docker Desktop.

1. **Database.** This machine's container `rentdb-pg` runs on port **5433**, because a Windows PostgreSQL install already uses 5432. On a new machine, `docker compose up -d` starts PostgreSQL on 5433.
2. **Connection string** (PowerShell):
   ```powershell
   $env:ConnectionStrings__RentDb = "Host=localhost;Port=5433;Database=rentdb_v2;Username=postgres;Password=<your password>"
   ```
3. **Build the frontend:**
   ```powershell
   cd frontend; npm install; npm run build; cd ..
   ```
4. **Start the API:**
   ```powershell
   cd src/RentTracker.Api; dotnet run
   ```
5. Open **http://127.0.0.1:3001**.

`.claude/run-api.ps1` does steps 2 and 4 in one go, reading the password from the Docker container so it isn't stored in a file.

**Sample data:** `Get-Content seed.sql | docker exec -i rentdb-pg psql -U postgres -d rentdb_v2` loads 5 properties, rooms, tenants, tenancies, payments and expenses. It refuses to run twice.

**Tests:** `dotnet test tests/RentTracker.Core.Tests`

## History

V2 is a port of the original Node.js version (`../rent-tracker`: Express, Prisma and PostgreSQL) to C# / .NET. The React frontend was carried over, with the currency changed from rupiah (Rp) to dollars ($). Everything is ported except tenant ID photos.
