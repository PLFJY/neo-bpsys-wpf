# XAML dictionary analysis script
# For each XAML file, determines the best default dictionary and reports keys in other dictionaries

$ErrorActionPreference = 'Stop'
$root = "e:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf"

# 1. Load key-map.csv
$csvPath = Join-Path $root "artifacts\i18n-migration\key-map.csv"
$lines = Get-Content $csvPath -Encoding UTF8
$keyToDict = @{}
for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $fields = @()
    $inQuotes = $false
    $field = ""
    foreach ($c in $line.ToCharArray()) {
        if ($c -eq '"') { $inQuotes = -not $inQuotes; continue }
        if ($c -eq ',' -and -not $inQuotes) { $fields += $field; $field = ""; continue }
        $field += $c
    }
    $fields += $field
    $keyToDict[$fields[0]] = $fields[3]
}

# 2. Find all XAML files with DefaultDictionary="Locales.Lang"
$hostDir = Join-Path $root "neo-bpsys-wpf"
$xamlFiles = Get-ChildItem -Path $hostDir -Filter "*.xaml" -Recurse | Where-Object {
    $_.FullName -notmatch '\\(obj|bin)\\'
}

# Regex for literal lex:Loc keys: {lex:Loc KeyName}
$locRegex = [regex]'\{lex:Loc\s+([A-Za-z0-9_.]+)\s*\}'

$results = @()

foreach ($file in $xamlFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)

    # Skip if no DefaultDictionary="Locales.Lang" and no lex:Loc references
    if ($content -notmatch 'DefaultDictionary="Locales\.Lang"' -and $content -notmatch 'lex:Loc') {
        continue
    }

    # Extract literal lex:Loc keys
    $matches = $locRegex.Matches($content)
    $keyDictCount = @{}
    $unmappedKeys = @()
    $keyLocations = @{}  # key -> dictionary

    foreach ($m in $matches) {
        $key = $m.Groups[1].Value
        if ($keyToDict.ContainsKey($key)) {
            $dict = $keyToDict[$key]
            $keyLocations[$key] = $dict
            if ($keyDictCount.ContainsKey($dict)) {
                $keyDictCount[$dict]++
            } else {
                $keyDictCount[$dict] = 1
            }
        } else {
            $unmappedKeys += $key
        }
    }

    # Determine the best default dictionary (most common)
    $bestDict = ""
    $bestCount = 0
    foreach ($dict in $keyDictCount.Keys) {
        if ($keyDictCount[$dict] -gt $bestCount) {
            $bestCount = $keyDictCount[$dict]
            $bestDict = $dict
        }
    }

    # Find keys NOT in the best dictionary
    $otherDictKeys = @()
    foreach ($key in $keyLocations.Keys) {
        if ($keyLocations[$key] -ne $bestDict) {
            $otherDictKeys += "$key -> $($keyLocations[$key])"
        }
    }

    $relPath = $file.FullName.Substring($root.Length + 1)
    $hasDefaultLang = $content -match 'DefaultDictionary="Locales\.Lang"'

    $results += [PSCustomObject]@{
        File = $relPath
        HasDefaultLang = $hasDefaultLang
        TotalKeys = $matches.Count
        BestDict = $bestDict
        BestCount = $bestCount
        OtherDictKeys = ($otherDictKeys -join "; ")
        UnmappedKeys = ($unmappedKeys -join "; ")
    }
}

$results | Format-Table -AutoSize -Wrap
Write-Output ""
Write-Output "Total XAML files to migrate: $($results.Count)"
