[CmdletBinding()]
param(
    [ValidateSet("Query", "Store", "Stats", "SelfTest")]
    [string]$Action = "Query",
    [string]$Path = "",
    [string]$ExpectedHash = "",
    [string]$Purpose = "",
    [string]$Flow = "",
    [string[]]$Invariants = @(),
    [string[]]$SideEffects = @(),
    [string[]]$Verification = @(),
    [ValidateRange(1, 50)][int]$Top = 10,
    [switch]$NoUsageTracking,
    [string]$ProjectRootOverride = "",
    [string]$CacheRootOverride = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$schemaVersion = 1
$profileVersion = 1
$scriptPath = $PSCommandPath
$projectRoot = if ([string]::IsNullOrWhiteSpace($ProjectRootOverride)) {
    (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
} else {
    [System.IO.Path]::GetFullPath($ProjectRootOverride)
}
$cacheRoot = if ([string]::IsNullOrWhiteSpace($CacheRootOverride)) {
    Join-Path $projectRoot "Library\AreaAgentIndex\SemanticSummaries"
} else {
    [System.IO.Path]::GetFullPath($CacheRootOverride)
}
$entryRoot = Join-Path $cacheRoot "Entries"
$usagePath = Join-Path $cacheRoot "usage.json"
$mutexName = "Local\AreaSurvivorsSemanticSummaryCache"

function Get-BytesHash {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($algorithm.ComputeHash($Bytes) |
                    ForEach-Object { $_.ToString("x2") }) -join "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-StringHash {
    param([Parameter(Mandatory = $true)][string]$Text)

    return Get-BytesHash -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Text))
}

