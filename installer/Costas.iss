; Costas - a station-centric FT8/FT4 client.
; Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
;
; Inno Setup script. Built by scripts/build-installer.ps1, which publishes the app, stages jt9 and Hamlib beside it
; and passes AppVersion, StageDir and OutputDir. The display name matches AppInfo.ProductName.
;
; Upgrades: AppId never changes, so running a newer installer upgrades the existing install in place (same folder,
; one entry in Apps & features), closing the app first. The app's Update button runs it with /SILENT (progress
; window only) and reopens the app itself once it has finished. Costas takes a few seconds to close (it stops jt9,
; rigctld and its services first), so a silent run waits for them to exit before it installs; see PrepareToInstall. User data lives in the user's data folder, not here,
; and is kept. Builds from before the rename to Costas installed "FT8 Client" shortcuts and Ft8Client.App.* files; those are
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
SetupIconFile=..\src\Ft8Client.App\Assets\costas.ico
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

[Code]
// The Update button starts this installer and then closes Costas, which takes a few seconds while it stops jt9,
// rigctld and its services. Setup used to start copying while they still held their files, the copy failed and the
// upgrade rolled back, so the first Update click seemed to do nothing. A silent run now waits for Costas, jt9 and
// rigctld from the install folder to exit first. Copies of jt9 or rigctld from elsewhere (WSJT-X, Hamlib) are not ours
// and do not hold it up.
const
  WaitForExitMs = 60000;
  PollMs = 250;

// The name of a Costas, jt9 or rigctld process running from Dir, or '' when there is none (or they cannot be listed).
function RunningFrom(const Dir: String): String;
var
  Locator, Service, Procs, Proc: Variant;
  Prefix: String;
  I: Integer;
begin
  Result := '';
  Prefix := Lowercase(AddBackslash(Dir));
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Procs := Service.ExecQuery('SELECT Name, ExecutablePath FROM Win32_Process WHERE Name = ''Costas.exe'' OR Name = ''jt9.exe'' OR Name = ''rigctld.exe''');
    for I := 0 to Procs.Count - 1 do
    begin
      Proc := Procs.ItemIndex(I);
      if not VarIsNull(Proc.ExecutablePath) then
        if Pos(Prefix, Lowercase(Proc.ExecutablePath)) = 1 then
        begin
          Result := Proc.Name;
          Exit;
        end;
    end;
  except
    Log('Could not list running programs: ' + GetExceptionMessage);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Name: String;
  Waited: Integer;
begin
  Result := '';
  if not WizardSilent then Exit;
  Waited := 0;
  Name := RunningFrom(ExpandConstant('{app}'));
  if Name <> '' then Log('Waiting for ' + Name + ' to exit before installing');
  while (Name <> '') and (Waited < WaitForExitMs) do
  begin
    Sleep(PollMs);
    Waited := Waited + PollMs;
    Name := RunningFrom(ExpandConstant('{app}'));
  end;
  if Name <> '' then
    Log(Name + ' is still running after ' + IntToStr(WaitForExitMs div 1000) + ' s; Setup will close it')
  else if Waited > 0 then
    Log('Costas and its helpers exited after ' + IntToStr(Waited) + ' ms');
end;
