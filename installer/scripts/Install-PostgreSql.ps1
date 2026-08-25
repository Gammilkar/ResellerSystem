#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs and provisions a local, service-registered PostgreSQL instance
  for ResellerSystem, using bundled portable PostgreSQL binaries (EDB
  "Portable"/zip build - PostgreSQL License, free for any use, no GUI
  installer involved). Idempotent: safe to re-run on upgrade.

  Called by the Inno Setup installer's [Run] section. Never prompts -
  fully silent, so the end user never sees a console window doing this.

.PARAMETER InstallDir
  Root install directory chosen in the wizard, e.g. C:\Program Files\ResellerSystem

.PARAMETER DataDir
  Where PostgreSQL stores its data, e.g. C:\ProgramData\ResellerSystem\pgdata
#>
param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$DataDir,
    [string]$ServiceName = "ResellerSystemPostgreSQL",
    [int]$Port = 55432,
    [string]$AdminUser = "reseller_admin"
)

$ErrorActionPreference = "Stop"
$pgBinDir   = Join-Path $InstallDir "postgresql\bin"
$pgCtl      = Join-Path $pgBinDir "pg_ctl.exe"
$initdb     = Join-Path $pgBinDir "initdb.exe"
$psql       = Join-Path $pgBinDir "psql.exe"
$logDir     = Join-Path $InstallDir "logs\database"
$secretsDir = Join-Path $InstallDir "config"
$credFile   = Join-Path $secretsDir "postgres.credentials.json"

New-Item -ItemType Directory -Force -Path $logDir, $secretsDir | Out-Null

function Write-Log($msg) {
    $line = "[$(Get-Date -Format o)] $msg"
    Add-Content -Path (Join-Path $logDir "postgres-provisioning.log") -Value $line
    Write-Host $line
}

# --- 1. Generate (or reuse) a strong local-only admin password ------------
if (Test-Path $credFile) {
    Write-Log "Existing credentials file found - reusing (upgrade scenario)."
    $creds = Get-Content $credFile -Raw | ConvertFrom-Json
    $AdminPassword = $creds.Password
} else {
    Add-Type -AssemblyName System.Web
    $AdminPassword = [System.Web.Security.Membership]::GeneratePassword(24, 6)
    $creds = @{ Username = $AdminUser; Password = $AdminPassword; Port = $Port; GeneratedAt = (Get-Date -Format o) }
    $creds | ConvertTo-Json | Set-Content -Path $credFile -Encoding UTF8

    # Lock the credentials file down to Administrators + SYSTEM only.
    icacls $credFile /inheritance:r | Out-Null
    icacls $credFile /grant:r "SYSTEM:(F)" "BUILTIN\Administrators:(F)" | Out-Null
    Write-Log "Generated new local PostgreSQL admin credentials, written to $credFile (ACL-restricted)."
}

# --- 2. initdb (first install only) ----------------------------------------
if (-not (Test-Path (Join-Path $DataDir "PG_VERSION"))) {
    Write-Log "Initializing PostgreSQL data directory at $DataDir ..."
    New-Item -ItemType Directory -Force -Path $DataDir | Out-Null

    $pwFile = New-TemporaryFile
    Set-Content -Path $pwFile -Value $AdminPassword -NoNewline

    & $initdb --username=$AdminUser --pwfile="$pwFile" --auth=scram-sha-256 --encoding=UTF8 -D "$DataDir" | Out-Null
    Remove-Item $pwFile -Force

    # Bind to localhost only - never expose PostgreSQL itself to the LAN;
    # only the Server API (a separate port) is reachable from other machines.
    $confPath = Join-Path $DataDir "postgresql.conf"
    (Get-Content $confPath) `
        -replace '#?listen_addresses\s*=.*', "listen_addresses = 'localhost'" `
        -replace '#?port\s*=.*', "port = $Port" `
        | Set-Content $confPath

    Write-Log "initdb complete."
} else {
    Write-Log "Data directory already initialized - skipping initdb (upgrade/repair scenario)."
}

# --- 3. Register as a Windows Service --------------------------------------
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Log "Registering Windows Service '$ServiceName' ..."
    & $pgCtl register -N $ServiceName -D "$DataDir" -w `
        -o "-p $Port" `
        --startup-type=automatic-delayed | Out-Null
} else {
    Write-Log "Service '$ServiceName' already registered."
}

Set-Service -Name $ServiceName -StartupType Automatic

# --- 4. Start it and wait until ready ---------------------------------------
Write-Log "Starting service '$ServiceName' ..."
Start-Service -Name $ServiceName

$env:PGPASSWORD = $AdminPassword
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    & $psql -h localhost -p $Port -U $AdminUser -d postgres -c "SELECT 1;" *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $ready) {
    Write-Log "ERROR: PostgreSQL did not become ready within 30 seconds."
    exit 1
}
Write-Log "PostgreSQL is up and accepting connections on port $Port."

# --- 5. Write connection info into Server.Host's config ---------------------
$appsettingsPath = Join-Path $InstallDir "server\appsettings.Production.json"
$settings = @{
    Postgres = @{
        Host               = "localhost"
        Port               = $Port
        AdminUsername      = $AdminUser
        AdminPassword      = $AdminPassword
        MasterDatabaseName = "reseller_system"
    }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content -Path $appsettingsPath -Encoding UTF8
icacls $appsettingsPath /inheritance:r | Out-Null
icacls $appsettingsPath /grant:r "SYSTEM:(F)" "BUILTIN\Administrators:(F)" | Out-Null

Write-Log "Wrote production connection settings to $appsettingsPath (ACL-restricted, not in git)."
Write-Log "PostgreSQL provisioning finished successfully."
exit 0
