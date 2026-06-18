;汉化:MonKeyDu 
;由 Inno Setup 脚本向导 生成的脚本,有关创建 INNO SETUP 脚本文件的详细信息，请参阅文档！!

#include "InnoDependencyInstaller\CodeDependencies.iss"
#define MyAppName "neo-bpsys-wpf"
; Extract File Version from EXE
#define AppExePath "..\build\neo-bpsys-wpf\neo-bpsys-wpf.exe"
#define MyAppVersion GetVersionNumbersString(AppExePath)
#define AppProductTextVersion GetStringFileInfo(AppExePath, "ProductVersion")
#define MyAppPublisher "PLFJY"
#define MyAppURL "https://bpsys.plfjy.top/"
#define MyAppExeName "neo-bpsys-wpf.exe"
#define BpuiIconName "bpui_icon.ico"

[Setup]
;注意:AppId 的值唯一标识此应用程序。请勿在安装程序中对其他应用程序使用相同的 AppId 值。
;（若要生成新的 GUID，请单击“工具”|”在 IDE 中生成 GUID）。
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
;下行注释，指定安装程序无法运行，除 Arm 上的 x64 和 Windows 11 之外的任何平台上.
ArchitecturesAllowed=x64compatible
WizardImageFile=侧图186x356.bmp
;WizardSmallImageFile=顶图165x54.bmp
WizardSmallImageFile=顶图54x54.bmp
;下行注释，强制安装程序在 64 位系统上，但不强制以 64 位模式运行.
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
;下面两行注释是License文件和InfoShown
LicenseFile=License.txt
;InfoBeforeFile=Readme.txt
;取消下行前面 ; 符号，在非管理安装模式下运行（仅为当前用户安装）.
;PrivilegesRequired=lowest
OutputDir=..\build\
OutputBaseFilename=neo-bpsys-wpf_Installer
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
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[code]
procedure InitializeWizard();
begin
WizardForm.LICENSEACCEPTEDRADIO.checked:= true;
end;

function InitializeSetup: Boolean;
begin
Dependency_AddDotNet100Desktop;
Result := True;
end;

function IsSameOrChildPath(Child: string; Parent: string): Boolean;
var
  NormalChild, NormalParent: string;
begin
  NormalChild := Lowercase(AddBackslash(RemoveBackslash(Child)));
  NormalParent := Lowercase(AddBackslash(RemoveBackslash(Parent)));
  Result := (NormalParent <> '\') and (Pos(NormalParent, NormalChild) = 1);
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
  Key, Tail, Ch: string;
  KeyPos, ColonPos, QuoteStart, I: Integer;
  Escaped: Boolean;
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
  Escaped := False;
  for I := 1 to Length(Tail) do
  begin
    Ch := Copy(Tail, I, 1);
    if (Ch = '"') and (not Escaped) then
    begin
      Result := Copy(Tail, 1, I - 1);
      StringChangeEx(Result, '\\', '\', True);
      StringChangeEx(Result, '\/', '/', True);
      exit;
    end;

    if (Ch = '\') and (not Escaped) then
      Escaped := True
    else
      Escaped := False;
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

  ModuleRoot := ExtractJsonStringValue(StateJson, 'ModuleRoot');
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

//卸载时总是删除 SmartBP 模块，用户数据仍由用户选择是否删除
procedure CurUninstallStepChanged (CurUninstallStep: TUninstallStep);
var
    mres : integer;
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
