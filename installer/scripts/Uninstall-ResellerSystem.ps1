#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Cleanly stops and removes the Windows Services created by the installer,
  and removes the firewall rule. Deliberately does NOT delete
  {commonappdata}\ResellerSystem (databases, documents, backups) — Inno
  Setup's uninstaller only removes files it installed; user data survives
  an uninstall by design so a reinstall/repair doesn't lose the business's
  data. A separate "Remove all data" option can be added to Server Manager
  later if a full wipe is ever wanted.
#>
param(
    [string]$ServiceName = "ResellerSystemServer",
    [string]$PgServiceName = "ResellerSystemPostgreSQL"
)

$ErrorActionPreference = "SilentlyContinue"

foreach ($svc in @($ServiceName, $PgServiceName)) {
    $s = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($s) {
        Stop-Service -Name $svc -Force
        Start-Sleep -Seconds 2
        & sc.exe delete $svc | Out-Null
    }
}

Get-NetFirewallRule -DisplayName "ResellerSystem Server (Private LAN)" -ErrorAction SilentlyContinue |
    Remove-NetFirewallRule -ErrorAction SilentlyContinue

exit 0
