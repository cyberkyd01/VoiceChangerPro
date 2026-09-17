; Inno Setup 6 script for Voice Changer Pro
; Build:  ISCC.exe installer\VoiceChangerPro.iss   (or .\build.ps1 -Installer)
; Input:  dist\VoiceChangerPro.exe (self-contained build, produced by build.ps1 -Publish)
; Output: installer\Output\VoiceChangerPro-Setup-<version>.exe

#define MyAppName "Voice Changer Pro by Cyberkyd"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Cyberkyd"
#define MyAppURL "https://github.com/cyberkyd01/VoiceChangerPro"
#define MyAppExeName "VoiceChangerPro.exe"
#define MyAppId "{{7E1C0D6A-4B3F-4A0E-9C55-2F0C6B1D8E21}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; Per-user install by default (no administrator needed); the user may choose "all users" in the dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename=VoiceChangerPro-Setup-{#MyAppVersion}
SetupIconFile=..\src\VoiceChanger.App\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
WizardSizePercent=110
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no
UsePreviousTasks=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "vbcable"; Description: "Download and install &VB-CABLE (free virtual audio cable, required so other apps can hear your changed voice)"; GroupDescription: "Dependencies:"; Check: not VbCableInstalled
Name: "launch"; Description: "&Start Voice Changer Pro when setup finishes"; GroupDescription: "After installation:"

[Files]
Source: "..\dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Real-time voice changer"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName}"; Flags: nowait postinstall skipifsilent; Tasks: launch

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\VoiceChangerPro\logs"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function VbCableInstalled: Boolean;
var
  Keys: TArrayOfString;
  I: Integer;
  Name: String;
  Root: Integer;
  Base: String;
begin
  Result := False;
  Base := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  for Root := 0 to 1 do
  begin
    if Root = 0 then Root := HKLM else Root := HKCU;
    if RegGetSubkeyNames(Root, Base, Keys) then
      for I := 0 to GetArrayLength(Keys) - 1 do
        if RegQueryStringValue(Root, Base + '\' + Keys[I], 'DisplayName', Name) then
          if (Pos('VBCABLE', Uppercase(Name)) > 0) or (Pos('VB-AUDIO VIRTUAL CABLE', Uppercase(Name)) > 0) then
          begin
            Result := True;
            Exit;
          end;
  end;
  { also accept the driver file itself }
  if FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win7.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win10.sys')) then
    Result := True;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Downloaded %s', [FileName]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

function TryDownload(const Url: String): Boolean;
begin
  Result := False;
  DownloadPage.Clear;
  DownloadPage.Add(Url, 'vbcable.zip', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      Result := True;
    except
      Log('Download failed: ' + Url + ' - ' + GetExceptionMessage);
    end;
  finally
    DownloadPage.Hide;
  end;
end;

procedure InstallVbCable;
var
  ResultCode: Integer;
  ZipPath, ExtractDir, SetupExe, PsCmd: String;
  Ok: Boolean;
begin
  Ok := TryDownload('https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip');
  if not Ok then Ok := TryDownload('https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack43.zip');
  if not Ok then
  begin
    MsgBox('VB-CABLE could not be downloaded automatically.' + #13#10#13#10 +
           'Please download it from https://vb-audio.com/Cable/ , run VBCABLE_Setup_x64.exe as administrator and reboot.' + #13#10 +
           'Voice Changer Pro works without it, but other applications will not hear your changed voice until it is installed.',
           mbInformation, MB_OK);
    Exit;
  end;

  ZipPath := ExpandConstant('{tmp}\vbcable.zip');
  ExtractDir := ExpandConstant('{tmp}\vbcable');
  PsCmd := '-NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath ''' + ZipPath + ''' -DestinationPath ''' + ExtractDir + ''' -Force"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), PsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    MsgBox('VB-CABLE package could not be extracted. Please install it manually from https://vb-audio.com/Cable/', mbError, MB_OK);
    Exit;
  end;

  SetupExe := ExtractDir + '\VBCABLE_Setup_x64.exe';
  if not FileExists(SetupExe) then
  begin
    MsgBox('VBCABLE_Setup_x64.exe was not found in the downloaded package. Please install VB-CABLE manually from https://vb-audio.com/Cable/', mbError, MB_OK);
    Exit;
  end;

  MsgBox('The VB-CABLE driver installer will now open. It needs administrator rights: click "Install Driver" in its window, then close it.' + #13#10#13#10 +
         'Windows may ask you to restart once so the virtual cable becomes available.', mbInformation, MB_OK);
  { driver installers must run elevated; use the runas verb (UAC prompt) }
  if not ShellExec('runas', SetupExe, '', ExtractDir, SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
    MsgBox('The VB-CABLE installer could not be started (' + IntToStr(ResultCode) + '). You can run it later from https://vb-audio.com/Cable/', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    { make sure a running instance does not lock the executable }
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end
  else if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('vbcable') then
      InstallVbCable;
  end;
end;
