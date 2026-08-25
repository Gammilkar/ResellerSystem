#Requires -RunAsAdministrator
<#
.SYNOPSIS
  One-time (per install/upgrade-via-installer) step that establishes the
  side-by-side version layout Server.Updater relies on:

    {InstallDir}\server-versions\{version}\   <- actual published files (Inno Setup extracts here)
    {InstallDir}\server\                       <- directory symlink -> the folder above
    {InstallDir}\server-versions\current.txt   <- plain text, current version string

  The Windows Service always points at {InstallDir}\server\Server.Host.exe
  (via the symlink), so it never needs reconfiguring when Server.Updater
  swaps versions later - only the symlink target and current.txt change.
#>
param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = "Stop"
$versionDir = Join-Path $InstallDir "server-versions\$Version"
$linkPath = Join-Path $InstallDir "server"
$currentFile = Join-Path $InstallDir "server-versions\current.txt"

if (-not (Test-Path $versionDir)) {
    throw "Expected published files at '$versionDir' - Inno Setup [Files] section must extract there."
}

if (Test-Path $linkPath) {
    $item = Get-Item $linkPath -Force
    if ($item.LinkType) {
        # Already a symlink from a previous install/upgrade-via-installer run - remove and recreate.
        Remove-Item $linkPath -Force
    } else {
        # First-ever install where {app}\server exists as a real folder
        # (shouldn't normally happen - Inno extracts to server-versions\ -
        # but guard against it rather than deleting user data silently).
        Rename-Item $linkPath "$linkPath.bak-$(Get-Date -Format yyyyMMddHHmmss)"
    }
}

New-Item -ItemType SymbolicLink -Path $linkPath -Target $versionDir | Out-Null
Set-Content -Path $currentFile -Value $Version -NoNewline

Write-Host "Server version layout initialized: $linkPath -> $versionDir"
exit 0
