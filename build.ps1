# Switch to the script's directory
Set-Location -Path $PSScriptRoot

# Build csporj
$BUILD_PATH = ".\build\neo-bpsys-wpf"
$PROJ_PATH = ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj"
$GIT_HASH = (git rev-parse --short=7 HEAD).Trim()

# Check if output directory exists, if not, create it
if (-Not (Test-Path -Path $BUILD_PATH)) {
    New-Item -ItemType Directory -Path $BUILD_PATH
}

# Build the project
dotnet publish $PROJ_PATH -c Release -o $BUILD_PATH /p:BuildMeta=$GIT_HASH

# Validate build artifact exists (required by Inno Setup script)
$MAIN_EXE = Join-Path $BUILD_PATH "neo-bpsys-wpf.exe"
if (-Not (Test-Path -Path $MAIN_EXE)) {
    Write-Host "Build output missing: $MAIN_EXE" -ForegroundColor Red
    Write-Host "Contents of {$BUILD_PATH}:" -ForegroundColor Yellow
    Get-ChildItem -Path $BUILD_PATH -Recurse | Format-Table -AutoSize
    throw "dotnet publish finished but main executable was not produced."
}

# Pack installer
# Set packer dir
$ISCC_PATH = ".\Installer\Inno Setup 6\ISCC.exe"
# Set pack script dir
$INSTALLER_PATH = ".\Installer\build_Installer.iss"

# Pack the installer
& $ISCC_PATH $INSTALLER_PATH
