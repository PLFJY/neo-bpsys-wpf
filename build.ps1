#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    # Works in Windows PowerShell 5.1 and PowerShell 7+
    if ($PSScriptRoot) { return $PSScriptRoot }

    # Fallback when $PSScriptRoot is empty (e.g., pasted into console)
    $path = $MyInvocation.MyCommand.Path
    if ($path) { return (Split-Path -Parent $path) }

    throw "Cannot determine script directory. Please run this as a .ps1 file."
}

function Invoke-External {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [Parameter()] [string[]] $Arguments = @(),
        [Parameter()] [string] $ErrorMessage = "External command failed."
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage (ExitCode=$LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

# Switch to the script's directory
$ScriptDir = Get-ScriptDirectory
Set-Location -Path $ScriptDir

# Paths
$RepoRoot = $ScriptDir
$BuildPath = Join-Path $RepoRoot "build\neo-bpsys-wpf"
$ModuleBuildPath = Join-Path $RepoRoot "build\SmartBpModule"
$ProjPath  = Join-Path $RepoRoot "neo-bpsys-wpf\neo-bpsys-wpf.csproj"
$ModuleProjPath = Join-Path $RepoRoot "neo-bpsys-wpf.SmartBp.Module\neo-bpsys-wpf.SmartBp.Module.csproj"

# Ensure output directory exists
if (-not (Test-Path -LiteralPath $BuildPath)) {
    New-Item -ItemType Directory -Path $BuildPath | Out-Null
}

# Get git hash (fail fast if git not available / not a repo)
$gitHashRaw = & git rev-parse --short=7 HEAD
if ($LASTEXITCODE -ne 0 -or -not $gitHashRaw) {
    throw "Failed to get git hash. Ensure git is installed and this is a git repository."
}
$GitHash = $gitHashRaw.Trim()

# Build (dotnet publish)
Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ProjPath,
    "-c", "Release",
    "-o", $BuildPath,
    "--no-restore",
    "/p:BuildMeta=$GitHash"
) -ErrorMessage "dotnet publish failed"

# Validate build artifact exists (required by Inno Setup script)
$MainExe = Join-Path $BuildPath "neo-bpsys-wpf.exe"
if (-not (Test-Path -LiteralPath $MainExe)) {
    Write-Host "Build output missing: $MainExe" -ForegroundColor Red
    Write-Host "Contents of {$BuildPath}:" -ForegroundColor Yellow
    Get-ChildItem -Path $BuildPath -Recurse | Format-Table -AutoSize
    throw "dotnet publish finished but main executable was not produced."
}

$ReleaseTag = (Get-Item -LiteralPath $MainExe).VersionInfo.ProductVersion.Trim()
if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
    throw "Failed to read release tag from ProductVersion: $MainExe"
}

# Pack installer
$IsccPath      = Join-Path $RepoRoot "Installer\Inno Setup 6\ISCC.exe"
$InstallerIss  = Join-Path $RepoRoot "Installer\build_Installer.iss"

if (-not (Test-Path -LiteralPath $IsccPath)) {
    throw "ISCC.exe not found at: $IsccPath"
}
if (-not (Test-Path -LiteralPath $InstallerIss)) {
    throw ".iss script not found at: $InstallerIss"
}

Invoke-External -FilePath $IsccPath -Arguments @($InstallerIss) -ErrorMessage "Inno Setup packaging failed"

$LiteInstaller = Join-Path $RepoRoot "build\neo-bpsys-wpf_Installer.exe"
$LiteHash = Join-Path $RepoRoot "build\neo-bpsys-wpf_Installer.exe.sha256"
if (-not (Test-Path -LiteralPath $LiteInstaller)) {
    throw "Lite installer missing: $LiteInstaller"
}
(Get-FileHash -LiteralPath $LiteInstaller -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath $LiteHash -NoNewline

if (Test-Path -LiteralPath $ModuleBuildPath) {
    Remove-Item -LiteralPath $ModuleBuildPath -Recurse -Force
}
New-Item -ItemType Directory -Path $ModuleBuildPath | Out-Null

Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ModuleProjPath,
    "-c", "Release",
    "-o", $ModuleBuildPath,
    "--no-restore"
) -ErrorMessage "SmartBP module publish failed"

