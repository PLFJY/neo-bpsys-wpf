<#
.SYNOPSIS
    Fetch and lock official x64 7-Zip binaries from ip7z/7zip GitHub releases.

.DESCRIPTION
    Default mode: verifies that third_party/7zip/win-x64/ contains 7z.exe, 7z.dll and
    License.txt whose SHA-256 matches the values recorded in
    third_party/7zip/7zip.lock.json. Exits non-zero if anything is missing or mismatched.

    -Update mode: queries the GitHub release API for ip7z/7zip latest stable release,
    downloads the x64 installer and 7zr.exe bootstrap, verifies their digest, extracts
    7z.exe / 7z.dll / License.txt via 7zr.exe, validates PE x64 architecture, and writes
    the lock file. The cache and extraction temp directories are never committed.

    The script never queries "latest" implicitly during a normal build - only -Update
    does. The committed binaries in third_party/7zip/win-x64/ are the source of truth
    for normal builds.

.PARAMETER Update
    When set, queries the GitHub API for the latest stable release, downloads assets,
    extracts canonical binaries, and rewrites the lock file.

.PARAMETER SkipVersionCheck
    Internal/testing only. When set with default mode, skips the online release tag
    comparison hint. Does not change verification of local files.

.EXAMPLE
    pwsh -File tools/Fetch-SevenZip.ps1
    Verifies committed 7-Zip binaries match the lock file.

.EXAMPLE
    pwsh -File tools/Fetch-SevenZip.ps1 -Update
    Downloads latest stable 7-Zip, extracts canonical x64 binaries, rewrites lock file.
#>

