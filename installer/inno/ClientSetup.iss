; ResellerSystem-Client-Setup.iss
; Standard non-admin per-user install of the Avalonia desktop client —
; no services, no firewall rules, no PostgreSQL. Just files + shortcuts.

#define MyAppName "Reseller System"
#define MyAppVersion "0.1.5"
#define MyAppPublisher "ResellerSystem"

[Setup]
AppId={{B6E3B6A0-6E1E-4B9B-8B7B-RESELLERCLI01}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ResellerSystem Client
DefaultGroupName=Reseller System
DisableProgramGroupPage=yes
; Per-user install — a shop employee's PC shouldn't need admin rights
; just to run the client.
PrivilegesRequired=lowest
OutputBaseFilename=ResellerSystem-Client-Setup
OutputDir=..\..\artifacts
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Desktop.App.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\..\dist\client-win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Reseller System"; Filename: "{app}\Desktop.App.exe"
Name: "{autodesktop}\Reseller System"; Filename: "{app}\Desktop.App.exe"
Name: "{group}\Uninstall Reseller System"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Desktop.App.exe"; Description: "Launch Reseller System"; Flags: postinstall nowait skipifsilent
