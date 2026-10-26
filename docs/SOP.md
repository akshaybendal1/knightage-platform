# Knightage Platform — SOP

Version 1.0 · 2026-10-26. Shared steps are in `knightage-deploy/docs/SOP-Operations.md`. `DEPLOYMENT.md` in this repo
describes an older design (per-tenant databases, environment-variable config) — follow this file instead.

## 1. Facts

| | |
|---|---|
| URL | https://platform.knightagetech.com (local http://localhost:5102) |
| Code | `src/Knightage.Platform.Api` (.NET 8, Dapper, inline SQL), `client/` (Angular → `wwwroot/browser`) |
| Database | `knightage-platform` — Tenants, TenantServiceDatabases, Subscriptions, Plans, TenantMigrations; migrations `sql/001`–`008` |
| Also writes | `Knightage_Crm` (default stages for new orgs, tenant migrations) via `ConnectionStrings:CrmShared`; reads `knightage-identity` |
| Deploy config | `knightage-deploy/configs/platform.json` → FTP `/platform.knightagetech.com/publish` |
| Who uses it | every customer (Services page / launcher), the PlatformAdmin (Tenants, Plans), and every other app server-to-server |

## 2. Configuration

Template: `knightage-deploy/ops/server/templates/appsettings.platform.json`. The keys that matter most:

- `Client:ServiceUrls:{Crm,Accounting,InventorySales}` — drives the launcher tiles **and** every app's lookup of the
  others (CRM → Inventory customer sync, Inventory → Accounting posting). A missing entry silently breaks those.
- `Smtp:*` and `App:PlatformAdminEmail` — **both missing on production today**, so "Request this plan", service
  requests and field-force requests answer 503. Platform has no log-only fallback.
- `Jwt:*` — same trio as every app.

## 3. Release procedure

```powershell
cd C:\Akshay\Projects\Knightage\knightage-platform
git status            # 79 uncommitted files, last commit 25 Aug -- commit + push first
cd client; npx ng build --configuration production; cd ..
Remove-Item -Recurse -Force src\Knightage.Platform.Api\bin\Release\net8.0\publish -ErrorAction SilentlyContinue
cd src\Knightage.Platform.Api; dotnet publish -c Release -o bin/Release/net8.0/publish; cd ..\..
```

Migrations (`Invoke-Migrations.ps1 -App platform ...`), deploy (`knightage-deploy.exe configs\platform.json ...`), verify
(`Test-Deployment.ps1 -Only platform`). **Platform is on every request path of the other apps** (status lookups, cached
15 min, 3 s timeout, fail-open): deploy it at a quiet time; while it is down, the apps treat everyone as Active.

## 4. PlatformAdmin tasks

| Task | Where |
|---|---|
| See all tenants, plan, subscription status, service rows | Tenants (search/sort/filter) |
| Assign a plan / set subscription dates and grace days | Tenants → org; bulk: `ops\tenant\Set-TenantSubscription.sql` |
| Enable/disable a service for a tenant | Tenants → org → service row (Disabled = 403 in that app) |
| Field Force Tracking, Accounting multiple companies, accent colour | Tenants → org |
| Create/edit plans (max users, max records, price) | Plans |
| Re-provision a tenant whose sign-up provisioning failed | Tenants → "Provision a tenant" with the organization id |
| Move a tenant's CRM data to its own database | Tenant migration (rare; blocks that tenant with 503 while it runs — take a backup first) |
| Check one tenant end to end | `ops\tenant\Get-TenantStatus.sql` |
| Make someone PlatformAdmin | `UPDATE Users SET Role='PlatformAdmin'` in `knightage-identity` (no UI) |

## 5. Subscription states (computed, not stored)

No subscription row → **NotSubscribed (full access, no end)** · today ≤ EndDate → Active · within grace days → ReadOnly
(writes 402) · after → Disabled (everything 402). The apps cache the result for 15 minutes.

## 6. Known gaps (2026-10-26 review)

**`POST /api/tenants/provision` accepts any signed-in user** — anyone can create tenants for arbitrary ids or switch a
service back on for their own org (confirmed locally; fix first). No security headers; unknown `/api/*` routes return
the web page with 200; no global error handler/logging; the tenant-migration table list (`CrmTenantTables.cs`) is out of
date; no billing/payment integration; trial periods never expire; Platform's look differs from the other apps.