#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch] $Update,
    [switch] $SkipVersionCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Force TLS 1.2 (PowerShell 5.1 defaults to TLS 1.0 which GitHub rejects)
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
} catch {
    # Some environments already enforce a higher protocol set; ignore.
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = Split-Path -Parent $ScriptDir
$SevenZipRoot = Join-Path $RepoRoot "third_party\7zip"
$BinaryDir = Join-Path $SevenZipRoot "win-x64"
$CacheDir = Join-Path $SevenZipRoot ".cache"
$ExtractDir = Join-Path $SevenZipRoot ".extract"
$LockFile = Join-Path $SevenZipRoot "7zip.lock.json"

$GitHubApiUrl = "https://api.github.com/repos/ip7z/7zip/releases/latest"

function Write-Step {
    param([Parameter(Mandatory)][string] $Message)
    Write-Host "[Fetch-SevenZip] $Message" -ForegroundColor Cyan
}

function Write-StepDetail {
    param([Parameter(Mandatory)][string] $Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Test-PeX64 {
    <#
        Verifies that a file is a PE image targeting AMD64 (x64).
        Returns $true if valid x64 PE, $false otherwise.
    #>
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 64) { return $false }

    # DOS header: 'MZ'
    if ($bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { return $false }

    # e_lfanew at offset 0x3C (4 bytes, little-endian)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0 -or ($peOffset + 6) -gt $bytes.Length) { return $false }

    # PE header: 'PE\0\0'
    if ($bytes[$peOffset]     -ne 0x50 -or `
        $bytes[$peOffset + 1] -ne 0x45 -or `
        $bytes[$peOffset + 2] -ne 0x00 -or `
        $bytes[$peOffset + 3] -ne 0x00) { return $false }

    # COFF Machine field at PE header + 4 (2 bytes, little-endian)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    # 0x8664 = AMD64 (x64)
    return ($machine -eq 0x8664)
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }
    $stream = $null
    $sha = $null
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $hashBytes = $sha.ComputeHash($stream)
        return -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    } finally {
        if ($null -ne $sha) { $sha.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Get-GitHubRelease {
    Write-Step "Querying GitHub API: $GitHubApiUrl"
    $headers = @{
        'User-Agent' = 'neo-bpsys-wpf-fetch-sevenzip'
        'Accept' = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    try {
        $release = Invoke-RestMethod -Method Get -Uri $GitHubApiUrl -Headers $headers -ErrorAction Stop
    } catch {
        throw "Failed to query GitHub API: $($_.Exception.Message)"
    }
    if ($null -eq $release) { throw "GitHub API returned null release." }
    if ($release.draft) { throw "Latest release is a draft. Refusing to use." }
    if ($release.prerelease) { throw "Latest release is a prerelease. Refusing to use." }
    if ([string]::IsNullOrWhiteSpace($release.tag_name)) { throw "Release has no tag_name." }
    return $release
}

function Find-ReleaseAsset {
    param(
        [Parameter(Mandatory)] $Assets,
        [Parameter(Mandatory)][string] $Pattern
    )
    $matches = @($assets | Where-Object { $_.name -match $Pattern })
    if ($matches.Count -eq 0) {
        throw "No asset matched pattern '$Pattern'. Available: $($assets.name -join ', ')"
    }
    if ($matches.Count -gt 1) {
        throw "Multiple assets matched pattern '$Pattern': $($matches.name -join ', ')"
    }
    return $matches[0]
}

function Confirm-AssetDigest {
    param(
        [Parameter(Mandatory)] $Asset,
        [Parameter(Mandatory)][string] $DownloadedPath
    )
    $digest = $Asset.digest
    if ([string]::IsNullOrWhiteSpace($digest)) {
        throw "Asset '$($Asset.name)' has no digest. Refusing to verify without digest."
    }
    $parts = $digest -split ':', 2
    if ($parts.Length -ne 2 -or $parts[0] -ne 'sha256') {
        throw "Asset '$($Asset.name)' digest is not sha256: '$digest'"
    }
    $expected = $parts[1].ToLowerInvariant()
    $actual = Get-FileSha256 -Path $DownloadedPath
    if ($actual -ne $expected) {
        try { Remove-Item -LiteralPath $DownloadedPath -Force } catch {}
        throw "SHA-256 mismatch for asset '$($Asset.name)': expected=$expected, actual=$actual. Downloaded file deleted."
    }
    Write-StepDetail "digest verified: $actual"
    return $actual
}

function Download-Asset {
    param(
        [Parameter(Mandatory)] $Asset,
        [Parameter(Mandatory)][string] $DestinationPath
    )
    $url = $Asset.browser_download_url
    if ([string]::IsNullOrWhiteSpace($url)) {
        throw "Asset '$($Asset.name)' has no browser_download_url."
    }
    Write-Step "Downloading: $url"
    $headers = @{
        'User-Agent' = 'neo-bpsys-wpf-fetch-sevenzip'
        'Accept' = 'application/octet-stream'
    }
    try {
        Invoke-WebRequest -Uri $url -OutFile $DestinationPath -Headers $headers -UseBasicParsing -ErrorAction Stop
    } catch {
        throw "Failed to download asset '$($Asset.name)' from $url`: $($_.Exception.Message)"
    }
    if (-not (Test-Path -LiteralPath $DestinationPath)) {
        throw "Download did not produce file: $DestinationPath"
    }
}

function Invoke-SevenZiprExtract {
    param(
        [Parameter(Mandatory)][string] $SevenZiprExe,
        [Parameter(Mandatory)][string] $InstallerExe,
        [Parameter(Mandatory)][string] $DestinationDir
    )
    if (-not (Test-Path -LiteralPath $SevenZiprExe)) {
        throw "7zr.exe not found: $SevenZiprExe"
    }
    # Note: 7zr.exe is a bootstrap extractor and may be 32-bit. Only the final
    # 7z.exe and 7z.dll extracted from the x64 installer are required to be x64.
    New-Item -ItemType Directory -Force -Path $DestinationDir | Out-Null
    Write-Step "Extracting via 7zr.exe: $InstallerExe -> $DestinationDir"
    & $SevenZiprExe e $InstallerExe "-o$DestinationDir" 7z.exe 7z.dll License.txt -y -bso0 -bse2 -bsp0
    if ($LASTEXITCODE -ne 0) {
        throw "7zr.exe extraction failed with exit code $LASTEXITCODE."
    }
}

function Write-LockFile {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)] $Release,
        [Parameter(Mandatory)] $InstallerAsset,
        [Parameter(Mandatory)][string] $InstallerSha256,
        [Parameter(Mandatory)] $BootstrapAsset,
        [Parameter(Mandatory)][string] $BootstrapSha256,
        [Parameter(Mandatory)][string] $SevenZipExeSha256,
        [Parameter(Mandatory)][string] $SevenZipDllSha256
    )
    $lock = [ordered]@{
        version = $Release.tag_name
        tag = $Release.tag_name
        architecture = 'x64'
        installerAsset = [ordered]@{
            name = $InstallerAsset.name
            url = $InstallerAsset.browser_download_url
            sha256 = $InstallerSha256
        }
        bootstrapAsset = [ordered]@{
            name = $BootstrapAsset.name
            url = $BootstrapAsset.browser_download_url
            sha256 = $BootstrapSha256
        }
        files = [ordered]@{
            '7z.exe' = $SevenZipExeSha256
            '7z.dll' = $SevenZipDllSha256
        }
    }
    $json = $lock | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Step "Lock file written: $Path"
}

