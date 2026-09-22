; snitch.out Windows installer (Inno Setup 6)
; Built in CI: iscc /DAppVersion=0.1.0 installer/snitch.iss
; Requires the dotnet publish output to exist first (see ci-release.yml).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "snitch.out"
#define AppPublisher "7yass"
#define AppURL "https://github.com/7yass/snitch.out"
#define AppExe "snitch.out.exe"

[Setup]
AppId={{83C5392F-4ACB-4F1B-8D3C-782858FF335C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\icon.ico
WizardImageFile=wizard-image.bmp
WizardSmallImageFile=wizard-small.bmp
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startmenu"; Description: "Create a Start Menu shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\Bloxstrap\bin\Release\net6.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startmenu
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Tell the app it is already installed so first launch goes straight to Roblox setup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppName}"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppName}"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: filesandordirs; Name: "{app}\Downloads"

[Code]
const
  DotNetSubkey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DotNetDownloadUrl = 'https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe';

function HasDotNet6Desktop(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM64, DotNetSubkey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '6.' then
        Result := True;
  if (not Result) and RegGetValueNames(HKCU, DotNetSubkey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '6.' then
        Result := True;
end;

function InitializeSetup(): Boolean;
var
  Answer: Integer;
  ExecCode: Integer;
begin
  Result := True;

  if HasDotNet6Desktop() then
    exit;

  Answer := MsgBox(
    'snitch.out needs the .NET 6 Desktop Runtime to run, and it was not found.' + #13#10 + #13#10 +
    'Download and install it now? (Setup will close, re-run it afterwards.)',
    mbConfirmation, MB_YESNO);

  if Answer = IDYES then
    ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOW, ewNoWait, ExecCode);

  Result := False;
end;
