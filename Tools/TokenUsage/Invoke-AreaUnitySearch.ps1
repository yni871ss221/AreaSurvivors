param(
    [Parameter(Mandatory = $true)]
    [string]$Query,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

if ($Query.Contains("`r") -or $Query.Contains("`n")) {
    throw "Scene/prefab search query must be a single line."
}
if ($Query.Length -eq 0 -or $Query -ne $Query.Trim()) {
    throw "guard_code: 40; Scene/prefab search query must be non-empty and must not have leading or trailing whitespace."
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$queryDirectory = Join-Path $projectRoot "Temp\AreaSurvivors"
$queryPath = Join-Path $queryDirectory "scene-prefab-search-query.txt"
$reportDirectory = Join-Path $projectRoot "TokenReports\UnityReports"
[System.IO.Directory]::CreateDirectory($queryDirectory) | Out-Null

$mutex = New-Object System.Threading.Mutex($false, "AreaSurvivors.ScenePrefabSearch")
$lockTaken = $false
try {
    try {
        $lockTaken = $mutex.WaitOne(120000)
    } catch [System.Threading.AbandonedMutexException] {
        $lockTaken = $true
    }
    if (-not $lockTaken) {
        throw "guard_code: 29; Scene/prefab search lock timed out after 120 seconds."
    }

    $statusOutput = @(& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action PlayStatus -PrintOutput)
    if ($LASTEXITCODE -ne 0) {
        throw "guard_code: 31; Scene/prefab search could not confirm Play Mode status."
    }
    $statusText = $statusOutput -join "`n"
    if ($statusText -match 'isPlaying:\s*True') {
        throw "guard_code: 32; Scene/prefab search is disabled while Unity is in Play Mode. Exit Play Mode before running the Reporter."
    }
    if ($statusText -notmatch 'isPlaying:\s*False') {
        throw "guard_code: 31; Scene/prefab search received an unrecognized Play Mode status."
    }

    $before = @{}
    Get-ChildItem -LiteralPath $reportDirectory -Filter "scene-prefab-search-*.md" -File -ErrorAction SilentlyContinue | ForEach-Object {
        $before[$_.FullName] = "{0}:{1}" -f $_.LastWriteTimeUtc.Ticks, $_.Length
    }

    try {
        [System.IO.File]::WriteAllText($queryPath, $Query, [System.Text.UTF8Encoding]::new($false))
        & "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action Menu -MenuPath "Area Survivors/Reports/Scene Prefab Search" -PrintOutput:$PrintOutput
        if ($LASTEXITCODE -ne 0) {
            throw "Scene/prefab search reporter failed with exit code $LASTEXITCODE."
        }

        $latest = $null
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $reports = Get-ChildItem -LiteralPath $reportDirectory -Filter "scene-prefab-search-*.md" -File -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTimeUtc -Descending
            foreach ($report in $reports) {
                $signature = "{0}:{1}" -f $report.LastWriteTimeUtc.Ticks, $report.Length
                if ($before.ContainsKey($report.FullName) -and $before[$report.FullName] -eq $signature) { continue }
                $header = @(Get-Content -LiteralPath $report.FullName -Encoding UTF8 -TotalCount 4)
                if ($header -contains ("Query: {0}" -f $Query)) {
                    $latest = $report
                    break
                }
            }
            if ($null -ne $latest) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
    } finally {
        Remove-Item -LiteralPath $queryPath -Force -ErrorAction SilentlyContinue
    }

    if ($null -eq $latest) {
        throw "guard_code: 30; Scene/prefab search produced no new report whose Query header exactly matched within 10 seconds: $Query"
    }

    Write-Output ("search_query: {0}" -f $Query)
    Write-Output ("report: {0}" -f $latest.FullName)
    Write-Output ("bytes: {0}" -f $latest.Length)
} finally {
    if ($lockTaken) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
