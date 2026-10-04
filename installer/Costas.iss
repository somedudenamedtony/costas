; Costas - a station-centric FT8/FT4 client.
; Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
;
; Inno Setup script. Built by scripts/build-installer.ps1, which publishes the app, stages jt9 and Hamlib beside it
; and passes AppVersion, StageDir and OutputDir. The display name matches AppInfo.ProductName.
;
; Upgrades: AppId never changes, so running a newer installer upgrades the existing install in place (same folder,
; one entry in Apps & features), closing the app first. User data lives in the user's data folder, not here, and is
; kept. Builds from before the rename to Costas installed "FT8 Client" shortcuts and Ft8Client.App.* files; those are
; removed on upgrade.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef StageDir
  #define StageDir "..\artifacts\stage"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{ED0829A3-63BC-4E6C-99CB-5DFF4BB4A021}
AppName=Costas
AppVersion={#AppVersion}
AppVerName=Costas {#AppVersion}
AppPublisher=Costas contributors
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Costas
DefaultDirName={autopf}\Costas
DefaultGroupName=Costas
DisableProgramGroupPage=yes
; An upgrade goes into the folder of the existing install without asking.
UsePreviousAppDir=yes
DisableDirPage=auto
; Per-user install by default (no admin prompt); the dialog offers all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=Costas-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Costas.exe
UninstallDisplayName=Costas
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Left by builds named "FT8 Client".
Type: files; Name: "{app}\Ft8Client.App.*"
Type: files; Name: "{autoprograms}\FT8 Client.lnk"
Type: files; Name: "{autodesktop}\FT8 Client.lnk"

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Costas"; Filename: "{app}\Costas.exe"
Name: "{autodesktop}\Costas"; Filename: "{app}\Costas.exe"; Tasks: desktopicon

[Run]
; Starts with no radio until one is set up; it never keys a radio on start-up.
Filename: "{app}\Costas.exe"; Description: "{cm:LaunchProgram,Costas}"; Flags: nowait postinstall skipifsilent

; The log, settings and saved files live in the user's data folder and are kept on uninstall.