$HostProvidedFiles = @{}
Get-ChildItem -LiteralPath $BuildPath -Recurse -File | ForEach-Object {
    $relativePath = [System.IO.Path]::GetRelativePath($BuildPath, $_.FullName)
    $HostProvidedFiles[$relativePath.ToLowerInvariant()] = $true
}

Get-ChildItem -LiteralPath $ModuleBuildPath -Recurse -File | ForEach-Object {
    $relativePath = [System.IO.Path]::GetRelativePath($ModuleBuildPath, $_.FullName)
    if ($HostProvidedFiles.ContainsKey($relativePath.ToLowerInvariant())) {
        Remove-Item -LiteralPath $_.FullName -Force
    }
}

$ComponentManifestPath = Join-Path $ModuleBuildPath "component.json"
if (-not (Test-Path -LiteralPath $ComponentManifestPath)) {
    throw "SmartBP module component manifest missing: $ComponentManifestPath"
}
$ComponentManifest = Get-Content -LiteralPath $ComponentManifestPath -Raw | ConvertFrom-Json
$ComponentManifest.ModuleVersion = $ReleaseTag
$ComponentManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ComponentManifestPath -Encoding UTF8

$ModuleZip = Join-Path $RepoRoot "build\SmartBpModule.zip"
if (Test-Path -LiteralPath $ModuleZip) {
    Remove-Item -LiteralPath $ModuleZip -Force
}
Compress-Archive -Path (Join-Path $ModuleBuildPath "*") -DestinationPath $ModuleZip -Force
$ModuleZipHash = (Get-FileHash -LiteralPath $ModuleZip -Algorithm SHA256).Hash.ToLowerInvariant()
$ModuleZipSize = (Get-Item -LiteralPath $ModuleZip).Length
$ModuleManifestPath = Join-Path $RepoRoot "build\SmartBpModuleManifest.json"
$ModuleManifest = [ordered]@{
    ComponentId = "SmartBpModule"
    ModuleVersion = $ReleaseTag
    RuntimeAbiVersion = 1
    Rid = "win-x64"
    RequiredAppVersion = ">=3.0.0"
    PackageVersions = [ordered]@{
        "OpenCvSharp4.Windows.Slim" = "4.13.0.20260602"
        "OpenCvSharp4.WpfExtensions" = "4.13.0.20260602"
        "Sdcb.PaddleInference.runtime.win64.mkl" = "3.3.1.70"
        "Sdcb.PaddleOCR" = "3.3.1"
        "Sdcb.PaddleOCR.Models.Online" = "3.3.1"
    }
    Asset = [ordered]@{
        Name = "SmartBpModule.zip"
        Url = "https://github.com/PLFJY/neo-bpsys-wpf/releases/download/$ReleaseTag/SmartBpModule.zip"
        Size = $ModuleZipSize
        Sha256 = $ModuleZipHash
    }
}
$ModuleManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ModuleManifestPath -Encoding UTF8

$FullInstallerIss = Join-Path $RepoRoot "Installer\build_Installer_full.iss"
Invoke-External -FilePath $IsccPath -Arguments @($FullInstallerIss) -ErrorMessage "Full Inno Setup packaging failed"
$FullInstaller = Join-Path $RepoRoot "build\neo-bpsys-wpf_Installer_full.exe"
$FullHash = Join-Path $RepoRoot "build\neo-bpsys-wpf_Installer_full.exe.sha256"
if (-not (Test-Path -LiteralPath $FullInstaller)) {
    throw "Full installer missing: $FullInstaller"
}
(Get-FileHash -LiteralPath $FullInstaller -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath $FullHash -NoNewline
