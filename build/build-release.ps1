#Requires -Version 7.0
<#
.SYNOPSIS
  Builds everything needed for a Windows release in one command:
    - Server.Host          -> self-contained win-x64 single-file publish
    - Desktop.ServerManager-> self-contained win-x64 publish
    - Desktop.App (client) -> self-contained win-x64 publish
    - PostgreSQL portable binaries staged for bundling
    - ResellerSystem-Server-Setup.exe   (Inno Setup)
    - ResellerSystem-Client-Setup.exe   (Inno Setup)

  macOS packaging is intentionally NOT done here — Apple's toolchain
  (codesign, hdiutil, .app bundling) only works on macOS. Run
  build\build-macos.sh on a Mac for ResellerSystem-macOS.dmg. This script
  will tell you that at the end.

.EXAMPLE
  .\build\build-release.ps1
  .\build\build-release.ps1 -Version 0.2.0
#>
param(
    [string]$Version = "0.1.4",
    [switch]$SkipInstallerCompile
)

$ErrorActionPreference = "Stop"
$root       = Split-Path -Parent $PSScriptRoot
$distDir    = Join-Path $root "dist"
$artifacts  = Join-Path $root "artifacts"
$redistDir  = Join-Path $root "redist"
$pgZipGlob  = Join-Path $redistDir "postgresql-*-windows-x64-binaries.zip"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- 0. Clean -----------------------------------------------------------
Write-Step "1/9 Cleaning old artifacts"
foreach ($dir in @($distDir, $artifacts)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir | Out-Null
}

# --- 1. Publish Server.Host, self-contained, single file ----------------
Write-Step "2/9 Publishing Server.Host (self-contained win-x64)"
dotnet publish "$root\src\Server.Host\Server.Host.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o "$distDir\server"
if ($LASTEXITCODE -ne 0) { throw "Server.Host publish failed." }

# Point storage/backup/update/temp roots at shared ProgramData, and the
# admin password placeholder stays empty here — Install-PostgreSql.ps1
# overwrites appsettings.Production.json with the real generated password
# at install time (not baked into the installer / not in git).
$appsettingsPath = "$distDir\server\appsettings.json"
$settings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$settings.Storage.StorageRoot = "C:\ProgramData\ResellerSystem\storage"
$settings.Storage.BackupRoot  = "C:\ProgramData\ResellerSystem\backups"
$settings.Storage.UpdateRoot  = "C:\ProgramData\ResellerSystem\updates"
$settings.Storage.TempRoot    = "C:\ProgramData\ResellerSystem\temp"
$settings.Versioning.ServerVersion = $Version
$settings | ConvertTo-Json -Depth 6 | Set-Content $appsettingsPath -Encoding UTF8

# --- 2. Publish Desktop.ServerManager ------------------------------------
Write-Step "3/9 Publishing Desktop.ServerManager (self-contained win-x64)"
dotnet publish "$root\src\Desktop.ServerManager\Desktop.ServerManager.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -o "$distDir\servermanager"
if ($LASTEXITCODE -ne 0) { throw "Desktop.ServerManager publish failed." }

# --- 2b. Publish Server.Updater -------------------------------------------
Write-Step "3b/9 Publishing Server.Updater (self-contained win-x64)"
dotnet publish "$root\src\Server.Updater\Server.Updater.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -o "$distDir\updater"
if ($LASTEXITCODE -ne 0) { throw "Server.Updater publish failed." }

# --- 3. Publish Desktop.App (Windows client) -----------------------------
Write-Step "4/9 Publishing Desktop.App for Windows (self-contained win-x64)"
dotnet publish "$root\src\Desktop.App\Desktop.App.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o "$distDir\client-win"
if ($LASTEXITCODE -ne 0) { throw "Desktop.App (Windows) publish failed." }

