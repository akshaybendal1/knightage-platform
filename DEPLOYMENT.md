# Deploying to IIS

This service ships an API plus a bundled Angular ops UI (built into
`src/Knightage.Platform.Api/wwwroot`). It's also the only service whose SQL
login needs elevated rights — it provisions a brand-new database per
(tenant, service) at registration time.

## One-time server prerequisites

- **.NET 8 Hosting Bundle** installed on the IIS server (same as every
  Knightage service) — https://dotnet.microsoft.com/download/dotnet/8.0,
  "Hosting Bundle" for Windows. Run `iisreset` after installing it.
- **Node.js 20+** wherever you run the Angular build (doesn't need to be the
  IIS server itself — you can build elsewhere and copy the output).
- A SQL Server instance reachable from the IIS server, with a SQL login that
  has **`CREATE DATABASE` rights (the `dbcreator` server role, minimum)** —
  not just read/write on one database. `ConnectionStrings:Default` is used
  both for this service's own `knightage-platform` database and, with the
  catalog swapped to `master`, to create every new tenant's database on the
  same server. A least-privilege login scoped to one database (like
  identity's or crm's) will provision-fail here.

## Build & publish

```powershell
# 1. Angular ops UI first -- it needs to exist in wwwroot before publish picks it up
cd client
npm install
npm run build   # outputs into ..\src\Knightage.Platform.Api\wwwroot

# 2. API (packages the wwwroot output automatically)
cd ..\src\Knightage.Platform.Api
dotnet publish -c Release -o C:\inetpub\knightage-platform
```

`dotnet publish` auto-generates a working `web.config` (in-process hosting
model) every time — don't hand-edit or check one in.

`src/Knightage.Platform.Api/schemas/*.sql` (the per-service schema templates
used to provision new tenant databases) are `Content` items in the `.csproj`
and get copied to the publish output automatically — nothing extra to do
there, but see the warning below.

## IIS site setup

- New Application Pool: .NET CLR Version = **No Managed Code**, Start Mode =
  `AlwaysRunning`.
- New Site with its physical path set to the publish folder above, assigned
  to that app pool, bound to `https://platform.<yourdomain>` with a TLS
  certificate bound in IIS.

## Required configuration (environment variables)

Same mechanism as every Knightage service: `appsettings.json` ships
placeholder values, real ones go on the **Application Pool's environment
variables** (IIS Manager → Application Pools → pool → Advanced Settings →
Environment Variables), which override `appsettings.json` automatically via
ASP.NET Core's `Section__Key` env-var convention.

| Variable | Set to | Notes |
|---|---|---|
| `ConnectionStrings__Default` | `Server=<sql-host>;Database=knightage-platform;User Id=<login>;Password=<real-password>;TrustServerCertificate=True` | this login needs `dbcreator` — see prerequisites above |
| `Jwt__Key` | same value as `knightage-identity`'s `Jwt__Key` | **must match byte-for-byte** — this service only validates tokens, it doesn't issue them |
| `Jwt__Issuer` | `knightage-identity` | must match identity exactly |
| `Jwt__Audience` | `knightage-platform-clients` | must match identity exactly |

There's no `Cors` section in this service's config today — its API is only
called server-to-server (from `knightage-identity` during provisioning, and
from each business app's `TenantResolutionMiddleware`), never directly from
a browser on another origin. If you later point a browser at this service's
own ops UI from a *different* origin than where it's hosted, you'd need to
add CORS then — not needed for the current same-origin bundled-Angular
setup.

## Database

Before the first request, create the `knightage-platform` database on your
SQL Server and run `sql/001_init.sql` against it. Tenant databases
themselves are **not** created by hand — they're provisioned automatically
per organization, using the `schemas/*.sql` files bundled into this
service's own publish output.

**Before going live**, double-check `src/Knightage.Platform.Api/schemas/crm.sql`
is fully in sync with every migration under `knightage-crm/sql/*.sql`. This
file is a hand-maintained copy, not a shared reference — a CRM schema change
that lands in `knightage-crm` without a matching update here will silently
provision every *new* tenant's CRM database with a stale schema (existing
tenants are unaffected either way, since they were already provisioned
before the change). Same applies to `schemas/accounting.sql` and
`schemas/inventorysales.sql` once those services have real migrations.

## Verify

`/swagger` is disabled outside Development. Confirm the app started by
hitting `GET /api/tenants` with a valid bearer token (obtained from
`knightage-identity`) and expect a `200` with a JSON array, not a connection
failure. If it doesn't come up, check `logs\stdout` in the publish folder
(enable `stdoutLogEnabled="true"` in the generated `web.config` temporarily
while diagnosing, then turn it back off).
