param(
    [int]$Top = 30,
    [switch]$Json,
    [switch]$SummaryOnly,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

if ($SelfTest) {
    $items = New-Object System.Collections.Generic.List[object]
    $items.Add([pscustomobject]@{ Value = 1 })
    $items.Add([pscustomobject]@{ Value = 2 })
    $array = @($items.ToArray())
    if ($array.Count -ne 2) {
        throw "Generic list conversion self-test failed."
    }
    Write-Output "project_cleanliness_self_test: passed"
    exit 0
}

$assetRoot = "Assets/AreaSurvivors"
$allFiles = @(
    Get-ChildItem -LiteralPath $assetRoot -Recurse -File -ErrorAction Stop
)
$metaFiles = @($allFiles | Where-Object { $_.Extension -eq ".meta" })
$payloadFiles = @($allFiles | Where-Object { $_.Extension -ne ".meta" })
$guidMetaRoots = @("Assets", "Packages", "Library/PackageCache") |
    Where-Object { Test-Path -LiteralPath $_ -PathType Container }
$guidMetaFiles = @(
    foreach ($root in $guidMetaRoots) {
        Get-ChildItem -LiteralPath $root -Recurse -File -Filter "*.meta" -ErrorAction Stop
    }
)

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Resolve-Path -LiteralPath $Path -Relative).TrimStart(".", "\", "/").Replace("\", "/")
}

function Get-MetaGuidMap {
    param([System.IO.FileInfo[]]$Files)

    $result = @{}
    foreach ($file in $Files) {
        $guidLine = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -TotalCount 4 |
            Where-Object { $_ -match '^guid:\s*([0-9a-fA-F]{32})\s*$' } |
            Select-Object -First 1
        if ($null -eq $guidLine) { continue }
        $guid = ([regex]::Match($guidLine, '^guid:\s*([0-9a-fA-F]{32})\s*$')).Groups[1].Value.ToLowerInvariant()
        $result[$guid] = Get-RelativePath $file.FullName
    }
    return $result
}

