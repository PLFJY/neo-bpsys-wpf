; SmartBP full installer.

#include "CodeDependencies.iss"
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
Source: "..\build\neo-bpsys-wpf\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\SmartBpModule\*"; DestDir: "{code:GetSmartBpModuleDir}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Code]
var
  SmartBpDirPage: TInputDirWizardPage;

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

function GetSmartBpModuleDir(Param: string): string;
begin
  Result := SmartBpDirPage.Values[0];
end;

procedure InitializeWizard();
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
  SmartBpDirPage.Values[0] := ExpandConstant('{localappdata}\neo-bpsys-wpf\Components\SmartBpModule');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = SmartBpDirPage.ID then
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
  Dependency_AddDotNet90Desktop;
  Result := True;
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
    SaveStringToFile(StatePath, StateJson, False);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  mres: integer;
begin
  case CurUninstallStep of
    usUninstall:
      begin
        mres := MsgBox('是否删除用户数据？(包括日志、自定义UI、自定义设置)', mbConfirmation, MB_YESNO or MB_DEFBUTTON2)
        if mres = IDYES then
          DelTree(ExpandConstant('{userappdata}\neo-bpsys-wpf'), True, True, True);
      end;
  end;
end;

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.bpui"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName}.bpui"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui"; ValueType: string; ValueName: ""; ValueData: "BP UI Layout Package"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#BpuiIconName}"
Root: HKCU; Subkey: "Software\Classes\{#MyAppName}.bpui\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifdoesntexist
