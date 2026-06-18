#Requires -Version 5.1
param(
    [Parameter()]
    [ValidateSet("Release", "Beta", "Preview")]
    [string] $Configuration = "Release"
)

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

function Get-Sha256Hash {
    param(
        [Parameter(Mandatory)] [string] $LiteralPath
    )

    $stream = [System.IO.File]::OpenRead($LiteralPath)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($stream)
            return ([System.BitConverter]::ToString($hashBytes) -replace '-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory)] [string] $BasePath,
        [Parameter(Mandatory)] [string] $TargetPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString()) -and
        -not $baseFullPath.EndsWith([System.IO.Path]::AltDirectorySeparatorChar.ToString())) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)
    $baseUri = New-Object System.Uri($baseFullPath)
    $targetUri = New-Object System.Uri($targetFullPath)
    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())
    return $relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar
}

# Switch to the script's directory
$ScriptDir = Get-ScriptDirectory
Set-Location -Path $ScriptDir

# Paths
$RepoRoot = $ScriptDir
$BuildRoot = Join-Path $RepoRoot "build"
$BuildPath = Join-Path $RepoRoot "build\neo-bpsys-wpf"
$ModuleBuildPath = Join-Path $RepoRoot "build\SmartBpModule"
$ProjPath  = Join-Path $RepoRoot "neo-bpsys-wpf\neo-bpsys-wpf.csproj"
$ModuleProjPath = Join-Path $RepoRoot "neo-bpsys-wpf.SmartBp.Module\neo-bpsys-wpf.SmartBp.Module.csproj"

# Clean build output before packaging.
$repoFullPath = [System.IO.Path]::GetFullPath($RepoRoot)
$buildRootFullPath = [System.IO.Path]::GetFullPath($BuildRoot)
$expectedBuildRoot = [System.IO.Path]::GetFullPath((Join-Path $repoFullPath "build"))
if (-not [string]::Equals($buildRootFullPath, $expectedBuildRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected build directory: $buildRootFullPath"
}
if (Test-Path -LiteralPath $buildRootFullPath) {
    Remove-Item -LiteralPath $buildRootFullPath -Recurse -Force
}
New-Item -ItemType Directory -Path $BuildPath | Out-Null

# Get git hash (fail fast if git not available / not a repo)
$gitHashRaw = & git rev-parse --short=7 HEAD
if ($LASTEXITCODE -ne 0 -or -not $gitHashRaw) {
    throw "Failed to get git hash. Ensure git is installed and this is a git repository."
}
$GitHash = $gitHashRaw.Trim()

# Build (dotnet publish)
Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ProjPath,
    "-c", $Configuration,
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
Get-Sha256Hash -LiteralPath $LiteInstaller | Set-Content -LiteralPath $LiteHash -NoNewline

if (Test-Path -LiteralPath $ModuleBuildPath) {
    Remove-Item -LiteralPath $ModuleBuildPath -Recurse -Force
}
New-Item -ItemType Directory -Path $ModuleBuildPath | Out-Null

Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ModuleProjPath,
    "-c", $Configuration,
    "-o", $ModuleBuildPath,
    "--no-restore"
) -ErrorMessage "SmartBP module publish failed"

$HostProvidedFiles = @{}
Get-ChildItem -LiteralPath $BuildPath -Recurse -File | ForEach-Object {
    $relativePath = Get-RelativePathCompat -BasePath $BuildPath -TargetPath $_.FullName
    $HostProvidedFiles[$relativePath.ToLowerInvariant()] = $true
}

Get-ChildItem -LiteralPath $ModuleBuildPath -Recurse -File | ForEach-Object {
    $relativePath = Get-RelativePathCompat -BasePath $ModuleBuildPath -TargetPath $_.FullName
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

$ModuleArchive = Join-Path $RepoRoot "build\SmartBpModule.7z"
if (Test-Path -LiteralPath $ModuleArchive) {
    Remove-Item -LiteralPath $ModuleArchive -Force
}
$ModulePackTool = Join-Path $RepoRoot "tools\PackSmartBpModule.cs"
Invoke-External -FilePath "dotnet" -Arguments @(
    "run", $ModulePackTool,
    "--",
    $ModuleBuildPath,
    $ModuleArchive
) -ErrorMessage "SmartBP module 7z packaging failed"
$ModuleArchiveHash = Get-Sha256Hash -LiteralPath $ModuleArchive
$ModuleArchiveSize = (Get-Item -LiteralPath $ModuleArchive).Length
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
        Name = "SmartBpModule.7z"
        Url = "https://github.com/PLFJY/neo-bpsys-wpf/releases/download/$ReleaseTag/SmartBpModule.7z"
        Size = $ModuleArchiveSize
        Sha256 = $ModuleArchiveHash
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
Get-Sha256Hash -LiteralPath $FullInstaller | Set-Content -LiteralPath $FullHash -NoNewline
