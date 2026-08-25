#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registers ResellerSystem.Server as a Windows Service, opens the Private-
  profile firewall rule for the API port, starts the service, and polls
  /health until the server (which self-provisions the master database and
  applies migrations on startup - see Server.Host StartupChecks) reports
  healthy. Called by the Inno Setup installer after Install-PostgreSql.ps1.
#>
param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [string]$ServiceName = "ResellerSystemServer",
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"
$exePath = Join-Path $InstallDir "server\Server.Host.exe"
$logDir  = Join-Path $InstallDir "logs\update"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-Log($msg) {
    $line = "[$(Get-Date -Format o)] $msg"
    Add-Content -Path (Join-Path $logDir "install.log") -Value $line
    Write-Host $line
}

# --- 1. Firewall rule - Private network profile only, never Public/Domain ---
$ruleName = "ResellerSystem Server (Private LAN)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port `
        -Profile Private | Out-Null
    Write-Log "Created firewall rule '$ruleName' for TCP $Port (Private profile only)."
} else {
    Write-Log "Firewall rule '$ruleName' already exists."
}

# --- 2. Register the Windows Service (sc.exe - no extra tooling needed) ----
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Log "Service '$ServiceName' exists - stopping for upgrade."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# ASP.NET Core's UseWindowsService() understands being launched by SCM directly;
# binPath just points at the self-contained single-file exe.
& sc.exe create $ServiceName binPath= "`"$exePath`"" start= delayed-auto DisplayName= "Reseller System Server" | Out-Null
& sc.exe description $ServiceName "Reseller System - local business server (API, database access, file storage)." | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

Write-Log "Registered Windows Service '$ServiceName'."

# --- 3. Start and health-check ----------------------------------------------
Start-Service -Name $ServiceName
Write-Log "Service start requested. Waiting for /health ..."

$healthy = $false
for ($i = 0; $i -lt 150; $i++) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 2
        if ($response.status -eq "healthy") { $healthy = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
}

if (-not $healthy) {
    Write-Log "ERROR: server did not report healthy within 300 seconds. Check logs under $InstallDir\logs."
    exit 1
}

Write-Log "Server is healthy on port $Port."
exit 0