function Get-UnresolvedGuidReferences {
    param(
        [System.IO.FileInfo[]]$Files,
        [hashtable]$KnownGuids
    )

    $textExtensions = @(".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".overrideController", ".physicsMaterial2D")
    $byGuid = @{}
    foreach ($file in $Files) {
        if ($textExtensions -notcontains $file.Extension) { continue }
        $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        foreach ($match in [regex]::Matches($text, 'guid:\s*([0-9a-fA-F]{32})')) {
            $guid = $match.Groups[1].Value.ToLowerInvariant()
            if ($guid -match '^0{16}') { continue }
            if ($KnownGuids.ContainsKey($guid)) { continue }
            if (-not $byGuid.ContainsKey($guid)) {
                $byGuid[$guid] = New-Object System.Collections.Generic.HashSet[string]
            }
            [void]$byGuid[$guid].Add((Get-RelativePath $file.FullName))
        }
    }

    return @(
        foreach ($guid in ($byGuid.Keys | Sort-Object)) {
            [pscustomobject]@{
                Guid = $guid
                ReferenceFiles = @($byGuid[$guid] | Sort-Object)
            }
        }
    )
}

function Get-DuplicateGroups {
    param([System.IO.FileInfo[]]$Files)

    $groups = New-Object System.Collections.Generic.List[object]
    foreach ($lengthGroup in ($Files | Where-Object { $_.Length -gt 0 } | Group-Object Length | Where-Object { $_.Count -gt 1 })) {
        $byHash = @{}
        foreach ($file in $lengthGroup.Group) {
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if (-not $byHash.ContainsKey($hash)) {
                $byHash[$hash] = New-Object System.Collections.Generic.List[object]
            }
            $byHash[$hash].Add($file)
        }

        foreach ($hash in $byHash.Keys) {
            $duplicates = @($byHash[$hash].ToArray())
            if ($duplicates.Count -lt 2) { continue }
            $eachBytes = [long]$duplicates[0].Length
            $importerHashes = @(
                $duplicates |
                    ForEach-Object { Get-NormalizedMetaHash -AssetFile $_ } |
                    Sort-Object -Unique
            )
            $groups.Add([pscustomobject]@{
                Hash = $hash
                Count = $duplicates.Count
                EachKB = [math]::Round($eachBytes / 1KB, 1)
                RedundantKB = [math]::Round(($eachBytes * ($duplicates.Count - 1)) / 1KB, 1)
                Category = Get-DuplicateCategory -Files $duplicates
                ImporterSettingsEqual = ($importerHashes.Count -eq 1 -and $importerHashes[0] -ne "missing-meta")
                Paths = @($duplicates | ForEach-Object { Get-RelativePath $_.FullName } | Sort-Object)
            })
        }
    }

    return @(
        $groups |
            Sort-Object `
                @{ Expression = "RedundantKB"; Descending = $true },
                @{ Expression = "Count"; Descending = $true }
    )
}

function Get-NormalizedMetaHash {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$AssetFile)

    $metaPath = $AssetFile.FullName + ".meta"
    if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
        return "missing-meta"
    }

    $normalized = (
        Get-Content -LiteralPath $metaPath -Encoding UTF8 |
            Where-Object { $_ -notmatch '^(?:guid|timeCreated):' }
    ) -join "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalized)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-DuplicateCategory {
    param([object[]]$Files)

    $paths = @($Files | ForEach-Object { Get-RelativePath $_.FullName })
    if (@($paths | Where-Object { $_ -match '/(?:Archive|Previous[^/]*)/' }).Count -gt 0) {
        return "historical-review"
    }
    $externalCount = @($paths | Where-Object { $_.StartsWith("Assets/AreaSurvivors/Sprites/External/", [System.StringComparison]::Ordinal) }).Count
    $generatedCount = @($paths | Where-Object { $_.StartsWith("Assets/AreaSurvivors/Sprites/Generated/", [System.StringComparison]::Ordinal) }).Count
    if ($externalCount -gt 0 -and $generatedCount -gt 0 -and ($externalCount + $generatedCount) -eq $paths.Count) {
        return "source-generated-preserved"
    }
    if (@($paths | Where-Object {
        $_.StartsWith(
            "Assets/AreaSurvivors/Sprites/Generated/GroundVariants/",
            [System.StringComparison]::Ordinal)
    }).Count -eq $paths.Count) {
        return "ground-variant-semantic-preserved"
    }
    if ($paths.Count -eq 2 -and
        @($paths | Where-Object { $_ -match '/Sprites/Generated/Characters/[^/]+\.png$' }).Count -eq 1 -and
        @($paths | Where-Object { $_ -match '/Sprites/Generated/Walk/[^/]+/Down_1\.png$' }).Count -eq 1) {
        return "standing-walk-keyframe-preserved"
    }
    if ($paths.Count -eq 2 -and
        $paths -contains "Assets/AreaSurvivors/Resources/Audio/SFX/arrow_rain.mp3" -and
        $paths -contains "Assets/AreaSurvivors/Resources/Audio/SFX/arrow_shot.mp3") {
        return "semantic-audio-preserved"
    }
    if ($paths.Count -eq 2 -and
        $paths -contains "Assets/AreaSurvivors/Sprites/Generated/Environment/GroundChunk_Grass_24x24.png" -and
        $paths -contains "Assets/AreaSurvivors/Sprites/Generated/MapChunks/GrassChunk.png") {
        return "ground-chunk-importer-preserved"
    }
    return "internal-review"
}

function Get-HistoricalGroups {
    param([System.IO.FileInfo[]]$Files)

    $items = foreach ($file in $Files) {
        $relative = Get-RelativePath $file.FullName
        $match = [regex]::Match($relative, '^(.*?/(?:Archive|Previous[^/]*|Backups?|Old|Legacy|Temp))(?:/|$)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $match.Success) { continue }
        [pscustomobject]@{
            Group = $match.Groups[1].Value
            Bytes = $file.Length
        }
    }

    return @(
        $items |
            Group-Object Group |
            ForEach-Object {
                $bytes = [long](($_.Group | Measure-Object Bytes -Sum).Sum)
                [pscustomobject]@{
                    Path = $_.Name
                    Files = $_.Count
                    KB = [math]::Round($bytes / 1KB, 1)
                }
            } |
            Sort-Object KB -Descending
    )
}

function Get-CodeDebtFiles {
    $roots = @("Assets/AreaSurvivors", "Tools")
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($root in $roots) {
        foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -in @(".cs", ".ps1") })) {
            if ($file.FullName -match '[\\/]ThirdParty[\\/]') { continue }
            if ($file.FullName -eq $PSCommandPath) { continue }
            $pattern = if ($file.Extension -eq ".ps1") {
                '^\s*#.*\b(TODO|FIXME|HACK)\b'
            } else {
                '//.*\b(TODO|FIXME|HACK)\b'
            }
            $matches = @(Select-String -LiteralPath $file.FullName -Pattern $pattern -AllMatches)
            if ($matches.Count -eq 0) { continue }
            $result.Add([pscustomobject]@{
                Path = Get-RelativePath $file.FullName
                Hits = $matches.Count
            })
        }
    }
    return @(
        $result |
            Sort-Object `
                @{ Expression = "Hits"; Descending = $true },
                @{ Expression = "Path"; Descending = $false }
    )
}

$guidMap = Get-MetaGuidMap -Files $guidMetaFiles
$missingMeta = @(
    $payloadFiles |
        Where-Object { -not (Test-Path -LiteralPath ($_.FullName + ".meta") -PathType Leaf) } |
        ForEach-Object { Get-RelativePath $_.FullName } |
        Sort-Object
)
$orphanMeta = @(
    $metaFiles |
        Where-Object { -not (Test-Path -LiteralPath $_.FullName.Substring(0, $_.FullName.Length - 5)) } |
        ForEach-Object { Get-RelativePath $_.FullName } |
        Sort-Object
)
$unresolvedGuids = @(Get-UnresolvedGuidReferences -Files $payloadFiles -KnownGuids $guidMap)
$duplicateGroups = @(Get-DuplicateGroups -Files $payloadFiles)
$historicalGroups = @(Get-HistoricalGroups -Files $payloadFiles)
$codeDebtFiles = @(Get-CodeDebtFiles)
$reviewDuplicateGroups = @($duplicateGroups | Where-Object { $_.Category -notmatch '-preserved$' })

$result = [pscustomobject]@{
    summary = [pscustomobject]@{
        asset_payload_files = $payloadFiles.Count
        meta_files = $metaFiles.Count
        guid_map_meta_files = $guidMetaFiles.Count
        missing_meta = $missingMeta.Count
        orphan_meta = $orphanMeta.Count
        unresolved_guids = $unresolvedGuids.Count
        duplicate_groups = $duplicateGroups.Count
        duplicate_redundant_kb = [math]::Round((($duplicateGroups | Measure-Object RedundantKB -Sum).Sum), 1)
        review_duplicate_groups = $reviewDuplicateGroups.Count
        review_duplicate_redundant_kb = [math]::Round((($reviewDuplicateGroups | Measure-Object RedundantKB -Sum).Sum), 1)
        historical_groups = $historicalGroups.Count
        code_debt_files = $codeDebtFiles.Count
    }
    missing_meta = @($missingMeta | Select-Object -First $Top)
    orphan_meta = @($orphanMeta | Select-Object -First $Top)
    unresolved_guid_references = @($unresolvedGuids | Select-Object -First $Top)
    duplicate_groups = @($reviewDuplicateGroups | Select-Object -First $Top)
    historical_groups = @($historicalGroups | Select-Object -First $Top)
    code_debt_files = @($codeDebtFiles | Select-Object -First $Top)
}

if ($SummaryOnly) {
    if ($Json) {
        $result.summary | ConvertTo-Json -Depth 4
    } else {
        $result.summary | Format-List
    }
    exit 0
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
    exit 0
}

Write-Output "Project cleanliness summary:"
$result.summary | Format-List
Write-Output ""
Write-Output "Missing meta:"
$result.missing_meta | Format-Table -AutoSize
Write-Output ""
Write-Output "Orphan meta:"
$result.orphan_meta | Format-Table -AutoSize
Write-Output ""
Write-Output "Unresolved GUID references:"
$result.unresolved_guid_references | Format-Table -AutoSize
Write-Output ""
Write-Output "Duplicate payload groups:"
$result.duplicate_groups | Format-Table Category, Count, EachKB, RedundantKB, Paths -AutoSize
Write-Output ""
Write-Output "Historical asset groups:"
$result.historical_groups | Format-Table -AutoSize
Write-Output ""
Write-Output "TODO/FIXME/HACK files:"
$result.code_debt_files | Format-Table -AutoSize
