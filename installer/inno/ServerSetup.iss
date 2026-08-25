; ResellerSystem-Server-Setup.iss
; Built with Inno Setup 6.x (https://jrsoftware.org/isinfo.php) — free for
; commercial and non-commercial use ("Inno Setup License", no royalties).
;
; Expects the release build script to have already populated:
;   dist\server\            self-contained win-x64 publish of Server.Host
;   dist\servermanager\     self-contained win-x64 publish of Desktop.ServerManager
;   dist\postgresql\        portable PostgreSQL binaries (bin/, share/, lib/)
;   installer\scripts\      the .ps1 files in this repo
;
; See build\build-release.ps1 for how these folders are produced.

#define MyAppName "Reseller System Server"
#define MyAppVersion "0.1.4"
#define MyAppPublisher "ResellerSystem"
#define MyServiceName "ResellerSystemServer"
#define MyPgServiceName "ResellerSystemPostgreSQL"
#define MyPort "5000"
#define MyPgPort "55432"

[Setup]
AppId={{B6E3B6A0-6E1E-4B9B-8B7B-RESELLERSRV01}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ResellerSystem
DefaultGroupName=Reseller System
DisableProgramGroupPage=yes
; Server must be able to register services / write firewall rules / bind ProgramData.
PrivilegesRequired=admin
OutputBaseFilename=ResellerSystem-Server-Setup
OutputDir=..\..\artifacts
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Kept as a plain installer (not MSI/WiX) specifically so a future updater
; can silently re-run this same .exe with /VERYSILENT for in-place upgrades.
UninstallDisplayIcon={app}\server\Server.Host.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\..\dist\server\*"; DestDir: "{app}\server-versions\{#MyAppVersion}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\dist\updater\*"; DestDir: "{app}\updater"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\dist\servermanager\*"; DestDir: "{app}\servermanager"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\dist\postgresql\*"; DestDir: "{app}\postgresql"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\scripts\Initialize-ServerVersion.ps1"; DestDir: "{app}\installer-scripts"; Flags: ignoreversion
Source: "..\scripts\Install-PostgreSql.ps1"; DestDir: "{app}\installer-scripts"; Flags: ignoreversion
Source: "..\scripts\Install-ServerService.ps1"; DestDir: "{app}\installer-scripts"; Flags: ignoreversion
Source: "..\scripts\Uninstall-ResellerSystem.ps1"; DestDir: "{app}\installer-scripts"; Flags: ignoreversion

[Dirs]
Name: "{app}\config"
Name: "{app}\logs\application"
Name: "{app}\logs\error"
Name: "{app}\logs\database"
Name: "{app}\logs\update"
Name: "{commonappdata}\ResellerSystem\pgdata"
Name: "{commonappdata}\ResellerSystem\storage"
Name: "{commonappdata}\ResellerSystem\backups"
Name: "{commonappdata}\ResellerSystem\updates"
Name: "{commonappdata}\ResellerSystem\temp"

[Icons]
Name: "{group}\Reseller System Server Manager"; Filename: "{app}\servermanager\Desktop.ServerManager.exe"
Name: "{group}\Uninstall Reseller System Server"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Reseller System Server Manager"; Filename: "{app}\servermanager\Desktop.ServerManager.exe"

[Run]
; 1) Establish the side-by-side version layout Server.Updater relies on —
;    {app}\server becomes a symlink into {app}\server-versions\{version}.
;    Must run FIRST: both PostgreSQL provisioning (writes appsettings
;    under {app}\server\) and service registration need that path to exist.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\installer-scripts\Initialize-ServerVersion.ps1"" -InstallDir ""{app}"" -Version ""{#MyAppVersion}"""; \
    StatusMsg: "Preparing server files..."; Flags: runhidden waituntilterminated

; 2) Provision PostgreSQL (idempotent — safe on upgrade/repair).
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\installer-scripts\Install-PostgreSql.ps1"" -InstallDir ""{app}"" -DataDir ""{commonappdata}\ResellerSystem\pgdata"" -ServiceName ""{#MyPgServiceName}"" -Port {#MyPgPort}"; \
    StatusMsg: "Configuring local PostgreSQL database..."; Flags: runhidden waituntilterminated

; 3) Register + start the server Windows Service, wire storage/backup/log
;    roots to the shared ProgramData folders created above, then health-check.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\installer-scripts\Install-ServerService.ps1"" -InstallDir ""{app}"" -ServiceName ""{#MyServiceName}"" -Port {#MyPort}"; \
    StatusMsg: "Starting Reseller System Server..."; Flags: runhidden waituntilterminated

; 4) Launch the Server Manager so the user immediately sees the result.
Filename: "{app}\servermanager\Desktop.ServerManager.exe"; Description: "Open Reseller System Server Manager"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\installer-scripts\Uninstall-ResellerSystem.ps1"" -ServiceName ""{#MyServiceName}"" -PgServiceName ""{#MyPgServiceName}"""; \
    Flags: runhidden waituntilterminated

[Code]
// Storage/backup/update/temp roots and the port are baked into
// appsettings.json at build time (see build-release.ps1), pointing at the
// {commonappdata}\ResellerSystem subfolders created in [Dirs] above, so no
// further templating is needed here.