function Get-FileHash {
    param([Parameter(Mandatory = $true)][string]$AbsolutePath)

    $stream = [System.IO.File]::OpenRead($AbsolutePath)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($algorithm.ComputeHash($stream) |
                    ForEach-Object { $_.ToString("x2") }) -join "")
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Resolve-SummarySource {
    param([Parameter(Mandatory = $true)][string]$RepoPath)

    if ([string]::IsNullOrWhiteSpace($RepoPath) -or
        [System.IO.Path]::IsPathRooted($RepoPath)) {
        throw "Summary Path must be a project-relative C# or PowerShell path."
    }
    $absolutePath = [System.IO.Path]::GetFullPath(
        (Join-Path $projectRoot $RepoPath)
    )
    $projectPrefix =
        $projectRoot.TrimEnd("\", "/") +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolutePath.StartsWith(
            $projectPrefix,
            [System.StringComparison]::OrdinalIgnoreCase
        )) {
        throw "Summary Path escaped the project root: $RepoPath"
    }
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        throw "Summary source file was not found: $RepoPath"
    }

    $extension = [System.IO.Path]::GetExtension($absolutePath).ToLowerInvariant()
    $assetsPrefix = (
        [System.IO.Path]::GetFullPath(
            (Join-Path $projectRoot "Assets\AreaSurvivors")
        ).TrimEnd("\", "/") +
        [System.IO.Path]::DirectorySeparatorChar
    )
    $toolsPrefix = (
        [System.IO.Path]::GetFullPath(
            (Join-Path $projectRoot "Tools")
        ).TrimEnd("\", "/") +
        [System.IO.Path]::DirectorySeparatorChar
    )
    $allowed = (
        $extension -eq ".cs" -and
        $absolutePath.StartsWith(
            $assetsPrefix,
            [System.StringComparison]::OrdinalIgnoreCase
        )
    ) -or (
        $extension -eq ".ps1" -and
        $absolutePath.StartsWith(
            $toolsPrefix,
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    if (-not $allowed) {
        throw "Semantic summaries are limited to AreaSurvivors C# and Tools PowerShell files: $RepoPath"
    }

    $relativePath = $absolutePath.Substring($projectPrefix.Length).
        Replace("\", "/")
    return [pscustomobject]@{
        absolute_path = $absolutePath
        relative_path = $relativePath
        content_hash = Get-FileHash -AbsolutePath $absolutePath
        entry_path = Join-Path $entryRoot (
            (Get-StringHash -Text $relativePath.ToLowerInvariant()) + ".json"
        )
    }
}

function Write-JsonAtomic {
    param(
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $parent = Split-Path $TargetPath -Parent
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $temporaryPath = $TargetPath + "." + [guid]::NewGuid().ToString("N") + ".tmp"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        ($Value | ConvertTo-Json -Depth 8),
        $utf8NoBom
    )
    Move-Item -LiteralPath $temporaryPath -Destination $TargetPath -Force
}

function Read-Usage {
    if (-not (Test-Path -LiteralPath $usagePath -PathType Leaf)) {
        return [pscustomobject]@{
            schema_version = $schemaVersion
            hits = 0
            misses = 0
            stores = 0
            invalidations = 0
            paths = @()
        }
    }
    try {
        $usage = Get-Content -LiteralPath $usagePath -Raw -Encoding UTF8 |
            ConvertFrom-Json
        if ([int]$usage.schema_version -ne $schemaVersion) {
            throw "schema mismatch"
        }
        return $usage
    } catch {
        return [pscustomobject]@{
            schema_version = $schemaVersion
            hits = 0
            misses = 0
            stores = 0
            invalidations = 0
            paths = @()
        }
    }
}

function Update-Usage {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [ValidateSet("hit", "miss", "store", "invalidation")]
        [string]$Kind
    )

    $mutex = New-Object System.Threading.Mutex($false, $mutexName)
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(10000)
        if (-not $acquired) {
            throw "Timed out waiting for semantic summary cache mutex."
        }
        $usage = Read-Usage
        $records = @($usage.paths)
        $record = @(
            $records |
                Where-Object { [string]$_.path -eq $RelativePath } |
                Select-Object -First 1
        )
        if ($record.Count -eq 0) {
            $currentRecord = [pscustomobject]@{
                path = $RelativePath
                hits = 0
                misses = 0
                stores = 0
                invalidations = 0
                last_used_at = ""
            }
            $records += $currentRecord
        } else {
            $currentRecord = $record[0]
        }

        switch ($Kind) {
            "hit" {
                $usage.hits = [int]$usage.hits + 1
                $currentRecord.hits = [int]$currentRecord.hits + 1
            }
            "miss" {
                $usage.misses = [int]$usage.misses + 1
                $currentRecord.misses = [int]$currentRecord.misses + 1
            }
            "store" {
                $usage.stores = [int]$usage.stores + 1
                $currentRecord.stores = [int]$currentRecord.stores + 1
            }
            "invalidation" {
                $usage.invalidations = [int]$usage.invalidations + 1
                $currentRecord.invalidations =
                    [int]$currentRecord.invalidations + 1
            }
        }
        $currentRecord.last_used_at = [DateTime]::UtcNow.ToString("o")
        $usage.paths = @($records)
        Write-JsonAtomic -TargetPath $usagePath -Value $usage
    } finally {
        if ($acquired) {
            [void]$mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
}

function ConvertTo-SummaryItems {
    param(
        [string[]]$Values,
        [int]$MaximumCount,
        [string]$Name
    )

    $items = @(
        @(
            foreach ($value in @($Values)) {
                foreach ($part in @([string]$value -split ";")) {
                    $trimmed = $part.Trim()
                    if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                        $trimmed
                    }
                }
            }
        ) | Select-Object -Unique
    )
    if ($items.Count -gt $MaximumCount) {
        throw "$Name accepts at most $MaximumCount items."
    }
    foreach ($item in $items) {
        if ($item.Length -gt 240) {
            throw "$Name item exceeds 240 characters."
        }
    }
    return ,@($items)
}

function Read-ValidEntry {
    param([Parameter(Mandatory = $true)][pscustomobject]$Source)

    if (-not (Test-Path -LiteralPath $Source.entry_path -PathType Leaf)) {
        return [pscustomobject]@{ entry = $null; reason = "missing" }
    }
    try {
        $entry = Get-Content -LiteralPath $Source.entry_path -Raw -Encoding UTF8 |
            ConvertFrom-Json
    } catch {
        return [pscustomobject]@{ entry = $null; reason = "invalid_json" }
    }
    if ([int]$entry.schema_version -ne $schemaVersion -or
        [int]$entry.profile_version -ne $profileVersion) {
        return [pscustomobject]@{ entry = $null; reason = "profile_changed" }
    }
    if ([string]$entry.path -ne [string]$Source.relative_path) {
        return [pscustomobject]@{ entry = $null; reason = "path_changed" }
    }
    if ([string]$entry.content_sha256 -ne [string]$Source.content_hash) {
        return [pscustomobject]@{ entry = $null; reason = "content_changed" }
    }
    return [pscustomobject]@{ entry = $entry; reason = "" }
}

function Invoke-Query {
    param([string]$RepoPath)

    $source = Resolve-SummarySource -RepoPath $RepoPath
    $result = Read-ValidEntry -Source $source
    if ($null -eq $result.entry) {
        if (-not $NoUsageTracking) {
            Update-Usage -RelativePath $source.relative_path -Kind "miss"
            if ($result.reason -ne "missing") {
                Update-Usage -RelativePath $source.relative_path -Kind "invalidation"
            }
        }
        Write-Output (
            "semantic_summary_cache: miss; content_sha256={0}; reason={1}" -f
            $source.content_hash,
            $result.reason
        )
        return
    }

    if (-not $NoUsageTracking) {
        Update-Usage -RelativePath $source.relative_path -Kind "hit"
    }
    $entry = $result.entry
    Write-Output (
        "semantic_summary_cache: hit; content_sha256={0}; profile_version={1}" -f
        $source.content_hash,
        $profileVersion
    )
    Write-Output "semantic_purpose: $($entry.purpose)"
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.flow)) {
        Write-Output "semantic_flow: $($entry.flow)"
    }
    foreach ($item in @($entry.invariants)) {
        Write-Output "semantic_invariant: $item"
    }
    foreach ($item in @($entry.side_effects)) {
        Write-Output "semantic_side_effect: $item"
    }
    foreach ($item in @($entry.verification)) {
        Write-Output "semantic_verification: $item"
    }
}

function Invoke-Store {
    param([string]$RepoPath)

    $source = Resolve-SummarySource -RepoPath $RepoPath
    if ($ExpectedHash -notmatch "^[a-fA-F0-9]{64}$") {
        throw "ExpectedHash must be a SHA-256 hexadecimal value."
    }
    if ($source.content_hash -ne $ExpectedHash.ToLowerInvariant()) {
        throw (
            "Summary source changed after it was understood. expected={0}; actual={1}" -f
            $ExpectedHash.ToLowerInvariant(),
            $source.content_hash
        )
    }
    $normalizedPurpose = $Purpose.Trim()
    $normalizedFlow = $Flow.Trim()
    if ([string]::IsNullOrWhiteSpace($normalizedPurpose) -or
        $normalizedPurpose.Length -gt 300) {
        throw "Purpose is required and must not exceed 300 characters."
    }
    if ($normalizedFlow.Length -gt 600) {
        throw "Flow must not exceed 600 characters."
    }
    $normalizedInvariants = ConvertTo-SummaryItems `
        -Values $Invariants `
        -MaximumCount 5 `
        -Name "Invariants"
    $normalizedSideEffects = ConvertTo-SummaryItems `
        -Values $SideEffects `
        -MaximumCount 5 `
        -Name "SideEffects"
    $normalizedVerification = ConvertTo-SummaryItems `
        -Values $Verification `
        -MaximumCount 3 `
        -Name "Verification"

    $entry = [pscustomobject][ordered]@{
        schema_version = $schemaVersion
        profile_version = $profileVersion
        path = $source.relative_path
        content_sha256 = $source.content_hash
        created_at = [DateTime]::UtcNow.ToString("o")
        purpose = $normalizedPurpose
        flow = $normalizedFlow
        invariants = $normalizedInvariants
        side_effects = $normalizedSideEffects
        verification = $normalizedVerification
    }
    $mutex = New-Object System.Threading.Mutex($false, $mutexName)
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(10000)
        if (-not $acquired) {
            throw "Timed out waiting for semantic summary cache mutex."
        }
        Write-JsonAtomic -TargetPath $source.entry_path -Value $entry
    } finally {
        if ($acquired) {
            [void]$mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
    Update-Usage -RelativePath $source.relative_path -Kind "store"
    Write-Output (
        "semantic_summary_store: success; path={0}; content_sha256={1}" -f
        $source.relative_path,
        $source.content_hash
    )
}

function Invoke-Stats {
    $usage = Read-Usage
    $requests = [int]$usage.hits + [int]$usage.misses
    $hitRate = if ($requests -eq 0) {
        0
    } else {
        [math]::Round(100.0 * [int]$usage.hits / $requests, 1)
    }
    $entryCount = @(
        if (Test-Path -LiteralPath $entryRoot -PathType Container) {
            Get-ChildItem -LiteralPath $entryRoot -Filter "*.json" -File
        }
    ).Count
    Write-Output (
        "semantic_summary_stats: hits={0}; misses={1}; hit_rate={2}%; stores={3}; invalidations={4}" -f
        $usage.hits,
        $usage.misses,
        $hitRate,
        $usage.stores,
        $usage.invalidations
    )
    Write-Output "summary_cache_entries: $entryCount"
    foreach ($record in @(
            $usage.paths |
                Sort-Object `
                    @{ Expression = { [int]$_.hits }; Descending = $true },
                    @{ Expression = { [int]$_.misses }; Descending = $true } |
                Select-Object -First $Top
        )) {
        Write-Output (
            "semantic_summary_path: path={0}; hits={1}; misses={2}; stores={3}; invalidations={4}" -f
            $record.path,
            $record.hits,
            $record.misses,
            $record.stores,
            $record.invalidations
        )
    }
}

