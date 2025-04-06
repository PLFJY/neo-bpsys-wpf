@echo off
REM 切换脚本工作目录目录
cd /d %~dp0

set BUILD_PATH="neo-bpsys-wpf\bin\Release\neo-bpsys-wpf"
set PROJ_PATH="neo-bpsys-wpf\neo-bpsys-wpf.csproj"

REM 检查输出目录是否已存在
if not exist %BUILD_PATH% (
	mkdir %BUILD_PATH%
)

dotnet publish %PROJ_PATH% -c Release -o %BUILD_PATH%

set ISCC_PATH="E:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set INSTALLER_PATH="E:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf\InstallerGenerate\build_Installer.iss"
set INSTALLER_BUILD_PATH="InstallerGenerate\bin"

%ISCC_PATH% %INSTALLER_PATH%