# Knightage Platform

Tenant control-plane service for the Knightage platform. Owns the tenant directory (organization → tenant → per-service database) and automates database provisioning for every business system when a new organization registers.

This is part of the Knightage multi-system platform:

- knightage-identity — Auth/SSO
- **knightage-platform** (this repo) — tenant control plane
- knightage-doc-intelligence — document extraction (OCR/NER) service, with optional cloud-LLM fallback
- knightage-accounting / knightage-crm / knightage-inventory-sales — business systems

## Data architecture this service owns

Each business app already runs against its own dedicated SQL Server database (`Knightage_Accounting`, `Knightage_Crm`, `Knightage_InventorySales` for local/dev), rather than the schema-per-app-inside-one-shared-database layout originally sketched for this repo. This service follows that same shape for tenants: **one database per (tenant, service) pair**, named `Knightage_{ServiceName}_{tenantSlug}` (e.g. `Knightage_Accounting_acme-retail-co`).

- `Tenants` — one row per organization (`OrganizationId` from `knightage-identity`, a URL-safe `Slug` derived from the organization name).
- `TenantServiceDatabases` — one row per `(tenant, service)`, recording which database backs that service for that tenant.
- `src/Knightage.Platform.Api/schemas/*.sql` holds a copy of each business service's `sql/001_init.sql`, applied verbatim when that service's database is created for a tenant. These are intentionally duplicated rather than shared across repos (same tradeoff as the duplicated `ExtractedField`/`ExtractionResult` models between `knightage-accounting` and `knightage-doc-intelligence`) — keep them in sync by hand if a business service's schema changes, until real migration versioning exists.

## Provisioning flow

`knightage-identity`'s `AuthService.RegisterAsync` calls `POST /api/tenants/provision` (bearer-authenticated with the newly registered user's own token) right after creating the organization and owner user. Provisioning is best-effort from identity's side — a `knightage-platform` outage does not block registration, it's just logged — and `ProvisionAsync` is idempotent, so it's safe to call again (e.g. from this app's own "Tenants" UI) to retry or backfill an organization that failed to provision the first time.

## Status

Tenant directory, provisioning, and per-request tenant routing are all implemented and wired end-to-end: `knightage-identity` calls this service on registration, this service creates and schemas a real per-tenant database for each of Accounting, CRM, and Inventory & Sales and records the mapping, and each of those three business apps now resolves and connects to its own tenant's database per request (via a `TenantResolutionMiddleware` in each app that queries this service's `GET /api/tenants/{organizationId}` from the caller's `org_id` claim). A minimal Angular ops UI (login + a Tenants page) lets an operator see provisioned tenants and their per-service databases, and manually trigger/retry provisioning.

Verified live: two separate organizations registered through `knightage-identity`, each written to through all three business apps, confirmed via direct SQL queries that each organization's data landed only in its own database and the old shared/static databases stayed untouched. Also verified the failure paths: no token still 401s, and a token for an organization with no provisioned database for a given service gets a `503` from that service rather than a confusing failure deep in a repository call.

## Project layout

- `src/Knightage.Platform.Api` — Web API host (controllers, startup, config, bundled Angular ops UI)
- `src/Knightage.Platform.Core` — domain models and interfaces (`Tenant`, `TenantServiceDatabase`, provisioning abstractions)
- `src/Knightage.Platform.Infrastructure` — data access (Dapper + SQL Server) and the SQL Server database/schema provisioner
- `src/Knightage.Platform.Service` — `ProvisioningService`, the orchestration logic
- `client/` — Angular ops UI (login + Tenants page), built into `src/Knightage.Platform.Api/wwwroot`

## API

All endpoints below require a bearer token issued by `knightage-identity`.

- `POST /api/tenants/provision` — body `{ organizationId, organizationName }`. Ensures a tenant exists for the organization and that every known business service has a provisioned database. Idempotent.
- `GET /api/tenants` — list all tenants.
- `GET /api/tenants/{organizationId}` — tenant detail plus its per-service database map.

## Auth

This service does not issue tokens — it only validates JWTs issued by `knightage-identity`. `appsettings.json`'s `Jwt:Key`/`Issuer`/`Audience` must match `knightage-identity`'s exactly (shared HMAC secret for now; revisit before this crosses a real network boundary in production).

## Running locally

Requires the .NET 8 SDK and Node/npm for the client.

```
# Backend
dotnet build
dotnet run --project src/Knightage.Platform.Api

# Frontend (builds into src/Knightage.Platform.Api/wwwroot)
cd client
npm install
npm run build
```

`appsettings.json`'s `ConnectionStrings:Default` is used both for this service's own `Knightage_Platform` database and, with the catalog swapped, as the server to create tenant databases on (same SQL Server instance, `CREATE DATABASE` requires a `master`-scoped connection which is derived from this same connection string).

Swagger UI is available at `/swagger` in development, with a "Bearer" auth button to paste a token obtained from `knightage-identity`.

Local dev port: `5102`.
