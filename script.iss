#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef MySourceDir
  #define MySourceDir "dist\\portable\\MirrorDeck"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "dist"
#endif

#define MyAppName "MirrorDeck"
#define MyAppPublisher "MirrorDeck"
#define MyAppPublisherURL "https://github.com/kaktools/mirrordeck"
#define MyAppSupportURL "https://github.com/kaktools/mirrordeck"
#define MyAppUpdatesURL "https://github.com/kaktools/mirrordeck/releases"

#define WindowsAppRuntimeInstallUrl "https://aka.ms/windowsappsdk/1.7/1.7.250401001/windowsappruntimeinstall-x64.exe"
#define BonjourInstallUrl "https://download.info.apple.com/Mac_OS_X/061-8098.20100603.gthyu/BonjourPSSetup.exe"

[Setup]
AppId={{5A9DBA9C-DC4E-44F5-8B10-679CC5B67F32}
AppName={#MyAppName}
AppVerName={#MyAppName} V{#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppUpdatesURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=MirrorDeck Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={autopf}\MirrorDeck
DefaultGroupName=MirrorDeck
DisableProgramGroupPage=yes
SetupIconFile={#MySourceDir}\Assets\MirrorDeck.ico
OutputBaseFilename=MirrorDeck-Setup-{#MyAppVersion}
OutputDir={#MyOutputDir}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UsedUserAreasWarning=no
UninstallDisplayIcon={app}\Assets\MirrorDeck.ico
UninstallDisplayName={#MyAppName} V{#MyAppVersion}
CloseApplications=yes
RestartApplications=no
ForceCloseApplications=yes
CloseApplicationsFilter=MirrorDeck.exe,uxplay.exe,scrcpy.exe,adb.exe

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Symbole:"; Flags: unchecked

[Components]
Name: "core"; Description: "MirrorDeck Kernanwendung"; Types: full compact custom; Flags: fixed
Name: "uxplay"; Description: "AirPlay-Modul (UxPlay)"; Types: full compact custom; Flags: fixed
Name: "scrcpy"; Description: "Android-Modul (scrcpy + adb)"; Types: full compact custom; Flags: fixed
Name: "bonjour"; Description: "Bonjour-Dienst (erforderlich für AirPlay-Erkennung)"; Types: full compact custom; Flags: fixed

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "version.txt"; DestDir: "{app}"; Flags: ignoreversion
#ifexist "vendor\uxplay\dist\uxplay-windows.zip"
Source: "vendor\uxplay\dist\uxplay-windows.zip"; Flags: dontcopy
#endif

[Icons]
Name: "{autoprograms}\MirrorDeck"; Filename: "{app}\MirrorDeck.exe"; IconFilename: "{app}\Assets\MirrorDeck.ico"
Name: "{autodesktop}\MirrorDeck"; Filename: "{app}\MirrorDeck.exe"; IconFilename: "{app}\Assets\MirrorDeck.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\MirrorDeck.exe"; Description: "MirrorDeck starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\MirrorDeck"
Type: filesandordirs; Name: "{localappdata}\MirrorDeck"
Type: filesandordirs; Name: "{commonappdata}\MirrorDeck"
Type: filesandordirs; Name: "{app}"

[Code]
var
  DownloadPage: TDownloadWizardPage;
  InstallWarnings: string;
  ExistingUxPlayInstalled: Boolean;
  ExistingScrcpyInstalled: Boolean;
  ExistingBonjourInstalled: Boolean;
  SkipOptionalComponentsPage: Boolean;
  RunningAppNoticeLabel: TNewStaticText;

function RunPowerShell(const Command: string): Integer;
forward;

function Is64BitInstallMode: Boolean;
begin
  Result := IsWin64;
end;

function FindFileInTree(const RootDir: string; const FileName: string): string;
var
  FindRec: TFindRec;
  Candidate: string;
begin
  Result := '';
  if not DirExists(RootDir) then
    Exit;

  Candidate := AddBackslash(RootDir) + FileName;
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;

  if FindFirst(AddBackslash(RootDir) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            Result := FindFileInTree(AddBackslash(RootDir) + FindRec.Name, FileName);
            if Result <> '' then
              Exit;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function GetDetectionInstallRoot(): string;
begin
  // {app} is not available during early wizard initialization.
  Result := WizardDirValue;
  if Result = '' then
    Result := ExpandConstant('{autopf}\MirrorDeck');
end;

function IsUxPlayAlreadyInstalled(): Boolean;
var
  Root: string;
begin
  Root := GetDetectionInstallRoot();
  Result :=
    (FindFileInTree(AddBackslash(Root) + 'tools\uxplay', 'uxplay.exe') <> '') or
    (FindFileInTree(AddBackslash(Root) + 'tools', 'uxplay.exe') <> '');
end;

function IsScrcpyAlreadyInstalled(): Boolean;
var
  Root: string;
  ScrcpyExe: string;
  AdbExe: string;
begin
  Root := GetDetectionInstallRoot();
  ScrcpyExe := FindFileInTree(AddBackslash(Root) + 'tools\\scrcpy', 'scrcpy.exe');
  if ScrcpyExe = '' then
    ScrcpyExe := FindFileInTree(AddBackslash(Root) + 'tools', 'scrcpy.exe');

  AdbExe := FindFileInTree(AddBackslash(Root) + 'tools\\scrcpy', 'adb.exe');
  if AdbExe = '' then
    AdbExe := FindFileInTree(AddBackslash(Root) + 'tools', 'adb.exe');

  Result := (ScrcpyExe <> '') and (AdbExe <> '');
end;

function IsBonjourAlreadyInstalled(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$svc=Get-Service -Name ''Bonjour Service'',''mDNSResponder'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($svc){ exit 0 } else { exit 3 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

function IsWindowsAppRuntimeInstalled(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$found=$false;' +
    '$roots=@(''HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'',''HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'');' +
    'foreach($r in $roots){' +
      '$item=Get-ItemProperty -Path $r -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match ''(?i)Windows App Runtime'' } | Select-Object -First 1;' +
      'if($item){ $found=$true; break };' +
    '};' +
    'if(-not $found){' +
      '$pkg=Get-AppxPackage -AllUsers -Name ''Microsoft.WindowsAppRuntime.1.7*'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
      'if($pkg){ $found=$true };' +
    '};' +
    'if($found){ exit 0 } else { exit 3 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

procedure DetectExistingOptionalComponents();
var
  UxLabel: string;
  ScrcpyLabel: string;
  BonjourLabel: string;
begin
  ExistingUxPlayInstalled := IsUxPlayAlreadyInstalled();
  ExistingScrcpyInstalled := IsScrcpyAlreadyInstalled();
  ExistingBonjourInstalled := IsBonjourAlreadyInstalled();
  SkipOptionalComponentsPage := ExistingUxPlayInstalled and ExistingScrcpyInstalled and ExistingBonjourInstalled;

  if ExistingUxPlayInstalled then UxLabel := 'true' else UxLabel := 'false';
  if ExistingScrcpyInstalled then ScrcpyLabel := 'true' else ScrcpyLabel := 'false';
  if ExistingBonjourInstalled then BonjourLabel := 'true' else BonjourLabel := 'false';

  Log(
    'Detected optional components: UxPlay=' + UxLabel +
    ', scrcpy=' + ScrcpyLabel +
    ', Bonjour=' + BonjourLabel);
end;

procedure ApplyDefaultComponentSelection();
var
  Selected: string;
begin
  // Always install all MirrorDeck modules.
  Selected := 'core,uxplay,scrcpy,bonjour';

  WizardSelectComponents(Selected);
end;

function IsMirrorDeckRunning(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$p=Get-Process -Name ''MirrorDeck'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($p){ exit 0 } else { exit 3 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

function AreMirrorDeckRuntimeProcessesRunning(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$targets=@(''MirrorDeck'',''uxplay'',''scrcpy'',''adb'');' +
    '$p=Get-Process -Name $targets -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($p){ exit 0 } else { exit 3 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

function StopMirrorDeckRuntimeProcesses(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$targets=@(''MirrorDeck'',''uxplay'',''scrcpy'',''adb'');' +
    'foreach($n in $targets){ Get-Process -Name $n -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue };' +
    'Start-Sleep -Milliseconds 700;' +
    '$left=Get-Process -Name $targets -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($left){ exit 3 } else { exit 0 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

function EnsureRuntimeProcessesClosedForInstall(const AskUser: Boolean): Boolean;
var
  Decision: Integer;
begin
  Result := True;

  if not AreMirrorDeckRuntimeProcessesRunning() then
    Exit;

  if AskUser and (not WizardSilent) then
  begin
    Decision := MsgBox(
      'MirrorDeck oder zugehörige Prozesse sind noch aktiv.' + #13#10 +
      'Soll der Installer diese jetzt automatisch schließen?',
      mbConfirmation,
      MB_YESNO
    );

    if Decision <> IDYES then
    begin
      MsgBox(
        'Bitte MirrorDeck vollständig schließen und danach erneut auf "Weiter" klicken.',
        mbInformation,
        MB_OK
      );
      Result := False;
      Exit;
    end;
  end;

  if not StopMirrorDeckRuntimeProcesses() then
  begin
    MsgBox(
      'MirrorDeck konnte nicht automatisch beendet werden.' + #13#10 +
      'Bitte die App und ggf. uxplay/scrcpy/adb manuell schließen und erneut versuchen.',
      mbError,
      MB_OK
    );
    Result := False;
  end;
end;

procedure UpdateReadyPageNotices();
begin
  if Assigned(RunningAppNoticeLabel) then
  begin
    if AreMirrorDeckRuntimeProcessesRunning() then
    begin
      RunningAppNoticeLabel.Caption :=
        'MirrorDeck oder zugehörige Prozesse sind noch aktiv. Beim Klick auf "Weiter" kann der Installer sie automatisch schließen.';
      RunningAppNoticeLabel.Visible := True;
    end
    else
    begin
      RunningAppNoticeLabel.Visible := False;
    end;
  end;
end;

procedure InitializeWizard();
begin
  DetectExistingOptionalComponents();
  ApplyDefaultComponentSelection();

  RunningAppNoticeLabel := TNewStaticText.Create(WizardForm);
  RunningAppNoticeLabel.Parent := WizardForm.ReadyPage;
  RunningAppNoticeLabel.Left := WizardForm.ReadyMemo.Left;
  RunningAppNoticeLabel.Top := WizardForm.ReadyMemo.Top + WizardForm.ReadyMemo.Height + ScaleY(8);
  RunningAppNoticeLabel.Width := WizardForm.ReadyMemo.Width;
  RunningAppNoticeLabel.Height := ScaleY(34);
  RunningAppNoticeLabel.AutoSize := False;
  RunningAppNoticeLabel.WordWrap := True;
  RunningAppNoticeLabel.Font.Style := [fsBold];
  RunningAppNoticeLabel.Caption := '';
  RunningAppNoticeLabel.Visible := False;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result :=
    MemoDirInfo + NewLine + NewLine +
    MemoTasksInfo + NewLine + NewLine +
    'Hinweis: MirrorDeck installiert alle Module standardmäßig.' + NewLine +
    'Wenn Laufzeitabhängigkeiten nachgeladen werden müssen, wird eine Internetverbindung benötigt.';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = wpSelectComponents then
    Result := True;
end;

function DownloadToTemp(const Url: string; const TargetName: string): string;
begin
  if not Assigned(DownloadPage) then
    DownloadPage := CreateDownloadPage('Abhängigkeiten herunterladen', 'MirrorDeck lädt bei Bedarf Laufzeitabhängigkeiten aus offiziellen Quellen herunter.', nil);

  DownloadPage.Clear;
  DownloadPage.Add(Url, TargetName, '');
  DownloadPage.Show;
  try
    DownloadPage.Download;
  finally
    DownloadPage.Hide;
  end;

  Result := ExpandConstant('{tmp}\') + TargetName;
end;

function RunInstaller(const InstallerPath: string; const Arguments: string): Integer;
var
  ResultCode: Integer;
begin
  if not FileExists(InstallerPath) then
  begin
    Result := -10;
    Exit;
  end;

  if Exec(InstallerPath, Arguments, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
    Result := -1;
end;

function RunPowerShell(const Command: string): Integer;
var
  ResultCode: Integer;
begin
  if Exec('powershell.exe', '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Command + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode
  else
    Result := -1;
end;

procedure ConfigureScrcpyPathTask();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''Stop'';' +
    '$appRoot=''' + ExpandConstant('{app}') + ''';' +
    '$scrcpyExe=Get-ChildItem -Path (Join-Path $appRoot ''tools\scrcpy'') -Filter ''scrcpy.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if(-not $scrcpyExe){ exit 2 };' +
    '$dir=$scrcpyExe.DirectoryName;' +
    '$envKey=''HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment'';' +
    '$path=(Get-ItemProperty -Path $envKey -Name Path -ErrorAction SilentlyContinue).Path;' +
    'if(-not $path){ $path='''' };' +
    '$parts=@($path -split '';'' | Where-Object { $_ -and $_.Trim() -ne '''' });' +
    '$already=$false; foreach($p in $parts){ if($p.TrimEnd(''\'') -ieq $dir.TrimEnd(''\'')){ $already=$true; break } };' +
    'if(-not $already){ $newPath=($parts + $dir) -join '';''; Set-ItemProperty -Path $envKey -Name Path -Value $newPath; [Environment]::SetEnvironmentVariable(''Path'', $newPath, ''Machine'') };' +
    'New-Item -Path ''HKLM:\Software\MirrorDeck'' -Force | Out-Null;' +
    'Set-ItemProperty -Path ''HKLM:\Software\MirrorDeck'' -Name ''ScrcpyPathDir'' -Value $dir -Type String;';

  Rc := RunPowerShell(Cmd);
  Log('scrcpy PATH auto-config RC=' + IntToStr(Rc));

  if (Rc <> 0) and (Rc <> 2) then
    InstallWarnings := InstallWarnings + '- Das scrcpy/adb-Verzeichnis konnte nicht zum System-PATH hinzugefügt werden.' + #13#10;
end;

procedure CleanupScrcpyPathOnUninstall();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$markerKey=''HKLM:\Software\MirrorDeck'';' +
    '$dir=(Get-ItemProperty -Path $markerKey -Name ScrcpyPathDir -ErrorAction SilentlyContinue).ScrcpyPathDir;' +
    'if(-not $dir){ exit 0 };' +
    '$envKey=''HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment'';' +
    '$path=(Get-ItemProperty -Path $envKey -Name Path -ErrorAction SilentlyContinue).Path;' +
    'if($path){' +
      '$parts=@($path -split '';'' | Where-Object { $_ -and $_.Trim() -ne '''' });' +
      '$filtered=@(); foreach($p in $parts){ if($p.TrimEnd(''\'') -ine $dir.TrimEnd(''\'')){ $filtered += $p } };' +
      '$newPath=($filtered -join '';'' );' +
      'Set-ItemProperty -Path $envKey -Name Path -Value $newPath;' +
      '[Environment]::SetEnvironmentVariable(''Path'', $newPath, ''Machine'');' +
    '};' +
    'Remove-ItemProperty -Path $markerKey -Name ScrcpyPathDir -ErrorAction SilentlyContinue;';

  Rc := RunPowerShell(Cmd);
  Log('scrcpy PATH cleanup RC=' + IntToStr(Rc));
end;

procedure StopRuntimeProcessesForUninstall();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$targets=@(''MirrorDeck'',''uxplay'',''scrcpy'',''adb'');' +
    'foreach($n in $targets){ Get-Process -Name $n -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue };';

  Rc := RunPowerShell(Cmd);
  Log('uninstall process stop RC=' + IntToStr(Rc));
end;

procedure RemoveMirrorDeckFirewallRules();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$appRoot=''' + ExpandConstant('{app}') + ''';' +
    '$rulesByName=Get-NetFirewallRule -DisplayName ''MirrorDeck-*'' -ErrorAction SilentlyContinue;' +
    'if($rulesByName){ $rulesByName | Remove-NetFirewallRule -ErrorAction SilentlyContinue };' +
    '$programs=@();' +
    '$mainExe=Join-Path $appRoot ''MirrorDeck.exe'';' +
    'if(Test-Path $mainExe){ $programs += $mainExe };' +
    '$ux=Get-ChildItem -Path (Join-Path $appRoot ''tools\uxplay'') -Filter ''uxplay.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName;' +
    '$sc=Get-ChildItem -Path (Join-Path $appRoot ''tools\scrcpy'') -Filter ''scrcpy.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName;' +
    '$adb=Get-ChildItem -Path (Join-Path $appRoot ''tools\scrcpy'') -Filter ''adb.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName;' +
    'if($ux){ $programs += $ux }; if($sc){ $programs += $sc }; if($adb){ $programs += $adb };' +
    '$programs=@($programs | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique);' +
    'foreach($p in $programs){' +
      '$filters=Get-NetFirewallApplicationFilter -Program $p -ErrorAction SilentlyContinue;' +
      'if($filters){ $filters | Get-NetFirewallRule -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue };' +
    '};';

  Rc := RunPowerShell(Cmd);
  Log('uninstall firewall cleanup RC=' + IntToStr(Rc));
end;

procedure CleanupMirrorDeckRegistry();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    'if(Test-Path ''HKLM:\Software\MirrorDeck''){ Remove-Item ''HKLM:\Software\MirrorDeck'' -Recurse -Force -ErrorAction SilentlyContinue };';

  Rc := RunPowerShell(Cmd);
  Log('uninstall registry cleanup RC=' + IntToStr(Rc));
end;

procedure UninstallBonjourOptional();
var
  Cmd: string;
  Rc: Integer;
begin
  if UninstallSilent then
  begin
    Log('Bonjour uninstall prompt skipped in silent uninstall mode.');
    Exit;
  end;

  if MsgBox(
      'Bonjour-Dienst ebenfalls deinstallieren?' + #13#10 +
      '(Ja = komplett bereinigen, Nein = Bonjour für andere Apps behalten.)',
      mbConfirmation,
      MB_YESNO) <> IDYES then
  begin
    Log('Bonjour uninstall skipped by user choice.');
    Exit;
  end;

  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$u=$null;' +
    '$roots=@(''HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'',''HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'');' +
    'foreach($r in $roots){' +
      '$c=Get-ItemProperty $r -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match ''(?i)bonjour'' } | Select-Object -First 1;' +
      'if($c){ $u=$c.UninstallString; break };' +
    '};' +
    'if($u){' +
      '$p=$null;' +
      'if($u -match ''(?i)msiexec''){' +
        '$guid=($u -replace ''.*?(\{[^}]+\}).*'',''$1'');' +
        'if($guid -and $guid -match ''\{[^}]+\}''){ $p=Start-Process -FilePath ''msiexec.exe'' -ArgumentList ''/x '' + $guid + '' /qn /norestart'' -PassThru -WindowStyle Hidden -ErrorAction SilentlyContinue };' +
      '}else{' +
        '$p=Start-Process -FilePath ''cmd.exe'' -ArgumentList ''/c '',$u,'' /quiet /norestart'' -PassThru -WindowStyle Hidden -ErrorAction SilentlyContinue;' +
      '};' +
      'if($p){ if(-not $p.WaitForExit(90000)){ try{ $p.Kill() } catch{} } };' +
    '};' +
    '$svc=Get-Service -Name ''Bonjour Service'',''mDNSResponder'' -ErrorAction SilentlyContinue;' +
    'if($svc){' +
      'foreach($s in $svc){ Stop-Service -Name $s.Name -Force -ErrorAction SilentlyContinue; sc.exe delete $s.Name | Out-Null }' +
    '};';

  Rc := RunPowerShell(Cmd);
  Log('bonjour uninstall optional RC=' + IntToStr(Rc));
end;

procedure CleanupInstallRootOnUninstall();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$appRoot=''' + ExpandConstant('{app}') + ''';' +
    'if(Test-Path $appRoot){ Remove-Item -Path $appRoot -Recurse -Force -ErrorAction SilentlyContinue };' +
    '$parent=Split-Path -Path $appRoot -Parent;' +
    'if(Test-Path $parent){' +
      '$remaining=Get-ChildItem -Path $parent -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq ''MirrorDeck'' };' +
      'if($remaining){ Remove-Item -Path $appRoot -Recurse -Force -ErrorAction SilentlyContinue };' +
    '};';

  Rc := RunPowerShell(Cmd);
  Log('install root cleanup RC=' + IntToStr(Rc));
end;

function DownloadLatestGitHubAssetSmart(const Repo: string; const TargetName: string): string;
var
  Cmd: string;
begin
  Cmd :=
    '$repo=''' + Repo + ''';' +
    '$target=''' + ExpandConstant('{tmp}\') + TargetName + ''';' +
    '$h=@{''User-Agent''=''MirrorDeck-Installer''};' +
    '$api=''https://api.github.com/repos/''+$repo;' +
    '$arch1=($env:PROCESSOR_ARCHITECTURE + '''').ToLowerInvariant();' +
    '$arch2=($env:PROCESSOR_ARCHITEW6432 + '''').ToLowerInvariant();' +
    '$isArm64=($arch1 -eq ''arm64'' -or $arch2 -eq ''arm64'');' +
    '$exclude=@(''(?i)sha256'',''(?i)checksums?'',''(?i)signature|sig'',''(?i)source|src'',''(?i)debug|symbols?'');' +
    '$generic=@(''(?i)\.zip$'',''(?i)\.(zip|7z)$'');' +
    '$patterns=@();' +
    'if($repo -eq ''FDH2/UxPlay''){' +
      'if($isArm64){$patterns+=@(''(?i)uxplay.*(windows|win).*(arm64|aarch64).*\.(zip|7z)$'',''(?i)(windows|win).*(arm64|aarch64).*\.(zip|7z)$'');};' +
      '$patterns+=@(''(?i)uxplay.*(windows|win).*(x64|amd64).*\.(zip|7z)$'',''(?i)uxplay.*(windows|win).*\.(zip|7z)$'',''(?i)(windows|win).*(x64|amd64).*\.(zip|7z)$'');' +
    '}elseif($repo -eq ''Genymobile/scrcpy''){' +
      'if($isArm64){$patterns+=@(''(?i)scrcpy.*(arm64|aarch64).*win.*\.zip$'',''(?i)scrcpy.*win.*(arm64|aarch64).*\.zip$'');};' +
      '$patterns+=@(''(?i)scrcpy.*win64.*\.zip$'',''(?i)scrcpy.*(windows|win).*(x64|amd64).*\.zip$'',''(?i)scrcpy.*win.*\.zip$'',''(?i)win.*\.zip$'');' +
    '}else{' +
      '$patterns+=@(''(?i)(windows|win).*(x64|amd64).*\.(zip|7z)$'',''(?i)(windows|win).*\.(zip|7z)$'');' +
    '};' +
    '$patterns+=$generic;' +
    '$releases=@();' +
    'try{$latest=Invoke-RestMethod -Headers $h -Uri ($api+''/releases/latest''); if($latest){$releases+=@($latest);}}catch{};' +
    'try{$many=Invoke-RestMethod -Headers $h -Uri ($api+''/releases?per_page=8''); if($many){$releases+=@($many);}}catch{};' +
    'if(-not $releases -or $releases.Count -eq 0){ exit 2 };' +
    '$selected=$null;' +
    'foreach($pat in $patterns){' +
      'foreach($rel in $releases){' +
        '$assets=@($rel.assets | Where-Object {' +
          '$n=$_.name; if(-not $n -or -not $_.browser_download_url){ return $false }; $bad=$false; foreach($ex in $exclude){ if($n -match $ex){ $bad=$true; break } }; (-not $bad)' +
        '}) | Sort-Object name;' +
        '$match=$assets | Where-Object { $_.name -match $pat } | Select-Object -First 1;' +
        'if($match){ $selected=$match; break };' +
      '};' +
      'if($selected){ break };' +
    '};' +
    'if(-not $selected){' +
      'foreach($rel in $releases){' +
        '$assets=@($rel.assets | Where-Object {' +
          '$n=$_.name; if(-not $n -or -not $_.browser_download_url){ return $false }; $bad=$false; foreach($ex in $exclude){ if($n -match $ex){ $bad=$true; break } }; (-not $bad)' +
        '}) | Sort-Object name;' +
        '$fallback=$assets | Where-Object { $_.name -match ''(?i)\.zip$'' } | Select-Object -First 1;' +
        'if($fallback){ $selected=$fallback; break };' +
      '};' +
    '};' +
    'if(-not $selected){ exit 3 };' +
    'Invoke-WebRequest -Headers $h -Uri $selected.browser_download_url -OutFile $target;';

  if RunPowerShell(Cmd) <> 0 then
    Result := ''
  else
    Result := ExpandConstant('{tmp}\') + TargetName;
end;

function EnsureBonjourInstalledFromInternet(): Boolean;
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$ok=$false;' +
    '$svc=Get-Service -Name ''Bonjour Service'',''mDNSResponder'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($svc){ $ok=$true };' +
    'if(-not $ok){' +
      '$tmp=Join-Path $env:TEMP ''BonjourPSSetup.exe'';' +
      'try{' +
        'Invoke-WebRequest -Uri ''' + '{#BonjourInstallUrl}' + ''' -OutFile $tmp;' +
        'if(Test-Path $tmp){ Start-Process -FilePath $tmp -ArgumentList ''/quiet /norestart'' -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue };' +
      '}catch{};' +
      'Start-Sleep -Seconds 2;' +
      '$svc=Get-Service -Name ''Bonjour Service'',''mDNSResponder'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
      'if($svc){ $ok=$true };' +
    '};' +
    'if(-not $ok){' +
      '$winget=Get-Command winget -ErrorAction SilentlyContinue;' +
      'if($winget){' +
        'try{' +
          '& winget install --id Apple.Bonjour --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity;' +
        '}catch{};' +
        'Start-Sleep -Seconds 2;' +
        '$svc=Get-Service -Name ''Bonjour Service'',''mDNSResponder'' -ErrorAction SilentlyContinue | Select-Object -First 1;' +
        'if($svc){ $ok=$true };' +
      '};' +
    '};' +
    'if($ok){ exit 0 } else { exit 3 };';

  Rc := RunPowerShell(Cmd);
  Result := (Rc = 0);
end;

procedure ExtractZipToApp(const ZipPath: string; const RelativeTarget: string);
var
  Cmd: string;
begin
  Cmd :=
    '$zip=''' + ZipPath + ''';' +
    '$target=''' + ExpandConstant('{app}\') + RelativeTarget + ''';' +
    'New-Item -ItemType Directory -Path $target -Force | Out-Null;' +
    'Expand-Archive -Path $zip -DestinationPath $target -Force;';

  if RunPowerShell(Cmd) <> 0 then
    Log('Failed to extract archive: ' + ZipPath);
end;

function BoolJson(const Value: Boolean): string;
begin
  if Value then
    Result := 'true'
  else
    Result := 'false';
end;

function TryGetBundledUxPlayZip(var ZipPath: string): Boolean;
begin
  Result := False;
  ZipPath := '';

  try
    ExtractTemporaryFile('uxplay-windows.zip');
    ZipPath := ExpandConstant('{tmp}\uxplay-windows.zip');
    Result := FileExists(ZipPath);
    if Result then
      Log('Using bundled local UxPlay archive from installer payload: ' + ZipPath);
  except
    Log('No bundled local UxPlay archive found in installer payload.');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpReady then
  begin
    UpdateReadyPageNotices();
    if not EnsureRuntimeProcessesClosedForInstall(True) then
    begin
      Result := False;
      Exit;
    end;
  end;

  if CurPageID = wpSelectComponents then
  begin
    if WizardIsComponentSelected('uxplay') and (not WizardIsComponentSelected('bonjour')) and (not ExistingBonjourInstalled) then
    begin
      Result := (MsgBox(
        'AirPlay/UxPlay wurde ohne Bonjour ausgewählt.' + #13#10 +
        'Die AirPlay-Erkennung funktioniert ggf. erst nach Bonjour-Installation.' + #13#10 +
        'Trotzdem fortfahren?',
        mbConfirmation,
        MB_YESNO
      ) = IDYES);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if not EnsureRuntimeProcessesClosedForInstall(False) then
    Result := 'MirrorDeck oder zugehörige Prozesse laufen noch. Bitte diese Prozesse schließen und das Setup erneut starten.';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
    UpdateReadyPageNotices();
end;

procedure SaveInstallSelection();
var
  Json: string;
  Core, Ux, Sc, Bon: string;
begin
  Core := 'true';
  Ux := 'true';
  Sc := 'true';
  Bon := 'true';

  Json :=
    '{' +
    '"installedByInstaller":true,' +
    '"installSelection":{' +
    '"core":' + Core + ',' +
    '"uxplay":' + Ux + ',' +
    '"scrcpy":' + Sc + ',' +
    '"bonjour":' + Bon +
    '}}';

  SaveStringToFile(ExpandConstant('{app}\install-selection.json'), Json, False);
end;

procedure DownloadAndInstallSelectedComponents();
var
  UxZip, ScZip, RuntimeInstaller: string;
  Rc: Integer;
  UxExe, ScrcpyExe, AdbExe: string;
  BonjourOk: Boolean;
begin
  InstallWarnings := '';

  if not IsWindowsAppRuntimeInstalled() then
  begin
    RuntimeInstaller := DownloadToTemp('{#WindowsAppRuntimeInstallUrl}', 'windowsappruntimeinstall-x64.exe');
    if RuntimeInstaller <> '' then
    begin
      Rc := RunInstaller(RuntimeInstaller, '/quiet /norestart');
      Log('Windows App Runtime install RC=' + IntToStr(Rc));
    end
    else
    begin
      Log('Windows App Runtime download failed. App may not start if runtime is missing.');
      InstallWarnings := InstallWarnings + '- Windows App Runtime konnte nicht heruntergeladen werden.' + #13#10;
    end;
  end
  else
    Log('Windows App Runtime already installed. Runtime download skipped.');

  if not ExistingUxPlayInstalled then
  begin
    if not TryGetBundledUxPlayZip(UxZip) then
      UxZip := DownloadLatestGitHubAssetSmart('FDH2/UxPlay', 'uxplay-latest.zip');

    if UxZip <> '' then
    begin
      ExtractZipToApp(UxZip, 'tools\uxplay');
      UxExe := FindFileInTree(ExpandConstant('{app}\tools\uxplay'), 'uxplay.exe');
      if UxExe = '' then
      begin
        Log('UxPlay archive extracted but uxplay.exe was not found under tools\\uxplay.');
        InstallWarnings := InstallWarnings + '- UxPlay-Download/-Entpacken abgeschlossen, aber uxplay.exe wurde im Installationsverzeichnis nicht gefunden.' + #13#10;
      end
      else
      begin
        Log('UxPlay installed at: ' + UxExe);
      end;
    end
    else
    begin
      Log('UxPlay retrieval failed. Neither bundled local ZIP nor FDH2/UxPlay Windows release asset was found.');
      InstallWarnings := InstallWarnings + '- UxPlay ist ausgewählt, aber weder ein lokales ZIP noch ein Windows-Release-Asset wurde gefunden.' + #13#10;
    end;
  end;

  if not ExistingScrcpyInstalled then
  begin
    ScZip := DownloadLatestGitHubAssetSmart('Genymobile/scrcpy', 'scrcpy-latest.zip');
    if ScZip <> '' then
    begin
      ExtractZipToApp(ScZip, 'tools\scrcpy');
      ScrcpyExe := FindFileInTree(ExpandConstant('{app}\tools\scrcpy'), 'scrcpy.exe');
      AdbExe := FindFileInTree(ExpandConstant('{app}\tools\scrcpy'), 'adb.exe');
      if ScrcpyExe = '' then
      begin
        Log('scrcpy archive extracted but scrcpy.exe was not found under tools\\scrcpy.');
        InstallWarnings := InstallWarnings + '- scrcpy-Download/-Entpacken abgeschlossen, aber scrcpy.exe wurde im Installationsverzeichnis nicht gefunden.' + #13#10;
      end
      else
      begin
        Log('scrcpy installed at: ' + ScrcpyExe);
      end;

      if AdbExe = '' then
      begin
        Log('scrcpy archive extracted but adb.exe was not found under tools\\scrcpy.');
        InstallWarnings := InstallWarnings + '- scrcpy wurde installiert, aber adb.exe wurde im Installationsverzeichnis nicht gefunden.' + #13#10;
      end
      else
      begin
        Log('adb installed at: ' + AdbExe);
      end;
    end
    else
    begin
      Log('scrcpy download skipped or failed.');
      InstallWarnings := InstallWarnings + '- scrcpy ist ausgewählt, aber der Download ist fehlgeschlagen.' + #13#10;
    end;
  end;

  if not ExistingBonjourInstalled then
  begin
    BonjourOk := EnsureBonjourInstalledFromInternet();
    if BonjourOk then
      Log('Bonjour was installed/verified successfully via internet flow.')
    else
    begin
      Log('Bonjour installation from internet failed.');
      InstallWarnings := InstallWarnings + '- Bonjour konnte nicht automatisch aus dem Internet installiert werden.' + #13#10;
    end;
  end;
end;

procedure EnsureFirewallRules();
var
  Cmd: string;
  Rc: Integer;
begin
  Cmd :=
    '$ErrorActionPreference=''SilentlyContinue'';' +
    '$appRoot=''' + ExpandConstant('{app}') + ''';' +
    'function Add-RuleIfMissing($name,$program,$protocol){' +
      'if(-not (Test-Path $program)){ return };' +
      '$existing=Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue;' +
      'if($null -eq $existing){ New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Program $program -Profile Any -Protocol $protocol | Out-Null };' +
    '};' +
    '$mainExe=Join-Path $appRoot ''MirrorDeck.exe'';' +
    'Add-RuleIfMissing ''MirrorDeck-App-TCP'' $mainExe ''TCP'';' +
    'Add-RuleIfMissing ''MirrorDeck-App-UDP'' $mainExe ''UDP'';' +
    '$uxExe=Get-ChildItem -Path (Join-Path $appRoot ''tools\uxplay'') -Filter ''uxplay.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($uxExe){ Add-RuleIfMissing ''MirrorDeck-UxPlay-TCP'' $uxExe.FullName ''TCP''; Add-RuleIfMissing ''MirrorDeck-UxPlay-UDP'' $uxExe.FullName ''UDP'' };' +
    '$scrcpyExe=Get-ChildItem -Path (Join-Path $appRoot ''tools\scrcpy'') -Filter ''scrcpy.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($scrcpyExe){ Add-RuleIfMissing ''MirrorDeck-scrcpy-TCP'' $scrcpyExe.FullName ''TCP''; Add-RuleIfMissing ''MirrorDeck-scrcpy-UDP'' $scrcpyExe.FullName ''UDP'' };' +
    '$adbExe=Get-ChildItem -Path (Join-Path $appRoot ''tools\scrcpy'') -Filter ''adb.exe'' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1;' +
    'if($adbExe){ Add-RuleIfMissing ''MirrorDeck-adb-TCP'' $adbExe.FullName ''TCP''; Add-RuleIfMissing ''MirrorDeck-adb-UDP'' $adbExe.FullName ''UDP'' };';

  Rc := RunPowerShell(Cmd);
  Log('Firewall rules check/apply RC=' + IntToStr(Rc));

  if Rc <> 0 then
    InstallWarnings := InstallWarnings + '- Firewall-Regeln konnten nicht vollständig geprüft/gesetzt werden.' + #13#10;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    DownloadAndInstallSelectedComponents();
    ConfigureScrcpyPathTask();
    EnsureFirewallRules();
    SaveInstallSelection();

    if InstallWarnings <> '' then
    begin
      MsgBox(
        'Einige ausgewählte Komponenten konnten nicht installiert werden:' + #13#10 + #13#10 +
        InstallWarnings + #13#10 +
        'MirrorDeck startet trotzdem. Fehlende Komponenten können später im Setup-Assistenten installiert werden.',
        mbInformation,
        MB_OK
      );
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopRuntimeProcessesForUninstall();
    CleanupScrcpyPathOnUninstall();
    RemoveMirrorDeckFirewallRules();
    CleanupMirrorDeckRegistry();
    UninstallBonjourOptional();
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    CleanupInstallRootOnUninstall();
  end;
end;
