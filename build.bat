@echo off
REM 切换脚本工作目录目录
cd /d %~dp0

REM 构建
set BUILD_PATH="build\neo-bpsys-wpf"
set PROJ_PATH="neo-bpsys-wpf\neo-bpsys-wpf.csproj"

REM 检查输出目录是否已存在
if not exist %BUILD_PATH% (
	mkdir %BUILD_PATH%
)

dotnet publish %PROJ_PATH% -c Release -o %BUILD_PATH%

REM 安装包打包
REM 设置打包程序路径
set ISCC_PATH="InstallerGenerate\iscc\ISCC.exe"
REM 设置脚本路径
set INSTALLER_PATH="InstallerGenerate\build_Installer.iss"

%ISCC_PATH% %INSTALLER_PATH%