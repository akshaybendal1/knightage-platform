# Knightage Deploy Utility

A config-driven CLI that automates the manual IIS deploy steps described in
each service's `DEPLOYMENT.md` (zip the build, push it over FTP, run the
pending SQL scripts) instead of doing them by hand. It's generic across
services -- one config file per app (platform, crm, accounting, ...), same
tool.

## What it does, in order

1. **Zip** the local build folder (e.g. a `dotnet publish` output).
2. **Extract** that zip locally too, so the exact same files can be
   mirror-uploaded file-by-file. (Plain FTP has no server-side "unzip" --
   there's generally no remote shell on a production FTP server -- so
   "unzipping remotely" is really: extract locally, then upload the
   extracted tree.)
3. **Upload** the zip to the FTP server, into `<remotePath>/_incoming/`, as a
   record of exactly what was deployed.
4. **Back up configs at the FTP location** -- for each path listed in
   `configFilesToPreserve` (e.g. `web.config`, an `appsettings.Production.json`
   that lives only on the server): download a timestamped copy to a local
   `.deploy-work/backups/<timestamp>/` folder, then rename it in place on the
   server to `<file>.bak`. This is what stops a fresh build's placeholder
   config from clobbering real production values.
5. **Deploy** -- mirror-upload the extracted build to the site root
   (`FtpFolderSyncMode.Update`: creates/overwrites files that exist locally,
   never deletes remote files that aren't part of the build -- logs,
   uploaded attachments, etc. are left alone).
6. **Replace backed-up config** -- delete the fresh build's own copy of each
   preserved file and rename `<file>.bak` back to `<file>`, restoring the
   real production config. Falls back to the local timestamped copy if the
   remote `.bak` is somehow missing.
7. **Run DB scripts** against the production database, in the order listed
   in `dbScripts`. Scripts are split on lines containing only `GO` (the
   `sqlcmd`/SSMS batch separator convention already used in this repo's
   `sql/*.sql` files).

Steps 3-7 (anything that touches the network or the database) are skipped
entirely with `--dry-run`, and require a `y` confirmation before running
unless you pass `--yes`. The database step gets its own separate
confirmation, since it's the one step here that isn't reversible by
re-running the tool.

## Usage

```powershell
cd tools/Knightage.DeployUtility
dotnet run -- path\to\deploy.production.json

# Non-interactive (CI/scripted):
dotnet run -- path\to\deploy.production.json --yes

# Validate a config and inspect the zip/extract output without touching
# anything remote:
dotnet run -- path\to\deploy.production.json --dry-run

# Deploy app files now, run the DB scripts separately/later:
dotnet run -- path\to\deploy.production.json --skip-db
```

Or build once and reuse the binary:

```powershell
dotnet publish -c Release -o C:\tools\knightage-deploy
C:\tools\knightage-deploy\knightage-deploy.exe deploy.production.json
```

## Config file

Copy `deploy.config.example.json`, fill in real values, and **never commit
the copy** -- it holds FTP and database passwords in plain text. Files
matching `deploy.*.json` (other than the example) are gitignored for exactly
this reason; keep your real configs named that way, e.g.
`deploy.production.json`, `deploy.staging.json`.

| Field | Meaning |
|---|---|
| `appName` | Label only, shown in console output. |
| `localPath` | The build output to deploy (e.g. `dotnet publish` folder). |
| `workDir` | Scratch space for zips/extracts/backups. Safe to delete between runs. |
| `ftp.*` | FTP(S) connection + `remotePath` (the site root on the server). |
| `configFilesToPreserve` | Paths, relative to `remotePath`, of files that hold real server-side values and must survive the deploy. Leave empty for a service whose config is entirely IIS App Pool environment variables (nothing to preserve). |
| `database` | Production DB connection. Omit if `dbScripts` is empty. |
| `dbScripts` | Paths (relative to this config file's own location, or absolute) to `.sql` files to run in order. |

## Known limitations

- Not build-tested in this sandbox (no `dotnet` SDK available here) --
  restore and build it before trusting it against a real server. The
  FluentFTP/`Microsoft.Data.SqlClient` API surface used here matches
  FluentFTP 54.x / SqlClient 5.2.x; if a newer major version changes method
  signatures, fix up the two files that call them
  (`FtpDeployer.cs`, `DbScriptRunner.cs`).
- No dry-run for the DB script step -- `--dry-run` only covers zip/extract.
  There's no safe way to "preview" arbitrary SQL without a real (or
  snapshot) database to run it against.
- DB scripts are not tracked as "already applied" -- this mirrors the
  manual process it replaces (each `DEPLOYMENT.md` today says "run
  `sql/00N_x.sql` by hand"). Re-running the same config re-runs every script
  in `dbScripts`, so drop scripts out of the list once they've shipped.
