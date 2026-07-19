#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'Beta', 'Preview')]
    [string] $Configuration = 'Debug',
    [int] $TimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-External([string] $File, [string[]] $Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $File $($Arguments -join ' ')" }
}
function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); return ([Net.IPEndPoint]$listener.LocalEndpoint).Port } finally { $listener.Stop() }
}
function Get-WindowToken([string] $Value) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}
function Wait-Ready([string] $BaseUrl, [int] $Seconds) {
    for ($i = 0; $i -lt ($Seconds * 4); $i++) {
        try {
            $health = (Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/health" -TimeoutSec 2).Content | ConvertFrom-Json
            if ($health.status -eq 'Ready') { return $health }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    throw "Web Renderer did not reach Ready at $BaseUrl."
}

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'build\web-renderer-ipc-validation'
$app = Join-Path $out 'app'
$project = Join-Path $repo 'neo-bpsys-wpf\neo-bpsys-wpf.csproj'
$teamPlugin = Join-Path $repo 'Built-inPlugins\neo-bpsys-wpf.TeamJsonMaker\neo-bpsys-wpf.TeamJsonMaker.csproj'
$webPlugin = Join-Path $repo 'Built-inPlugins\neo-bpsys-wpf.WebRenderer\neo-bpsys-wpf.WebRenderer.csproj'
$examplePlugin = Join-Path $repo 'neo-bpsys-wpf.ExamplePlugin\neo-bpsys-wpf.ExamplePlugin.csproj'
$port = Get-FreePort
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $out 'main.stdout.log'
$stderr = Join-Path $out 'main.stderr.log'
$screenshot = Join-Path $out 'BpWindow.png'
$process = $null

try {
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out | Out-Null
    Invoke-External dotnet @('restore', $project, '-r', 'win-x64')
    Invoke-External dotnet @('restore', $teamPlugin, '-r', 'win-x64')
    Invoke-External dotnet @('restore', $webPlugin, '-r', 'win-x64')
    if ($Configuration -eq 'Debug') { Invoke-External dotnet @('restore', $examplePlugin, '-r', 'win-x64') }
    Invoke-External dotnet @('publish', $project, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'false', '-o', $app)
    $exe = Join-Path $app 'neo-bpsys-wpf.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "Published application missing: $exe" }
    $process = Start-Process -FilePath $exe -ArgumentList @('--web-port', "$port", '--web-log-protocol') -WorkingDirectory $app -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $health = Wait-Ready $baseUrl $TimeoutSeconds
    $windows = (Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/api/windows" -TimeoutSec 10).Content | ConvertFrom-Json
    $bpWindow = @($windows | Where-Object { $_.fullWindowType -eq 'BpWindow' }) | Select-Object -First 1
    if ($null -eq $bpWindow) { throw 'Ready sidecar did not publish real BpWindow.' }
    $token = Get-WindowToken 'BpWindow'
    $bootstrap = (Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/api/bootstrap/$token" -TimeoutSec 10).Content | ConvertFrom-Json
    foreach ($name in 'WindowSettings', 'CanvasSettings', 'ControlLayout', 'Resources') { if ($null -eq $bootstrap.$name) { throw "BpWindow bootstrap is missing $name" } }
    $edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
    if (-not (Test-Path -LiteralPath $edge)) { throw 'Microsoft Edge headless executable is required for screenshot validation.' }
    & $edge '--headless' '--disable-gpu' "--screenshot=$screenshot" "$baseUrl/render/$token"
    if (-not (Test-Path -LiteralPath $screenshot)) { throw 'Edge did not create BpWindow screenshot.' }
    $logs = @((Get-Content -Raw -ErrorAction SilentlyContinue $stdout), (Get-Content -Raw -ErrorAction SilentlyContinue $stderr)) -join "`n"
    foreach ($entry in 'sidecar process started', 'pipe connected', 'sidecar.ready', 'host.hello', 'bootstrap.replace sent', 'bootstrap.applied received', 'Web Renderer Ready') { if ($logs -notmatch [regex]::Escape($entry)) { throw "Missing required handshake log: $entry" } }
    Write-Host "health: $($health | ConvertTo-Json -Compress)"
    Write-Host "windows: $($windows | ConvertTo-Json -Compress)"
    Write-Host "BpWindow bootstrap: $($bootstrap | ConvertTo-Json -Depth 2 -Compress)"
    Write-Host "screenshot: $screenshot"
    Write-Host 'Web Renderer IPC validation passed.'
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
