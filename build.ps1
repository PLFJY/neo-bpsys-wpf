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
$ProjPath  = Join-Path $RepoRoot "neo-bpsys-wpf\neo-bpsys-wpf.csproj"

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
