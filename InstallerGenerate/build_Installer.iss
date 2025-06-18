;汉化:MonKeyDu 
;由 Inno Setup 脚本向导 生成的脚本,有关创建 INNO SETUP 脚本文件的详细信息，请参阅文档！!

#include "CodeDependencies.iss"
#define MyAppName "neo-bpsys-wpf"
; Extract File Version from EXE
#define MyAppVersion GetFileVersion("..\build\neo-bpsys-wpf\neo-bpsys-wpf.exe")
#define MyAppPublisher "PLFJY"
#define MyAppURL "https://plfjy.top/"
#define MyAppExeName "neo-bpsys-wpf.exe"

[Setup]
;注意:AppId 的值唯一标识此应用程序。请勿在安装程序中对其他应用程序使用相同的 AppId 值。
;（若要生成新的 GUID，请单击“工具”|”在 IDE 中生成 GUID）。
AppId={{842859C0-E6A4-4997-BA10-0933EC09444F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}-v{#MyAppVersion}
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
Dependency_AddDotNet90Desktop;
Result := True;
end;

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifdoesntexist

