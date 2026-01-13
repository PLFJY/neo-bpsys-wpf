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

# Pack installer
# Set packer dir
$ISCC_PATH = ".\Installer\Inno Setup 6\ISCC.exe"
# Set pack script dir
$INSTALLER_PATH = ".\Installer\build_Installer.iss"

# Pack the installer
& $ISCC_PATH $INSTALLER_PATH
