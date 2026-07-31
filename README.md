# Knightage Platform

Tenant control-plane service for the Knightage platform. Owns the tenant directory (tenant → database connection info), automates database-per-tenant provisioning on signup, and orchestrates schema migrations across all tenant databases for every business system.

This is part of the Knightage multi-system platform:

- knightage-identity — Auth/SSO
- **knightage-platform** (this repo) — tenant control plane
- knightage-doc-intelligence — document extraction (OCR/NER) service, with optional cloud-LLM fallback
- knightage-accounting / knightage-crm / knightage-inventory-sales — business systems (built in later phases)

## Data architecture this service owns

- One SQL Server database per tenant.
- Within each tenant database: separate schemas — `accounting`, `crm`, `inventory`, and a `platform` schema (tenant-local user/role/org-membership cache synced from knightage-identity).
- Each business app's schema migrations are versioned in that app's own repo, but applied to a tenant database through this service's migration orchestration, so a tenant can be brought up to date for all systems in one controlled operation.

## Status

Phase 0 scaffold only — no business logic yet. This establishes a buildable ASP.NET Core 8 Web API skeleton following the platform's layered convention.

## Project layout

- `src/Knightage.Platform.Api` — Web API host (controllers, startup, config)
- `src/Knightage.Platform.Core` — domain models and interfaces (tenant directory entries, provisioning jobs)
- `src/Knightage.Platform.Infrastructure` — data access (Dapper + SQL Server)
- `src/Knightage.Platform.Service` — provisioning and migration orchestration logic

## Running locally

Requires the .NET 8 SDK.

```
dotnet build
dotnet run --project src/Knightage.Platform.Api
```

Swagger UI is available at `/swagger` in development.
