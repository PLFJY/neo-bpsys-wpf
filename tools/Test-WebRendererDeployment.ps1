#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release", "Beta", "Preview")]
    [string] $Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-External {
    param([Parameter(Mandatory)] [string] $FilePath, [Parameter(Mandatory)] [string[]] $Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed (ExitCode=$LASTEXITCODE): $FilePath $($Arguments -join ' ')" }
}

function Assert-Condition {
    param([Parameter(Mandatory)] [bool] $Condition, [Parameter(Mandatory)] [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Get-AvailablePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Get-ClientReferences {
    param([Parameter(Mandatory)] [string] $IndexPath)
    $html = [System.IO.File]::ReadAllText($IndexPath)
    $urls = [System.Collections.Generic.List[string]]::new()
    foreach ($match in [regex]::Matches($html, '<script\b[^>]*\bsrc=["''](?<url>[^"'']+)["''][^>]*>|<link\b[^>]*\bhref=["''](?<url>[^"'']+)["''][^>]*>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $url = $match.Groups['url'].Value
        if ($url.StartsWith('/') -and -not $url.StartsWith('//')) { $urls.Add($url) }
    }
    return @($urls | Select-Object -Unique)
}

function Start-VerifiedSidecar {
    param([Parameter(Mandatory)] [string] $HostDirectory)
    $port = Get-AvailablePort
    $stdout = Join-Path $HostDirectory 'deployment-validation.stdout.log'
    $stderr = Join-Path $HostDirectory 'deployment-validation.stderr.log'
    $hostDll = Join-Path $HostDirectory 'neo-bpsys-wpf.WebRenderer.Host.dll'
    $arguments = @($hostDll, '--pipe', "deployment-validation-$PID-$([Guid]::NewGuid().ToString('N'))", '--parent-pid', "$PID", '--address', '127.0.0.1', '--port', "$port", '--plugin-version', 'deployment-validation')
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -WorkingDirectory $HostDirectory -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -WindowStyle Hidden
    $baseUrl = "http://127.0.0.1:$port"
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw "Sidecar exited before becoming healthy. stderr: $([System.IO.File]::ReadAllText($stderr))" }
        try {
            $health = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/health" -TimeoutSec 2
            if ($health.StatusCode -eq 200) { return [pscustomobject]@{ Process = $process; BaseUrl = $baseUrl; Stdout = $stdout; Stderr = $stderr; Health = $health } }
        }
        catch { Start-Sleep -Milliseconds 250 }
    }
    throw "Sidecar did not become healthy at $baseUrl."
}

function Stop-VerifiedSidecar {
    param($Sidecar)
    if ($null -ne $Sidecar -and -not $Sidecar.Process.HasExited) { Stop-Process -Id $Sidecar.Process.Id -Force }
}

function Test-DeployedClient {
    param([Parameter(Mandatory)] [string] $WwwRoot, [Parameter(Mandatory)] [string] $HostDirectory)
    $indexPath = Join-Path $WwwRoot 'index.html'
    Assert-Condition (Test-Path -LiteralPath $indexPath) "Final plugin index.html is missing: $indexPath"
    $references = Get-ClientReferences $indexPath
    Assert-Condition ($references.Count -gt 0) "Final index.html has no local script/link references: $indexPath"
    foreach ($reference in $references) {
        $filePath = Join-Path $WwwRoot ($reference.TrimStart('/').Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        Assert-Condition (Test-Path -LiteralPath $filePath -PathType Leaf) "Final index reference is missing: $reference ($filePath)"
    }

    $sidecar = $null
    try {
        $sidecar = Start-VerifiedSidecar $HostDirectory
        $health = $sidecar.Health.Content | ConvertFrom-Json
        $buildId = [string]$health.clientBuildId
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($buildId)) 'Health response has no clientBuildId.'
        $html = [System.IO.File]::ReadAllText($indexPath)
        Assert-Condition ($html.Contains("name=`"web-renderer-client-build-id`" content=`"$buildId`"")) "index.html and /health clientBuildId differ."

        $root = Invoke-WebRequest -UseBasicParsing -Uri "$($sidecar.BaseUrl)/" -TimeoutSec 10
        Assert-Condition ($root.StatusCode -eq 200) "GET / returned $($root.StatusCode)."
        Assert-Condition ($root.Headers['Cache-Control'] -eq 'no-store') "GET / cache policy is '$($root.Headers['Cache-Control'])', expected no-store."
        $index = Invoke-WebRequest -UseBasicParsing -Uri "$($sidecar.BaseUrl)/index.html" -TimeoutSec 10
        Assert-Condition ($index.StatusCode -eq 200) "GET /index.html returned $($index.StatusCode)."
        Assert-Condition ($index.Headers['Cache-Control'] -eq 'no-store') "GET /index.html cache policy is '$($index.Headers['Cache-Control'])', expected no-store."
        foreach ($reference in $references) {
            $response = Invoke-WebRequest -UseBasicParsing -Uri "$($sidecar.BaseUrl)$reference" -TimeoutSec 10
            Assert-Condition ($response.StatusCode -eq 200) "GET $reference returned $($response.StatusCode)."
            if ($reference -match '^/assets/.*\.(js|css)$') {
                Assert-Condition ($response.Headers['Cache-Control'] -eq 'public, max-age=31536000, immutable') "GET $reference does not use immutable cache policy."
            }
        }
        return [pscustomobject]@{ BuildId = $buildId; References = $references; Stdout = $sidecar.Stdout; Stderr = $sidecar.Stderr }
    }
    finally { Stop-VerifiedSidecar $sidecar }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$webDirectory = Join-Path $repoRoot 'Built-inPlugins\neo-bpsys-wpf.WebRenderer\Web'
$stylesPath = Join-Path $webDirectory 'src\styles.css'
$projectPath = Join-Path $repoRoot 'neo-bpsys-wpf\neo-bpsys-wpf.csproj'
$teamPluginProject = Join-Path $repoRoot 'Built-inPlugins\neo-bpsys-wpf.TeamJsonMaker\neo-bpsys-wpf.TeamJsonMaker.csproj'
$webPluginProject = Join-Path $repoRoot 'Built-inPlugins\neo-bpsys-wpf.WebRenderer\neo-bpsys-wpf.WebRenderer.csproj'
$examplePluginProject = Join-Path $repoRoot 'neo-bpsys-wpf.ExamplePlugin\neo-bpsys-wpf.ExamplePlugin.csproj'
$validationRoot = Join-Path $repoRoot 'build\web-renderer-deployment-validation'
$publishDirectory = Join-Path $validationRoot 'app'
$pluginDirectory = Join-Path $publishDirectory 'Plugins\top.plfjy.bpsys.WebRenderer'
$hostDirectory = Join-Path $pluginDirectory 'Host'
$wwwRoot = Join-Path $hostDirectory 'wwwroot'
$originalStyles = [System.IO.File]::ReadAllBytes($stylesPath)

try {
    if (Test-Path -LiteralPath $validationRoot) { Remove-Item -LiteralPath $validationRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $validationRoot | Out-Null
    Push-Location $webDirectory
    try {
        Invoke-External 'pnpm' @('install', '--frozen-lockfile')
        Invoke-External 'pnpm' @('run', 'build')
    }
    finally { Pop-Location }

    Invoke-External 'dotnet' @('restore', $projectPath, '-r', 'win-x64')
    Invoke-External 'dotnet' @('restore', $teamPluginProject, '-r', 'win-x64')
    Invoke-External 'dotnet' @('restore', $webPluginProject, '-r', 'win-x64')
    if ($Configuration -eq 'Debug') { Invoke-External 'dotnet' @('restore', $examplePluginProject, '-r', 'win-x64') }
    Invoke-External 'dotnet' @('publish', $projectPath, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'false', '-o', $publishDirectory)
    $first = Test-DeployedClient $wwwRoot $hostDirectory
    $oldReferences = @($first.References)

    $probe = [Guid]::NewGuid().ToString('N')
    [System.IO.File]::AppendAllText($stylesPath, "`n:root { --web-renderer-deployment-probe: '$probe'; }`n", [System.Text.UTF8Encoding]::new($false))
    Invoke-External 'dotnet' @('publish', $projectPath, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'false', '-o', $publishDirectory)
    $second = Test-DeployedClient $wwwRoot $hostDirectory
    Assert-Condition ($first.BuildId -ne $second.BuildId) 'clientBuildId did not change after the second build.'
    $removedReferences = @($oldReferences | Where-Object { $_ -notin $second.References })
    Assert-Condition ($removedReferences.Count -gt 0) 'No hashed client resource changed after modifying Web source.'
    foreach ($reference in $removedReferences) {
        $oldPath = Join-Path $wwwRoot ($reference.TrimStart('/').Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        Assert-Condition (-not (Test-Path -LiteralPath $oldPath)) "Old hashed client resource remains in final output: $reference"
    }
    $logs = (Get-Content -Raw -LiteralPath $second.Stdout) + (Get-Content -Raw -LiteralPath $second.Stderr)
    Assert-Condition ($logs -notmatch '(/assets/[^\s]+\.(js|css).*404|404.*?/assets/[^\s]+\.(js|css))') 'Sidecar log contains an old Vite /assets JS/CSS 404.'

    Write-Host "Final index: $wwwRoot\index.html"
    Write-Host "First clientBuildId: $($first.BuildId)"
    Write-Host "Second clientBuildId: $($second.BuildId)"
    Write-Host "Current references: $($second.References -join ', ')"
    Write-Host "Removed old references: $($removedReferences -join ', ')"
    Write-Host 'Web Renderer deployment validation passed.'
}
finally {
    [System.IO.File]::WriteAllBytes($stylesPath, $originalStyles)
}