function Test-LockAndBinaries {
    <#
        Default-mode verification. Returns $true if everything checks out.
    #>
    if (-not (Test-Path -LiteralPath $LockFile)) {
        Write-Host "Lock file not found: $LockFile" -ForegroundColor Red
        Write-Host "Run: pwsh -File tools\Fetch-SevenZip.ps1 -Update" -ForegroundColor Yellow
        return $false
    }
    try {
        $lock = Get-Content -LiteralPath $LockFile -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        Write-Host "Lock file is not valid JSON: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    if ($lock.architecture -ne 'x64') {
        Write-Host "Lock file architecture is not 'x64': $($lock.architecture)" -ForegroundColor Red
        return $false
    }

    foreach ($fileName in @('7z.exe', '7z.dll')) {
        $filePath = Join-Path $BinaryDir $fileName
        if (-not (Test-Path -LiteralPath $filePath)) {
            Write-Host "Missing canonical binary: $filePath" -ForegroundColor Red
            return $false
        }
        $expected = $lock.files.$fileName
        if ([string]::IsNullOrWhiteSpace($expected)) {
            Write-Host "Lock file missing sha256 for $fileName" -ForegroundColor Red
            return $false
        }
        $actual = Get-FileSha256 -Path $filePath
        if ($actual -ne $expected) {
            Write-Host "SHA-256 mismatch for $fileName`: expected=$expected, actual=$actual" -ForegroundColor Red
            return $false
        }
        if (-not (Test-PeX64 -Path $filePath)) {
            Write-Host "$fileName is not a PE x64 binary: $filePath" -ForegroundColor Red
            return $false
        }
        Write-StepDetail "$fileName OK (sha256=$actual)"
    }

    $licensePath = Join-Path $BinaryDir 'License.txt'
    if (-not (Test-Path -LiteralPath $licensePath)) {
        Write-Host "Missing License.txt: $licensePath" -ForegroundColor Red
        return $false
    }
    Write-StepDetail "License.txt OK"

    Write-Step "Verified 7-Zip binaries match lock file (version=$($lock.version))."
    return $true
}

