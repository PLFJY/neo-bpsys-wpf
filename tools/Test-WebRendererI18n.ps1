[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path $PSScriptRoot '..\Built-inPlugins\neo-bpsys-wpf.WebRenderer\Web\src'
$files = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Include *.ts,*.tsx
$forbidden = @(
    'localize\s*\(',
    'Dictionaries|AnyHost|MapV2Dictionary|OperationTeam\.Camp',
    'Game:\$\{|Common:\$\{|Any:\$\{',
    '\?\?\s*(key|map|enum)\b',
    'FIRST HALF|SECOND HALF|GAME \{?\d'
)
foreach ($pattern in $forbidden) {
    $match = $files | Select-String -Pattern $pattern
    if ($match) { throw "Web Renderer i18n contract violation: $pattern`n$($match | Out-String)" }
}
Write-Host "Web Renderer i18n static contract passed ($($files.Count) source files)."
