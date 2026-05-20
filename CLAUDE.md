# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the web application
dotnet run --project Eascess/Eascess_Web.csproj

# Run all tests
dotnet test Eascess.Tests/Eascess.Tests.csproj

# Run a single test class
dotnet test Eascess.Tests/Eascess.Tests.csproj --filter "FullyQualifiedName~LicenseApiIntegrationTests"

# Run a single test method
dotnet test Eascess.Tests/Eascess.Tests.csproj --filter "FullyQualifiedName~ValidKey_CorrectDomain_ReturnsValid"

# Add a migration (run from solution root)
dotnet ef migrations add <MigrationName> --project Eascess_Infrastructure --startup-project Eascess

# Apply migrations
dotnet ef database update --project Eascess_Infrastructure --startup-project Eascess
```

## Architecture

Clean Architecture with four layers. Dependencies flow inward — Presentation → Application → Domain; Infrastructure → Domain.

```
Eascess_Domain/          → Entities, IRepository<T>, IUnitOfWork
Eascess_Application/     → Service interfaces + implementations, DTOs
Eascess_Infrastructure/  → EaccessDbContext, Repository<T>, UnitOfWork, WcagAnalyzer, Migrations
Eascess/                 → ASP.NET Core MVC (controllers, views, middleware, wwwroot)
Eascess.Tests/           → xUnit, Moq, WebApplicationFactory with InMemory DB
```

### Key design decisions

**Repository + Unit of Work:** All data access goes through `IRepository<T>` (generic CRUD + `FindAsync`/`FirstOrDefaultAsync`) and `IUnitOfWork`. Both are registered as `Scoped`. Never inject `EaccessDbContext` directly into application or presentation layers.

**DynamicCorsMiddleware** (registered before MVC, only intercepts `/api` routes): replaces static `AllowAnyOrigin`. Validates the `Origin` header against registered (non-deleted) domains in the DB. If no match, no CORS headers are added — the request is not blocked, just denied the header.

**Widget flow** (`wwwroot/js/widget.js`): IIFE that reads `data-key` and `data-api` from its own `<script>` tag, then calls `/api/license/validate` → `/api/widget/config` → builds a closed Shadow DOM. On `localhost`/`127.0.0.1`, validation failures are silently bypassed. User preferences persist in `localStorage` under key `eascess-prefs-{licenseKey}`.

**WCAG scanning** (`WcagScanService`): fetches page HTML via `HttpClient`, runs `WcagAnalyzer` rules, upserts a `Page` record, creates `ScanReport` + `ScanReportDetail` rows, and calculates a 0–100 score with severity weighting.

### API endpoints

| Endpoint | Controller | Auth |
|---|---|---|
| `GET /api/license/validate?key=&domain=` | LicenseApiController | None |
| `GET /api/widget/config?key=` | WidgetApiController | None |

Both are CORS-controlled by DynamicCorsMiddleware. All MVC routes require `[Authorize]`.

### Testing

Integration tests use `EaccessWebAppFactory` (WebApplicationFactory + EF InMemory with a unique GUID database per instance). Seed data is created via `SeedDatabase()` in the factory. Unit tests for `LicenseValidationService` mock the repository with Moq.

When adding integration tests, always use `EaccessWebAppFactory` — do not reuse instances across test classes to avoid state bleed.

### Database

SQL Server, connection string key: `"Default"`. Local dev: `Server=.;Database=Eascess;Trusted_Connection=True;TrustServerCertificate=True`.

Migrations are in `Eascess_Infrastructure/Migrations/`. The single migration (`InitialCreate`) was generated from an existing schema — all timestamps use `getutcdate()` as default, PKs use `newid()` for GUIDs.

Soft-delete pattern on `Domain`: `IsDeleted` + `DeletedAt`. Always filter `IsDeleted == false` when querying active domains.

`Domain.LicenseKey` is the link between the widget embedded on a customer site and the Eascess backend. It must match the `Origin` hostname for license validation to pass.
