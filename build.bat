@echo off
:: switch dir
cd /d %~dp0

:: build csporj
set BUILD_PATH=".\build\neo-bpsys-wpf"
set PROJ_PATH=".\neo-bpsys-wpf\neo-bpsys-wpf.csproj"

:: check output dir
if not exist %BUILD_PATH% (
	mkdir %BUILD_PATH%
)
:: build
dotnet publish %PROJ_PATH% -c Release -o %BUILD_PATH%

:: pack installer
:: set packer dir
set ISCC_PATH=".\InstallerGenerate\Inno Setup 6\ISCC.exe"
:: set pack script dir
set INSTALLER_PATH=".\InstallerGenerate\build_Installer.iss"
:: pack
%ISCC_PATH% %INSTALLER_PATH%