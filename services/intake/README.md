# Intake Service

ASP.NET Core .NET 10 Web API — handles customer onboarding profile creation and admin intake workflows.

## Running locally

```
dotnet run --launch-profile http
```

Health check: `GET http://localhost:5168/health`

## Required environment variables

Supply these via `appsettings.Development.json` (local), `docker-compose.yml` (Docker), or Azure App Settings (production).  
**Never commit real values to the repository.**

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | JWT signing key — must match the Identity service exactly |
| `TaxIdHashPepper` | Pepper for TaxId hashing — must match the Identity service exactly |
| `Cors__AllowedOrigins__0` | Allowed frontend origin, e.g. `http://localhost:4200` |
| `Identity__BaseUrl` | Base URL of the Identity service, e.g. `http://localhost:5290` |
| `Internal__ApiKey` | Shared secret sent in `X-Internal-Key` header to Identity |
| `Risk__BaseUrl` | Base URL of the Risk service, e.g. `http://localhost:5291` |
| `Review__BaseUrl` | Base URL of the Review service, e.g. `http://localhost:5292` |

## API endpoints (implemented incrementally)

| Method | Path | Description |
|---|---|---|
| GET | `/health` | Health check |
| POST | `/api/users/signup` | Customer self-registration (Step 4) |
| GET | `/api/admins/inputForm/customers/{id}/exists` | Customer exists check (Step 5) |
| POST | `/api/admins/inputForm` | Admin input form — create customer and open ticket (Step 5) |
| GET | `/api/admins/pending-customers` | List self-signup customers without a ticket (Step 5) |
| GET | `/api/admins/pending-customers/{id}` | Get pending customer profile (Step 5) |
| POST | `/api/admins/pending-customers/{id}/submit` | Open ticket for pending customer (Step 5) |

## Notes

- This service uses the **shared SQL Server database** as a temporary arrangement during migration.
  The `Customers` table is owned jointly with the Identity service for now.
- The two admin POST endpoints (`/inputForm` and `/pending-customers/{id}/submit`) currently
  return 501. They validate inputs fully but do not write to the database. Full implementation
  requires calling Risk then Review — both services are now available.
- Risk screening (`POST /internal/analyze`) is called with the admin's forwarded Bearer JWT —
  no separate internal key is used for Risk.
- **One-time database fix required when using the shared `VerificationHubDB`:**
  Run `services/intake/database/ApplyIndexFix.sql` once before testing Intake in
  microservices mode. It recreates `UX_Customers_Username` as a filtered index so
  Intake can insert profiles before Identity writes the real username. This script
  is not needed when Intake uses its own `VerificationHub_Intake` database.
- See `services/intake/Data/IntakeDbContext.cs` for the EF Core configuration.