# --- 4. Stage PostgreSQL portable binaries -------------------------------
Write-Step "5/9 Staging PostgreSQL portable binaries"
# We deliberately do NOT auto-download PostgreSQL during CI/build to avoid
# a hidden network dependency and supply-chain surprise. Place the official
# EDB "binaries" zip (PostgreSQL License, free) in \redist\ once, e.g.:
#   https://www.enterprisedb.com/download-postgresql-binaries
#   -> postgresql-16.x-x-windows-x64-binaries.zip
$pgZip = Get-ChildItem -Path $pgZipGlob -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $pgZip) {
    Write-Warning "No PostgreSQL binaries zip found in '$redistDir'."
    Write-Warning "Download the official Windows x64 'binaries' zip from https://www.enterprisedb.com/download-postgresql-binaries"
    Write-Warning "and place it there as postgresql-<version>-windows-x64-binaries.zip, then re-run this script."
    throw "Missing PostgreSQL redistributable — see warning above."
}
Expand-Archive -Path $pgZip.FullName -DestinationPath "$distDir\postgresql" -Force
Write-Host "Staged $($pgZip.Name) -> dist\postgresql"

# --- 5. Compile installers with Inno Setup -------------------------------
if ($SkipInstallerCompile) {
    Write-Step "6/9 Skipping installer compilation (-SkipInstallerCompile)"
} else {
    Write-Step "6/9 Compiling installers (Inno Setup)"
    $iscc = @(
        "$Env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$Env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        Write-Warning "Inno Setup 6 not found. Install it (free) from https://jrsoftware.org/isdl.php and re-run,"
        Write-Warning "or re-run with -SkipInstallerCompile to only produce the dist\ publish folders."
        throw "Inno Setup compiler (ISCC.exe) not found."
    }

    & $iscc "$root\installer\inno\ServerSetup.iss" "/DMyAppVersion=$Version"
    if ($LASTEXITCODE -ne 0) { throw "Server installer compilation failed." }

    & $iscc "$root\installer\inno\ClientSetup.iss" "/DMyAppVersion=$Version"
    if ($LASTEXITCODE -ne 0) { throw "Client installer compilation failed." }
}

Write-Step "7/9 Packaging server-update-$Version.zip + update-manifest.json"
$updateZipPath = "$artifacts\server-update-$Version.zip"
Compress-Archive -Path "$distDir\server\*" -DestinationPath $updateZipPath -Force
$updateZipHash = (Get-FileHash -Path $updateZipPath -Algorithm SHA256).Hash.ToLower()
$updateZipSize = (Get-Item $updateZipPath).Length

$manifest = [ordered]@{
    productVersion            = $Version
    releasedAt                = (Get-Date).ToUniversalTime().ToString("o")
    minimumUpgradeFromVersion = "0.1.0"
    server                    = [ordered]@{
        url       = "REPLACE_WITH_GITHUB_RELEASE_ASSET_URL/server-update-$Version.zip"
        sha256    = $updateZipHash
        sizeBytes = $updateZipSize
    }
    releaseNotesUrl = "REPLACE_WITH_CHANGELOG_URL#$Version"
}
$manifestPath = "$artifacts\update-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
Write-Host "Wrote $manifestPath — replace the REPLACE_WITH_* placeholders with real GitHub Release URLs before publishing."

Write-Step "8/9 Running unit tests (fast layers only — no Docker required)"
dotnet test "$root\tests\Server.Domain.Tests" -c Release --nologo
dotnet test "$root\tests\Server.Application.Tests" -c Release --nologo
dotnet test "$root\tests\Server.Api.Tests" -c Release --nologo

Write-Step "9/9 Done"
Write-Host "`nArtifacts:" -ForegroundColor Green
Get-ChildItem $artifacts -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.FullName)" }
Write-Host "`nNote: ResellerSystem-macOS.dmg is NOT built by this script." -ForegroundColor Yellow
Write-Host "Run build\build-macos.sh on a Mac to produce it." -ForegroundColor Yellow
