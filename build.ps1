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
        [Parameter()] [string] $WorkingDirectory,
        [Parameter()] [string] $ErrorMessage = "External command failed."
    )

    $oldLocation = $null
    if ($WorkingDirectory) {
        $oldLocation = Get-Location
        Set-Location -LiteralPath $WorkingDirectory
    }
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$ErrorMessage (ExitCode=$LASTEXITCODE): $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        if ($null -ne $oldLocation) {
            Set-Location -LiteralPath $oldLocation
        }
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
$TeamJsonMakerProjPath = Join-Path $RepoRoot "Built-inPlugins\neo-bpsys-wpf.TeamJsonMaker\neo-bpsys-wpf.TeamJsonMaker.csproj"
$WebRendererProjPath = Join-Path $RepoRoot "Built-inPlugins\neo-bpsys-wpf.WebRenderer\neo-bpsys-wpf.WebRenderer.csproj"
$RuntimeIdentifier = "win-x64"
$SelfContained = "false"

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
    "restore", $ProjPath,
    "-r", $RuntimeIdentifier,
    "/p:SelfContained=$SelfContained"
) -ErrorMessage "dotnet restore failed"

Invoke-External -FilePath "dotnet" -Arguments @(
    "restore", $TeamJsonMakerProjPath,
    "-r", $RuntimeIdentifier,
    "/p:SelfContained=$SelfContained"
) -ErrorMessage "TeamJsonMaker plugin restore failed"

Invoke-External -FilePath "dotnet" -Arguments @(
    "restore", $WebRendererProjPath,
    "-r", $RuntimeIdentifier,
    "/p:SelfContained=$SelfContained"
) -ErrorMessage "Web Renderer plugin restore failed"

Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ProjPath,
    "-c", $Configuration,
    "-o", $BuildPath,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained,
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
    "restore", $ModuleProjPath,
    "-r", $RuntimeIdentifier,
    "/p:SelfContained=$SelfContained"
) -ErrorMessage "SmartBP module restore failed"

Invoke-External -FilePath "dotnet" -Arguments @(
    "publish", $ModuleProjPath,
    "-c", $Configuration,
    "-o", $ModuleBuildPath,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained,
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
$ModuleVersion = [string]$ComponentManifest.ModuleVersion
if ([string]::IsNullOrWhiteSpace($ModuleVersion)) {
    throw "SmartBP module component manifest has empty ModuleVersion: $ComponentManifestPath"
}
Write-Host "SmartBP module version: $ModuleVersion" -ForegroundColor Cyan

$ModuleArchive = Join-Path $RepoRoot "build\SmartBpModule.7z"
if (Test-Path -LiteralPath $ModuleArchive) {
    Remove-Item -LiteralPath $ModuleArchive -Force
}

$SevenZipExe = Join-Path $RepoRoot "third_party\7zip\win-x64\7z.exe"
if (-not (Test-Path -LiteralPath $SevenZipExe)) {
    throw "Official 7-Zip x64 binary missing. Run tools\Fetch-SevenZip.ps1 -Update first. Expected: $SevenZipExe"
}

$ListFile = Join-Path $RepoRoot "build\smartbp-module-filelist.txt"
try {
    # 枚举 staging 内全部文件，生成排序后的 UTF-8 无 BOM list file（相对路径，/ 分隔）
    # 使用 StringComparer.Ordinal 做序号排序，保证跨环境排序一致。
    # 注:Sort-Object -Ordinal 仅 PowerShell 7.4+ 支持，这里改用 [Array]::Sort 以兼容 PS 5.1+。
    $moduleFiles = @(
        Get-ChildItem -LiteralPath $ModuleBuildPath -Recurse -File |
            ForEach-Object {
                $relativePath = Get-RelativePathCompat -BasePath $ModuleBuildPath -TargetPath $_.FullName
                $relativePath -replace '\\', '/'
            }
    )
    if ($moduleFiles.Count -eq 0) {
        throw "SmartBP module staging directory is empty: $ModuleBuildPath"
    }
    $moduleFilesArray = [string[]]$moduleFiles
    [Array]::Sort($moduleFilesArray, [System.StringComparer]::Ordinal)
    $moduleFiles = $moduleFilesArray
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($ListFile, $moduleFiles, $utf8NoBom)

    Invoke-External -FilePath $SevenZipExe `
        -Arguments @(
            "a",
            "-t7z",
            $ModuleArchive,
            "@$ListFile",
            "-m0=lzma2",
            "-mx=9",
            "-mmt=on",
            "-y",
            "-bso1",
            "-bse2",
            "-bsp1",
            "-bb1",
            "-scsUTF-8"
        ) `
        -WorkingDirectory $ModuleBuildPath `
        -ErrorMessage "SmartBP module 7z packaging failed"
}
catch {
    if (Test-Path -LiteralPath $ModuleArchive) {
        Remove-Item -LiteralPath $ModuleArchive -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $ListFile) {
        Remove-Item -LiteralPath $ListFile -Force
    }
}

# 打包后验证：7z t 完整性测试
Invoke-External -FilePath $SevenZipExe `
    -Arguments @("t", $ModuleArchive, "-bso0", "-bse2", "-bsp0") `
    -ErrorMessage "SmartBP module archive integrity test (7z t) failed"

# 列表内容校验
# 使用 -slt -ba:输出结构化的条目块(每个条目含 Path = 字段),-ba 抑制归档级头部。
# 相比默认表格输出,结构化字段更稳定,正则匹配 Path = 不受列宽/对齐影响。
# 注:PowerShell 捕获外部命令输出得到的是字符串数组(每行一个元素),-notmatch 对数组
# 操作会返回"不匹配的元素列表"(非空即真),语义错误。这里先 join 成单个字符串,
# 再用 (?m) 多行模式匹配,确保 -notmatch 在单字符串上正确返回布尔值。
$listOutput = & $SevenZipExe l $ModuleArchive -slt -ba -sccUTF-8
if ($LASTEXITCODE -ne 0) {
    throw "SmartBP module archive list verification failed (exit code $LASTEXITCODE)"
}
$listText = ($listOutput -join "`n")
if ($listText -notmatch '(?m)^Path\s*=\s*component\.json\s*$') {
    throw "SmartBP module archive missing component.json"
}
if ($listText -match '(?m)^Path\s*=\s*SmartBpModule[/\\]') {
    throw "SmartBP module archive has unexpected SmartBpModule/ top-level directory"
}
if ($listText -match '(?m)^Path\s*=\s*SmartBpModule\.7z') {
    throw "SmartBP module archive contains its own output archive"
}
if ($listText -match '(?m)^Path\s*=\s*smartbp-module-filelist\.txt') {
    throw "SmartBP module archive contains the list file"
}

$ModuleArchiveHash = Get-Sha256Hash -LiteralPath $ModuleArchive
$ModuleArchiveSize = (Get-Item -LiteralPath $ModuleArchive).Length
$ModuleManifestPath = Join-Path $RepoRoot "build\SmartBpModuleManifest.json"
$ModuleManifest = [ordered]@{
    ComponentId = "SmartBpModule"
    ModuleVersion = $ModuleVersion
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
