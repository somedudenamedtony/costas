; Ft8Client - a station-centric FT8/FT4 client.
; Copyright (C) 2026 Ft8Client contributors. GPLv3; see LICENSE.
;
; Inno Setup script. Built by scripts/build-installer.ps1, which publishes the app, stages jt9 and Hamlib beside it
; and passes AppVersion, StageDir and OutputDir. The display name matches AppInfo.ProductName.

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
AppName=FT8 Client
AppVersion={#AppVersion}
AppVerName=FT8 Client {#AppVersion}
AppPublisher=Ft8Client contributors
DefaultDirName={autopf}\FT8 Client
DefaultGroupName=FT8 Client
DisableProgramGroupPage=yes
; Per-user install by default (no admin prompt); the dialog offers all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=Ft8Client-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Ft8Client.App.exe
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FT8 Client"; Filename: "{app}\Ft8Client.App.exe"
Name: "{autodesktop}\FT8 Client"; Filename: "{app}\Ft8Client.App.exe"; Tasks: desktopicon

[Run]
; Starts in simulation until a radio is set up; it never keys a radio on start-up.
Filename: "{app}\Ft8Client.App.exe"; Description: "{cm:LaunchProgram,FT8 Client}"; Flags: nowait postinstall skipifsilent

; The log, settings and saved files live in the user's data folder and are kept on uninstall.
