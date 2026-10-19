; SmartBP full installer.

#define MyAppName "neo-bpsys-wpf"
#define AppExePath "..\build\neo-bpsys-wpf\neo-bpsys-wpf.exe"
#define MyAppVersion GetVersionNumbersString(AppExePath)
#define AppProductTextVersion GetStringFileInfo(AppExePath, "ProductVersion")
#define MyAppPublisher "PLFJY"
#define MyAppURL "https://bpsys.plfjy.top/"
#define MyAppExeName "neo-bpsys-wpf.exe"
#define BpuiIconName "bpui_icon.ico"

[Setup]
AppId={{842859C0-E6A4-4997-BA10-0933EC09444F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}-{#AppProductTextVersion}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductTextVersion={#AppProductTextVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableWelcomePage=no
DisableReadypage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardImageFile=侧图186x356.bmp
WizardSmallImageFile=顶图54x54.bmp
DisableProgramGroupPage=yes
LicenseFile=License.txt
OutputDir=..\build\
OutputBaseFilename=neo-bpsys-wpf_Installer_full
SetupIconFile=..\neo-bpsys-wpf\favicon.ico
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone

[Files]
Source: "..\build\neo-bpsys-wpf\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; CUDA 依赖和下载缓存归模块管理，完整安装包更新也不能覆盖已下载的文件。
Source: "..\build\neo-bpsys-wpf\*"; DestDir: "{app}"; Excludes: "Runtime\Paddle\cuda,Runtime\Paddle\Dependencies,Runtime\Paddle\Downloads,PaddleRuntimeDownload"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\SmartBpModule\*"; DestDir: "{code:GetSmartBpModuleDir}"; Excludes: "Runtime\Paddle\cuda,Runtime\Paddle\Dependencies,Runtime\Paddle\Downloads,PaddleRuntimeDownload"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

; Dependency installer is included; InitializeSetup skips it only under Wine.
#include "InnoDependencyInstaller\CodeDependencies.iss"

[Code]

var
  SmartBpDirPage: TInputDirWizardPage;
  SmartBpDirPageCreated: Boolean;
  SmartBpModuleDirValue: string;

function TryReadSmartBpModuleRootFromState(var ModuleRoot: string): Boolean; forward;

function IsWine: Boolean;
begin
  Result := RegKeyExists(HKCU, 'Software\Wine') or RegKeyExists(HKLM, 'Software\Wine');
end;

function GetDefaultSmartBpModuleDir(): string;
begin
  Result := ExpandConstant('{localappdata}\neo-bpsys-wpf\Components\SmartBpModule');
end;

function IsSameOrChildPath(Child: string; Parent: string): Boolean;
var
  NormalChild, NormalParent: string;
begin
  NormalChild := Lowercase(AddBackslash(RemoveBackslash(Child)));
  NormalParent := Lowercase(AddBackslash(RemoveBackslash(Parent)));
  Result := (NormalParent <> '\') and (Pos(NormalParent, NormalChild) = 1);
end;

function IsWritableDirectory(Value: string): Boolean;
var
  Full, ProbePath: string;
begin
  Result := False;
  Full := RemoveBackslash(ExpandConstant(Value));
  if not ForceDirectories(Full) then
    exit;

  ProbePath := AddBackslash(Full) + '.smartbp-write-test.tmp';
  if SaveStringToFile(ProbePath, 'test', False) then
  begin
    DeleteFile(ProbePath);
    Result := True;
  end;
end;

function IsUnsafeSmartBpPath(Value: string): Boolean;
var
  Full, Root, ProgramFiles, ProgramFilesX86, WindowsDir, SystemDir, AppDir: string;
begin
  Full := RemoveBackslash(ExpandConstant(Value));
  Root := RemoveBackslash(ExtractFileDrive(Full) + '\');
  ProgramFiles := RemoveBackslash(ExpandConstant('{autopf}'));
  ProgramFilesX86 := RemoveBackslash(ExpandConstant('{commonpf32}'));
  WindowsDir := RemoveBackslash(ExpandConstant('{win}'));
  SystemDir := RemoveBackslash(ExpandConstant('{sys}'));
  AppDir := RemoveBackslash(WizardDirValue);

  Result :=
    (CompareText(Full, Root) = 0) or
    IsSameOrChildPath(Full, ProgramFiles) or
    IsSameOrChildPath(Full, ProgramFilesX86) or
    IsSameOrChildPath(Full, WindowsDir) or
    IsSameOrChildPath(Full, SystemDir) or
    ((IsSameOrChildPath(Full, AppDir)) and (IsSameOrChildPath(AppDir, ProgramFiles) or (not IsWritableDirectory(AppDir)))) or
    (not IsWritableDirectory(Full));
end;

// 软件更新默认沿用宿主记录的模块根目录，保留该位置的 CUDA 依赖。
function GetInitialSmartBpModuleDir(): string;
var
  ExistingRoot: string;
begin
  if TryReadSmartBpModuleRootFromState(ExistingRoot) then
  begin
    if DirExists(ExistingRoot) and (not IsUnsafeSmartBpPath(ExistingRoot)) then
    begin
      Result := ExistingRoot;
      exit;
    end;
  end;

  if RegQueryStringValue(HKCU, 'Software\neo-bpsys-wpf\SmartBpModule', 'ModuleRoot', ExistingRoot) then
  begin
    if DirExists(ExistingRoot) and (not IsUnsafeSmartBpPath(ExistingRoot)) then
    begin
      Result := ExistingRoot;
      exit;
    end;
  end;

  Result := GetDefaultSmartBpModuleDir();
end;

function GetSmartBpModuleDir(Param: string): string;
begin
  if SmartBpDirPageCreated then
    Result := SmartBpDirPage.Values[0]
  else
    Result := SmartBpModuleDirValue;
end;

procedure InitializeWizard();
begin
  Log('InitializeWizard: begin.');

  SmartBpModuleDirValue := GetInitialSmartBpModuleDir();

  if WizardSilent or IsWine then
  begin
    Log('Wine or silent mode detected: skipping license radio pre-check and SmartBP input page creation.');
    SmartBpDirPageCreated := False;
  end
  else
  begin
    WizardForm.LICENSEACCEPTEDRADIO.checked := true;

    SmartBpDirPage := CreateInputDirPage(
      wpSelectDir,
      'SmartBP 模块安装位置',
      '选择 SmartBP 模块安装目录',
      '请选择一个可写的用户目录。不要选择 Program Files、Windows、System32 或磁盘根目录。',
      False,
      '');
    SmartBpDirPage.Add('');
    SmartBpDirPage.Values[0] := SmartBpModuleDirValue;
    SmartBpDirPageCreated := True;
  end;

  Log('InitializeWizard: end. SmartBP module dir=' + GetSmartBpModuleDir(''));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if SmartBpDirPageCreated and (CurPageID = SmartBpDirPage.ID) then
  begin
    if IsUnsafeSmartBpPath(SmartBpDirPage.Values[0]) then
    begin
      MsgBox('该路径不适合安装 SmartBP 模块，请选择可写的用户目录。', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function InitializeSetup: Boolean;
begin
  SmartBpModuleDirValue := GetDefaultSmartBpModuleDir();
  if IsWine then
    Log('Wine environment detected: skipping dependency installation. SmartBP module dir=' + SmartBpModuleDirValue)
  else begin
    Dependency_AddDotNet100Desktop;
    Log('Dependency installation enabled (.NET Desktop Runtime 10.0). SmartBP module dir=' + SmartBpModuleDirValue);
  end;
  Result := True;
end;

function IsUnsafeSmartBpModuleDeletePath(Value: string): Boolean;
var
  Full, Root, Drive, ProgramFiles, ProgramFilesX86, WindowsDir, SystemDir, AppDir, UserAppData: string;
begin
  Full := RemoveBackslash(ExpandConstant(Value));
  Drive := ExtractFileDrive(Full);
  if (Full = '') or (Drive = '') then
  begin
    Result := True;
    exit;
  end;

  Root := RemoveBackslash(Drive + '\');
  ProgramFiles := RemoveBackslash(ExpandConstant('{autopf}'));
  ProgramFilesX86 := RemoveBackslash(ExpandConstant('{commonpf32}'));
  WindowsDir := RemoveBackslash(ExpandConstant('{win}'));
  SystemDir := RemoveBackslash(ExpandConstant('{sys}'));
  AppDir := RemoveBackslash(ExpandConstant('{app}'));
  UserAppData := RemoveBackslash(ExpandConstant('{userappdata}\neo-bpsys-wpf'));

  Result :=
    (CompareText(Full, Root) = 0) or
    (CompareText(Full, AppDir) = 0) or
    (CompareText(Full, UserAppData) = 0) or
    IsSameOrChildPath(Full, ProgramFiles) or
    IsSameOrChildPath(Full, ProgramFilesX86) or
    IsSameOrChildPath(Full, WindowsDir) or
    IsSameOrChildPath(Full, SystemDir);
end;

function ExtractJsonStringValue(Json: string; Name: string): string;
var
  Key, Tail, Ch, Decoded: string;
  KeyPos, ColonPos, QuoteStart, I, UnicodeCode, LowSurrogate: Integer;
  Utf8Bytes: AnsiString;
begin
  Result := '';
  Key := '"' + Name + '"';
  KeyPos := Pos(Key, Json);
  if KeyPos = 0 then
    exit;

  Tail := Copy(Json, KeyPos + Length(Key), Length(Json));
  ColonPos := Pos(':', Tail);
  if ColonPos = 0 then
    exit;

  Tail := Copy(Tail, ColonPos + 1, Length(Tail));
  QuoteStart := Pos('"', Tail);
  if QuoteStart = 0 then
    exit;

  Tail := Copy(Tail, QuoteStart + 1, Length(Tail));
  Decoded := '';
  I := 1;
  while I <= Length(Tail) do
  begin
    Ch := Copy(Tail, I, 1);
    if Ch = '"' then
    begin
      Result := Decoded;
      exit;
    end;
    if Ch = '\' then
    begin
      I := I + 1;
      Ch := Copy(Tail, I, 1);
      case Ch of
        '"', '\', '/': Decoded := Decoded + Ch;
        'b': Decoded := Decoded + #8;
        'f': Decoded := Decoded + #12;
        'n': Decoded := Decoded + #10;
        'r': Decoded := Decoded + #13;
        't': Decoded := Decoded + #9;
        'u':
          begin
            if I + 4 > Length(Tail) then
              exit;
            UnicodeCode := StrToIntDef('$' + Copy(Tail, I + 1, 4), -1);
            if UnicodeCode < 0 then
              exit;
            if (UnicodeCode >= $D800) and (UnicodeCode <= $DBFF) then
            begin
              if (I + 10 > Length(Tail)) or (Copy(Tail, I + 5, 2) <> '\u') then
                exit;
              LowSurrogate := StrToIntDef('$' + Copy(Tail, I + 7, 4), -1);
              if (LowSurrogate < $DC00) or (LowSurrogate > $DFFF) then
                exit;
              UnicodeCode := $10000 + (UnicodeCode - $D800) * $400 + LowSurrogate - $DC00;
              I := I + 6;
            end
            else if (UnicodeCode >= $DC00) and (UnicodeCode <= $DFFF) then
              exit;

            // Pascal Script 的 Char 是单字节；通过明确的 UTF-8 字节构造 Unicode 字符。
            if UnicodeCode < $80 then
              Utf8Bytes := Chr(UnicodeCode)
            else if UnicodeCode < $800 then
              Utf8Bytes := Chr($C0 + UnicodeCode div $40) + Chr($80 + UnicodeCode mod $40)
            else if UnicodeCode < $10000 then
              Utf8Bytes := Chr($E0 + UnicodeCode div $1000) +
                Chr($80 + (UnicodeCode div $40) mod $40) + Chr($80 + UnicodeCode mod $40)
            else
              Utf8Bytes := Chr($F0 + UnicodeCode div $40000) +
                Chr($80 + (UnicodeCode div $1000) mod $40) +
                Chr($80 + (UnicodeCode div $40) mod $40) + Chr($80 + UnicodeCode mod $40);
            Decoded := Decoded + Utf8Decode(Utf8Bytes);
            I := I + 4;
          end;
      else
        exit;
      end;
    end
    else
    begin
      if Ord(Ch[1]) < 32 then
        exit;
      Decoded := Decoded + Ch;
    end;
    I := I + 1;
  end;
end;

function TryReadSmartBpModuleRootFromState(var ModuleRoot: string): Boolean;
var
  StatePath: string;
  StateJson: AnsiString;
begin
  Result := False;
  StatePath := ExpandConstant('{userappdata}\neo-bpsys-wpf\SmartBpModuleState.json');
  if not LoadStringFromFile(StatePath, StateJson) then
    exit;

  ModuleRoot := ExtractJsonStringValue(Utf8Decode(StateJson), 'ModuleRoot');
  Result := ModuleRoot <> '';
end;

procedure DeleteSmartBpModuleDirectory(ModuleRoot: string);
begin
  if ModuleRoot = '' then
    exit;

  if IsUnsafeSmartBpModuleDeletePath(ModuleRoot) then
  begin
    Log('Skipped unsafe SmartBP module directory during uninstall: ' + ModuleRoot);
    exit;
  end;

  if DirExists(ModuleRoot) then
  begin
    Log('Deleting SmartBP module directory during uninstall: ' + ModuleRoot);
    DelTree(ModuleRoot, True, True, True);
  end;
end;

procedure DeleteSmartBpModuleOnUninstall();
var
  ModuleRoot: string;
begin
  if RegQueryStringValue(HKCU, 'Software\neo-bpsys-wpf\SmartBpModule', 'ModuleRoot', ModuleRoot) then
    DeleteSmartBpModuleDirectory(ModuleRoot);

  if TryReadSmartBpModuleRootFromState(ModuleRoot) then
    DeleteSmartBpModuleDirectory(ModuleRoot);

  DeleteSmartBpModuleDirectory(ExpandConstant('{localappdata}\neo-bpsys-wpf\Components\SmartBpModule'));
  RegDeleteValue(HKCU, 'Software\neo-bpsys-wpf\SmartBpModule', 'ModuleRoot');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  StatePath, StateJson, ModuleRootEscaped: string;
begin
  if CurStep = ssPostInstall then
  begin
    StatePath := ExpandConstant('{userappdata}\neo-bpsys-wpf\SmartBpModuleState.json');
    ForceDirectories(ExtractFileDir(StatePath));
    ModuleRootEscaped := GetSmartBpModuleDir('');
    StringChange(ModuleRootEscaped, '\', '\\');
    StateJson :=
      '{' + #13#10 +
      '  "ModuleRoot": "' + ModuleRootEscaped + '",' + #13#10 +
      '  "ModuleVersion": "{#AppProductTextVersion}",' + #13#10 +
      '  "RuntimeAbiVersion": 1,' + #13#10 +
      '  "Rid": "win-x64",' + #13#10 +
      '  "InstallKind": "FullInstaller",' + #13#10 +
      '  "LastLoadedSuccessfully": false,' + #13#10 +
      '  "LegacyOcrModelMigration": { "Completed": false }' + #13#10 +
      '}';
    SaveStringToFile(StatePath, Utf8Encode(StateJson), False);
    RegWriteStringValue(HKCU, 'Software\neo-bpsys-wpf\SmartBpModule', 'ModuleRoot', GetSmartBpModuleDir(''));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  mres: integer;
begin
  case CurUninstallStep of
    usUninstall:
      begin
        DeleteSmartBpModuleOnUninstall();
        mres := MsgBox('是否删除用户数据？(包括日志、自定义UI、自定义设置)', mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
        if mres = IDYES then
          DelTree(ExpandConstant('{userappdata}\neo-bpsys-wpf'), True, True, True);
      end;
  end;
end;

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\neo-bpsys-wpf\SmartBpModule"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\.bpui"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName}.bpui"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui"; ValueType: string; ValueName: ""; ValueData: "BP UI Layout Package"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#BpuiIconName}"
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifdoesntexist
