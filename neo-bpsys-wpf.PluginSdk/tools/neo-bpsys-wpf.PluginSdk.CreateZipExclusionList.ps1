param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectAssetsFile,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$ExcludedRoots,

    [Parameter(Mandatory = $true)]
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

function Get-PropertyNames {
    param([object]$Object)

    if ($null -eq $Object) {
        return @()
    }

    if ($Object -is [System.Collections.IDictionary]) {
        return @($Object.Keys)
    }

    return @($Object.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    if ($Object -is [System.Collections.IDictionary]) {
        return $Object[$Name]
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) {
        return $property.Value
    }

    return $null
}

function Get-LibraryNameFromKey {
    param([string]$Key)

    if ($Key -match "^(?<name>.+?)/") {
        return $Matches["name"]
    }

    return $Key
}

function Get-FirstPropertyValue {
    param([object]$Object)

    foreach ($name in Get-PropertyNames $Object) {
        return Get-PropertyValue $Object $name
    }

    return $null
}

function Get-DirectDependencyNames {
    param([object]$Assets)

    $result = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $groups = Get-PropertyValue $Assets "projectFileDependencyGroups"

    foreach ($frameworkName in Get-PropertyNames $groups) {
        $entries = @(Get-PropertyValue $groups $frameworkName)
        foreach ($entry in $entries) {
            if ([string]::IsNullOrWhiteSpace($entry)) {
                continue
            }

            $dependencyName = ($entry -split "\s+", 2)[0]
            if (-not [string]::IsNullOrWhiteSpace($dependencyName)) {
                [void]$result.Add($dependencyName)
            }
        }

        break
    }

    return $result
}

function Get-ReachableLibraries {
    param(
        [hashtable]$LibrariesByKey,
        [hashtable]$KeysByLibraryName,
        [string[]]$RootNames
    )

    $visited = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $queue = New-Object System.Collections.Generic.Queue[string]

    foreach ($rootName in $RootNames) {
        if ([string]::IsNullOrWhiteSpace($rootName)) {
            continue
        }

        if (-not $KeysByLibraryName.ContainsKey($rootName)) {
            continue
        }

        foreach ($key in $KeysByLibraryName[$rootName]) {
            if ($visited.Add($key)) {
                $queue.Enqueue($key)
            }
        }
    }

    while ($queue.Count -gt 0) {
        $key = $queue.Dequeue()
        $library = $LibrariesByKey[$key]
        $dependencies = Get-PropertyValue $library "dependencies"

        foreach ($dependencyName in Get-PropertyNames $dependencies) {
            if (-not $KeysByLibraryName.ContainsKey($dependencyName)) {
                continue
            }

            foreach ($dependencyKey in $KeysByLibraryName[$dependencyName]) {
                if ($visited.Add($dependencyKey)) {
                    $queue.Enqueue($dependencyKey)
                }
            }
        }
    }

    return ,$visited
}

function Add-PathIfExists {
    param(
        [System.Collections.Generic.HashSet[string]]$Paths,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    if (Test-Path -LiteralPath $Path) {
        [void]$Paths.Add((Resolve-Path -LiteralPath $Path).Path)
    }
}

function Add-RelatedPublishFiles {
    param(
        [System.Collections.Generic.HashSet[string]]$Paths,
        [string]$FilePath
    )

    $extension = [System.IO.Path]::GetExtension($FilePath)
    if ([string]::IsNullOrWhiteSpace($extension)) {
        return
    }

    foreach ($sidecarExtension in @(".pdb", ".xml")) {
        if ($sidecarExtension.Equals($extension, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $sidecarPath = [System.IO.Path]::ChangeExtension($FilePath, $sidecarExtension)
        Add-PathIfExists -Paths $Paths -Path $sidecarPath
    }
}

function Add-PublishAsset {
    param(
        [System.Collections.Generic.HashSet[string]]$Paths,
        [string]$PublishDirPath,
        [string]$SectionName,
        [string]$AssetRelativePath
    )

    if ([string]::IsNullOrWhiteSpace($AssetRelativePath)) {
        return
    }

    $normalizedPublishDir = $PublishDirPath
    if (-not $normalizedPublishDir.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $normalizedPublishDir += [System.IO.Path]::DirectorySeparatorChar
    }

    $publishPath = $null
    if ($AssetRelativePath.StartsWith("bin/placeholder/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $publishPath = Join-Path $normalizedPublishDir ([System.IO.Path]::GetFileName($AssetRelativePath))
    }
    elseif ($SectionName -eq "runtime") {
        $publishPath = Join-Path $normalizedPublishDir ([System.IO.Path]::GetFileName($AssetRelativePath))
    }
    else {
        $relativePath = $AssetRelativePath.Replace("/", "\")
        $publishPath = Join-Path $normalizedPublishDir $relativePath
    }

    Add-PathIfExists -Paths $Paths -Path $publishPath
    Add-RelatedPublishFiles -Paths $Paths -FilePath $publishPath
}

$assets = Get-Content -LiteralPath $ProjectAssetsFile -Raw | ConvertFrom-Json
$targetLibraries = Get-FirstPropertyValue (Get-PropertyValue $assets "targets")

if ($null -eq $targetLibraries) {
    throw "project.assets.json does not contain a usable targets node: $ProjectAssetsFile"
}

$librariesByKey = @{}
$keysByLibraryName = @{}

foreach ($libraryKey in Get-PropertyNames $targetLibraries) {
    $library = Get-PropertyValue $targetLibraries $libraryKey
    $librariesByKey[$libraryKey] = $library

    $libraryName = Get-LibraryNameFromKey $libraryKey
    if (-not $keysByLibraryName.ContainsKey($libraryName)) {
        $keysByLibraryName[$libraryName] = New-Object System.Collections.Generic.List[string]
    }

    $keysByLibraryName[$libraryName].Add($libraryKey)
}

$excludedRootNames = @(
    $ExcludedRoots.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries) |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -ne "" }
)

$directDependencyNames = Get-DirectDependencyNames -Assets $assets
$preservedRootNames = New-Object System.Collections.Generic.List[string]

foreach ($dependencyName in $directDependencyNames) {
    if ($excludedRootNames -contains $dependencyName) {
        continue
    }

    $preservedRootNames.Add($dependencyName)
}

$excludedLibraryKeys = Get-ReachableLibraries -LibrariesByKey $librariesByKey -KeysByLibraryName $keysByLibraryName -RootNames $excludedRootNames
$preservedLibraryKeys = Get-ReachableLibraries -LibrariesByKey $librariesByKey -KeysByLibraryName $keysByLibraryName -RootNames $preservedRootNames.ToArray()

$finalExcludedLibraryKeys = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($libraryKey in $excludedLibraryKeys) {
    if (-not $preservedLibraryKeys.Contains($libraryKey)) {
        [void]$finalExcludedLibraryKeys.Add($libraryKey)
    }
}

$publishFilesToExclude = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($libraryKey in $finalExcludedLibraryKeys) {
    $library = $librariesByKey[$libraryKey]
    foreach ($sectionName in @("runtime", "runtimeTargets", "native", "resources")) {
        $section = Get-PropertyValue $library $sectionName
        foreach ($assetRelativePath in Get-PropertyNames $section) {
            Add-PublishAsset -Paths $publishFilesToExclude -PublishDirPath $PublishDir -SectionName $sectionName -AssetRelativePath $assetRelativePath
        }
    }
}

$outputDirectory = Split-Path -Parent $OutputFile
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

if ($publishFilesToExclude.Count -eq 0) {
    Set-Content -LiteralPath $OutputFile -Value @() -Encoding ASCII
    return
}

$publishFilesToExclude |
    Sort-Object |
    Set-Content -LiteralPath $OutputFile -Encoding ASCII
