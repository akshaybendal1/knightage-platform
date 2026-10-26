# CRM schema bundle

`crm-full-schema.sql` is a concatenation of every `knightage-crm/sql/0*.sql`
migration -- the **plain** files, deliberately not the `appuser.`-qualified
`.production.sql` twins -- in numeric order. It's replayed against a
brand-new, empty database to give it CRM's exact current schema when a
tenant is moved to a dedicated CRM database (see
`TenantDataService`/`ProvisioningService.CreateDedicatedCrmDatabaseAsync`).

**Why plain, not `.production.sql`**: the login that runs `CREATE DATABASE`
becomes that database's owner (`dbo`) by default, so unqualified
`CREATE TABLE`/`CREATE PROCEDURE` statements land in `dbo` consistently --
which is exactly what CRM's own repository code expects, since it always
calls tables/procs unqualified (e.g. `"usp_Leads_GetFieldSales"`, not
`"appuser.usp_Leads_GetFieldSales"`). Mixing in `appuser.`-qualified
statements produced `Cannot find the object 'appuser.Documents'` on a fresh
database, because nothing there maps the `appuser` login to a same-named
schema the way the real (much older) `Knightage_Crm` database happens to.

## Regenerating

Whenever knightage-crm gets a new `sql/0NN_*.sql` migration, regenerate this
file so a freshly-provisioned dedicated database stays correct -- concatenate
every plain `sql/0*.sql` file (skip any `*.production.sql`), in numeric
order, into this file.

(See the git history of this repo's now-removed `schemas/crm.sql` for the
same maintenance pattern this file replaces -- it needed the same kind of
manual sync whenever CRM's schema changed.)
