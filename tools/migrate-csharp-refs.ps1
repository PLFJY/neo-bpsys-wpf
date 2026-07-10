# C# localization reference migration script
# Migrates I18nHelper.GetLocalizedString("key") -> I18nHelper.GetLocalizedString(AppI18nDictionaries.X, "key")
# Uses key-map.csv for key -> dictionary mapping

$ErrorActionPreference = 'Stop'
$root = "e:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf"

# 1. Load key-map.csv into lookup
$csvPath = Join-Path $root "artifacts\i18n-migration\key-map.csv"
$lines = Get-Content $csvPath -Encoding UTF8
$keyToDict = @{}
$notFoundKeys = @{}
for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    # Parse CSV: Key,SourceDictionary,TargetAssembly,TargetDictionary,...
    # Handle quoted fields
    $fields = @()
    $inQuotes = $false
    $field = ""
    foreach ($c in $line.ToCharArray()) {
        if ($c -eq '"') { $inQuotes = -not $inQuotes; continue }
        if ($c -eq ',' -and -not $inQuotes) { $fields += $field; $field = ""; continue }
        $field += $c
    }
    $fields += $field
    $key = $fields[0]
    $targetDict = $fields[3]
    $keyToDict[$key] = $targetDict
}
Write-Output "Loaded $($keyToDict.Count) keys from key-map.csv"

# 2. Map dictionary name to constant name
$dictToConstant = @{
    "Locales.Common"          = "AppI18nDictionaries.Common"
    "Locales.Shell"           = "AppI18nDictionaries.Shell"
    "Locales.Team"            = "AppI18nDictionaries.Team"
    "Locales.Game"            = "AppI18nDictionaries.Game"
    "Locales.Bp"              = "AppI18nDictionaries.Bp"
    "Locales.Score"           = "AppI18nDictionaries.Score"
    "Locales.FrontManage"     = "AppI18nDictionaries.FrontManage"
    "Locales.Designer"        = "AppI18nDictionaries.Designer"
    "Locales.AnimationEditor" = "AppI18nDictionaries.AnimationEditor"
    "Locales.Settings"        = "AppI18nDictionaries.Settings"
    "Locales.PluginMarket"    = "AppI18nDictionaries.PluginMarket"
}

# 3. Files to skip
$skipFiles = @(
    "Lang.Designer.cs",
    "I18nHelper.cs",
    "AppI18nDictionaries.cs"
)

# 4. Process host C# files
$hostDir = Join-Path $root "neo-bpsys-wpf"
$csFiles = Get-ChildItem -Path $hostDir -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch '\\(obj|bin)\\' -and
    $skipFiles -notcontains $_.Name
}

$totalReplacements = 0
$filesChanged = 0
$unmappedKeys = @{}

foreach ($file in $csFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $original = $content
    $fileReplacements = 0

    # Pattern: GetLocalizedString("literalkey" followed by , or )
    # This naturally skips already-migrated calls (GetLocalizedString(AppI18nDictionaries.X, "key")
    # and dynamic calls (GetLocalizedString(variable))
    $regex = [regex]'GetLocalizedString\("([^"]+)"\s*([,)])'

    $content = $regex.Replace($content, {
        param($m)
        $key = $m.Groups[1].Value
        $sep = $m.Groups[2].Value
        if ($keyToDict.ContainsKey($key)) {
            $targetDict = $keyToDict[$key]
            $constant = $dictToConstant[$targetDict]
            if ($constant) {
                $fileReplacements++
                return "GetLocalizedString($constant, `"$key`"$sep"
            } else {
                # SmartBp module dict - shouldn't happen in host, but report
                if (-not $unmappedKeys.ContainsKey($key)) { $unmappedKeys[$key] = $targetDict }
                return $m.Value
            }
        } else {
            # Key not found in map - report and skip
            if (-not $unmappedKeys.ContainsKey($key)) { $unmappedKeys[$key] = "NOT_FOUND" }
            return $m.Value
        }
    })

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        $filesChanged++
        $totalReplacements += $fileReplacements
        Write-Output "  $($file.Name): $fileReplacements replacements"
    }
}

Write-Output ""
Write-Output "C# migration complete: $totalReplacements replacements across $filesChanged files"

if ($unmappedKeys.Count -gt 0) {
    Write-Output ""
    Write-Output "Unmapped keys (not migrated):"
    foreach ($k in $unmappedKeys.Keys | Sort-Object) {
        Write-Output "  $k -> $($unmappedKeys[$k])"
    }
}