function Invoke-SelfTest {
    $temporaryRoot = Join-Path (
        [System.IO.Path]::GetTempPath()
    ) ("area-summary-selftest-" + [guid]::NewGuid().ToString("N"))
    $temporaryCache = Join-Path $temporaryRoot "Cache"
    $fixturePath = Join-Path $temporaryRoot "Tools\Fixture.ps1"
    try {
        New-Item -ItemType Directory -Path (Split-Path $fixturePath -Parent) `
            -Force |
            Out-Null
        [System.IO.File]::WriteAllText(
            $fixturePath,
            "function Invoke-Fixture { 'v1' }",
            (New-Object System.Text.UTF8Encoding($false))
        )
        $missOutput = @(
            & $scriptPath `
                -Action Query `
                -Path "Tools/Fixture.ps1" `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache
        ) -join "`n"
        $hashMatch = [regex]::Match(
            $missOutput,
            "content_sha256=([a-f0-9]{64})"
        )
        if (-not $hashMatch.Success -or $missOutput -notmatch "reason=missing") {
            throw "Semantic summary cache did not report a hashed miss."
        }
        $fixtureHash = $hashMatch.Groups[1].Value
        & $scriptPath `
            -Action Store `
            -Path "Tools/Fixture.ps1" `
            -ExpectedHash $fixtureHash `
            -Purpose "Fixture purpose." `
            -Flow "Query, store, and invalidate." `
            -Invariants "Hash must match." `
            -SideEffects "Writes generated cache only." `
            -Verification "Self-test." `
            -ProjectRootOverride $temporaryRoot `
            -CacheRootOverride $temporaryCache |
            Out-Null
        $hitOutput = @(
            & $scriptPath `
                -Action Query `
                -Path "Tools/Fixture.ps1" `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache
        ) -join "`n"
        if ($hitOutput -notmatch "semantic_summary_cache: hit" -or
            $hitOutput -notmatch "semantic_purpose: Fixture purpose") {
            throw "Semantic summary cache did not return the stored entry."
        }

        [System.IO.File]::WriteAllText(
            $fixturePath,
            "function Invoke-Fixture { 'v2' }",
            (New-Object System.Text.UTF8Encoding($false))
        )
        $changedOutput = @(
            & $scriptPath `
                -Action Query `
                -Path "Tools/Fixture.ps1" `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache
        ) -join "`n"
        if ($changedOutput -notmatch "reason=content_changed") {
            throw "Semantic summary cache did not invalidate changed content."
        }
        $changedHashMatch = [regex]::Match(
            $changedOutput,
            "content_sha256=([a-f0-9]{64})"
        )
        $lengthRejected = $false
        try {
            & $scriptPath `
                -Action Store `
                -Path "Tools/Fixture.ps1" `
                -ExpectedHash $changedHashMatch.Groups[1].Value `
                -Purpose ("x" * 301) `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache |
                Out-Null
        } catch {
            $lengthRejected = $_.Exception.Message -match
                "must not exceed 300"
        }
        if (-not $lengthRejected) {
            throw "Semantic summary cache accepted an oversized purpose."
        }

        $hashRejected = $false
        try {
            & $scriptPath `
                -Action Store `
                -Path "Tools/Fixture.ps1" `
                -ExpectedHash ("0" * 64) `
                -Purpose "Must be rejected." `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache |
                Out-Null
        } catch {
            $hashRejected = $_.Exception.Message -match
                "changed after it was understood"
        }
        if (-not $hashRejected) {
            throw "Semantic summary cache accepted a stale ExpectedHash."
        }

        $statsOutput = @(
            & $scriptPath `
                -Action Stats `
                -ProjectRootOverride $temporaryRoot `
                -CacheRootOverride $temporaryCache
        ) -join "`n"
        if ($statsOutput -notmatch "hits=1" -or
            $statsOutput -notmatch "invalidations=1") {
            throw "Semantic summary cache usage stats are invalid."
        }
    } finally {
        $temporaryPrefix = [System.IO.Path]::GetFullPath(
            [System.IO.Path]::GetTempPath()
        ).TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
        if ($resolvedTemporaryRoot.StartsWith(
                $temporaryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase
            ) -and
            [System.IO.Path]::GetFileName($resolvedTemporaryRoot).StartsWith(
                "area-summary-selftest-",
                [System.StringComparison]::Ordinal
            ) -and
            [System.IO.Directory]::Exists($resolvedTemporaryRoot)) {
            [System.IO.Directory]::Delete($resolvedTemporaryRoot, $true)
        }
    }
    Write-Output "semantic_summary_cache_self_test: passed"
}

switch ($Action) {
    "Query" {
        if ([string]::IsNullOrWhiteSpace($Path)) {
            throw "Query requires Path."
        }
        Invoke-Query -RepoPath $Path
    }
    "Store" {
        if ([string]::IsNullOrWhiteSpace($Path)) {
            throw "Store requires Path."
        }
        Invoke-Store -RepoPath $Path
    }
    "Stats" {
        Invoke-Stats
    }
    "SelfTest" {
        Invoke-SelfTest
    }
}