function Invoke-Update {
    if (-not (Test-Path -LiteralPath $SevenZipRoot)) {
        New-Item -ItemType Directory -Force -Path $SevenZipRoot | Out-Null
    }
    if (Test-Path -LiteralPath $CacheDir) {
        Remove-Item -LiteralPath $CacheDir -Recurse -Force
    }
    if (Test-Path -LiteralPath $ExtractDir) {
        Remove-Item -LiteralPath $ExtractDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
    New-Item -ItemType Directory -Force -Path $ExtractDir | Out-Null

    try {
        $release = Get-GitHubRelease
        Write-Step "Latest stable release: tag=$($release.tag_name)"

        $installerAsset = Find-ReleaseAsset -Assets $release.assets -Pattern '^7z\d+-x64\.exe$'
        $bootstrapAsset = Find-ReleaseAsset -Assets $release.assets -Pattern '^7zr\.exe$'
        Write-StepDetail "installer asset: $($installerAsset.name)"
        Write-StepDetail "bootstrap asset: $($bootstrapAsset.name)"

        $installerPath = Join-Path $CacheDir $installerAsset.name
        $bootstrapPath = Join-Path $CacheDir $bootstrapAsset.name

        Download-Asset -Asset $installerAsset -DestinationPath $installerPath
        $installerSha256 = Confirm-AssetDigest -Asset $installerAsset -DownloadedPath $installerPath

        Download-Asset -Asset $bootstrapAsset -DestinationPath $bootstrapPath
        $bootstrapSha256 = Confirm-AssetDigest -Asset $bootstrapAsset -DownloadedPath $bootstrapPath

        Invoke-SevenZiprExtract -SevenZiprExe $bootstrapPath -InstallerExe $installerPath -DestinationDir $ExtractDir

        $extractedExe = Join-Path $ExtractDir '7z.exe'
        $extractedDll = Join-Path $ExtractDir '7z.dll'
        $extractedLicense = Join-Path $ExtractDir 'License.txt'

        if (-not (Test-Path -LiteralPath $extractedExe)) { throw "Extraction did not produce 7z.exe: $extractedExe" }
        if (-not (Test-Path -LiteralPath $extractedDll)) { throw "Extraction did not produce 7z.dll: $extractedDll" }
        if (-not (Test-Path -LiteralPath $extractedLicense)) { throw "Extraction did not produce License.txt: $extractedLicense" }

        if (-not (Test-PeX64 -Path $extractedExe)) { throw "Extracted 7z.exe is not PE x64." }
        if (-not (Test-PeX64 -Path $extractedDll)) { throw "Extracted 7z.dll is not PE x64." }

        $exeSha = Get-FileSha256 -Path $extractedExe
        $dllSha = Get-FileSha256 -Path $extractedDll
        Write-Step "7z.exe sha256: $exeSha"
        Write-Step "7z.dll sha256: $dllSha"

        if (-not (Test-Path -LiteralPath $BinaryDir)) {
            New-Item -ItemType Directory -Force -Path $BinaryDir | Out-Null
        }
        # Move with overwrite
        Move-Item -LiteralPath $extractedExe -Destination (Join-Path $BinaryDir '7z.exe') -Force
        Move-Item -LiteralPath $extractedDll -Destination (Join-Path $BinaryDir '7z.dll') -Force
        Move-Item -LiteralPath $extractedLicense -Destination (Join-Path $BinaryDir 'License.txt') -Force

        Write-LockFile `
            -Path $LockFile `
            -Release $release `
            -InstallerAsset $installerAsset `
            -InstallerSha256 $installerSha256 `
            -BootstrapAsset $bootstrapAsset `
            -BootstrapSha256 $bootstrapSha256 `
            -SevenZipExeSha256 $exeSha `
            -SevenZipDllSha256 $dllSha

        Write-Step "7-Zip x64 binaries installed to: $BinaryDir"
        Write-Step "Lock file: $LockFile"
        Write-Step "Release tag: $($release.tag_name)"
    } finally {
        if (Test-Path -LiteralPath $CacheDir) {
            try { Remove-Item -LiteralPath $CacheDir -Recurse -Force } catch {}
        }
        if (Test-Path -LiteralPath $ExtractDir) {
            try { Remove-Item -LiteralPath $ExtractDir -Recurse -Force } catch {}
        }
    }
}

if ($Update) {
    Invoke-Update
    if (-not (Test-LockAndBinaries)) {
        throw "Post-update verification failed."
    }
    exit 0
} else {
    if (Test-LockAndBinaries) {
        exit 0
    } else {
        exit 1
    }
}
