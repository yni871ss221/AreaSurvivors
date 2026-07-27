param(
    [string]$SessionPath = "",
    [ValidateRange(1, 99)][int]$Stage = 1,
    [ValidateRange(1, 100)][int]$TopFrames = 15,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function New-StagePerformanceDetail {
    param(
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][hashtable]$IncidentReports,
        [Parameter(Mandatory = $true)][int]$StageNumber,
        [Parameter(Mandatory = $true)][int]$TopFrameCount
    )

    $summaries = @(
        $Session.incidents |
            Where-Object { [int]$_.stage -eq $StageNumber } |
            Sort-Object incidentIndex
    )
    $incidentRows = @(
        $summaries |
            ForEach-Object {
                $summary = $_
                $report = $IncidentReports[[string]$summary.fileName]
                $samples = @($report.samples)
                $durationSeconds = if ($samples.Count -gt 1) {
                    [Math]::Max(
                        0.001,
                        [double]$samples[-1].sessionSeconds -
                            [double]$samples[0].sessionSeconds)
                }
                else {
                    0.001
                }
                [PSCustomObject]@{
                    incident_index = [int]$summary.incidentIndex
                    file_name = [string]$summary.fileName
                    duration_seconds = [Math]::Round($durationSeconds, 3)
                    p95_frame_ms = [double]$summary.p95FrameMs
                    max_frame_ms = [double]$summary.maxFrameMs
                    peak_enemies = [int]$summary.peakEnemies
                    active_enemies = [int]$report.activeEnemies
                    active_xp_orbs = [int]$report.activeExperienceOrbs
                    active_token_orbs = [int]$report.activeTokenOrbs
                    weapons = @($report.weapons)
                    projectile_triggers_per_second = [Math]::Round(
                        [double]$summary.projectileTriggerCallbacks / $durationSeconds,
                        2)
                    projectile_candidates_per_second = [Math]::Round(
                        [double]$summary.projectileCandidates / $durationSeconds,
                        2)
                    banana_hits_per_second = [Math]::Round(
                        [double]$summary.bananaProjectileDamageHits / $durationSeconds,
                        2)
                    banana_queries_per_second = [Math]::Round(
                        [double]$summary.bananaOverlapQueries / $durationSeconds,
                        2)
                    banana_candidates_per_second = [Math]::Round(
                        [double]$summary.bananaColliderCandidates / $durationSeconds,
                        2)
                    excalibur_hits_per_second = [Math]::Round(
                        [double]$summary.excaliburProjectileDamageHits / $durationSeconds,
                        2)
                    summoned_enemy_attempts = [long]$summary.summonedEnemySpawnAttempts
                    summoned_enemy_spawns = [long]$summary.summonedEnemySpawns
                    summoned_enemy_cap_blocked = [long]$summary.summonedEnemyCapBlocked
                    popup_created = [long]$summary.popupInstancesCreated
                    popup_reuses = [long]$summary.popupReuses
                    popup_drops = [long]$summary.popupDrops
                    gc_megabytes_per_second = [Math]::Round(
                        ([double]$summary.gcAllocatedBytes / 1MB) / $durationSeconds,
                        2)
                }
            }
    )

    $allFrames = @(
        $summaries |
            ForEach-Object {
                $summary = $_
                $report = $IncidentReports[[string]$summary.fileName]
                @($report.samples) |
                    ForEach-Object {
                        [PSCustomObject]@{
                            incident_index = [int]$summary.incidentIndex
                            session_seconds = [double]$_.sessionSeconds
                            frame_ms = [double]$_.frameMs
                            main_thread_ms = [double]$_.mainThreadMs
                            gc_allocated_bytes = [long]$_.gcAllocatedBytes
                            enemy_count = [int]$_.enemyCount
                            projectile_trigger_callbacks = [long]$_.projectileTriggerCallbacks
                            projectile_candidates = [long]$_.projectileCandidates
                            projectile_damage_hits = [long]$_.projectileDamageHits
                            banana_damage_hits = [long]$_.bananaProjectileDamageHits
                            banana_overlap_queries = [long]$_.bananaOverlapQueries
                            banana_collider_candidates = [long]$_.bananaColliderCandidates
                            excalibur_damage_hits = [long]$_.excaliburProjectileDamageHits
                            summoned_enemy_spawn_attempts = [long]$_.summonedEnemySpawnAttempts
                            summoned_enemy_spawns = [long]$_.summonedEnemySpawns
                            summoned_enemy_cap_blocked = [long]$_.summonedEnemyCapBlocked
                            damage_feedback_events = [long]$_.damageFeedbackEvents
                            popup_spawns = [long]$_.popupSpawns
                            popup_drops = [long]$_.popupDrops
                            active_damage_popups = [int]$_.activeDamagePopups
                            active_hit_flashes = [int]$_.activeHitFlashes
                            enemy_deaths = [long]$_.enemyDeaths
                            xp_orb_spawns = [long]$_.xpOrbSpawns
                        }
                    }
            }
    )

    $deduplicatedFrames = @{}
    foreach ($frame in ($allFrames | Sort-Object frame_ms -Descending)) {
        $key = $frame.session_seconds.ToString("R", [Globalization.CultureInfo]::InvariantCulture)
        if (-not $deduplicatedFrames.ContainsKey($key)) {
            $deduplicatedFrames[$key] = $frame
        }
    }
    $topFrameRows = @(
        $deduplicatedFrames.Values |
            Sort-Object frame_ms -Descending |
            Select-Object -First $TopFrameCount
    )

    return [PSCustomObject]@{
        report = "performance_stage_detail"
        session_id = [string]$Session.sessionId
        stage = $StageNumber
        incident_count = $summaries.Count
        incidents = $incidentRows
        top_frames = $topFrameRows
    }
}

if ($SelfTest) {
    $sessionFixture = [PSCustomObject]@{
        sessionId = "fixture"
        incidents = @(
            [PSCustomObject]@{
                incidentIndex = 1
                stage = 3
                fileName = "incident-001.json"
                p95FrameMs = 40
                maxFrameMs = 80
                peakEnemies = 20
                projectileTriggerCallbacks = 300
                projectileCandidates = 30
                bananaProjectileDamageHits = 10
                bananaOverlapQueries = 20
                bananaColliderCandidates = 80
                excaliburProjectileDamageHits = 5
                summonedEnemySpawnAttempts = 5
                summonedEnemySpawns = 3
                summonedEnemyCapBlocked = 2
                popupInstancesCreated = 2
                popupReuses = 8
                popupDrops = 1
                gcAllocatedBytes = 1048576
            }
        )
    }
    $incidentFixture = [PSCustomObject]@{
        activeEnemies = 18
        activeExperienceOrbs = 12
        activeTokenOrbs = 2
        weapons = @("BoomerangSword:Lv11")
        samples = @(
            [PSCustomObject]@{
                sessionSeconds = 10
                frameMs = 20
                mainThreadMs = 19
                bananaOverlapQueries = 1
                bananaColliderCandidates = 4
                summonedEnemySpawnAttempts = 1
                summonedEnemySpawns = 1
                summonedEnemyCapBlocked = 0
            },
            [PSCustomObject]@{
                sessionSeconds = 12
                frameMs = 80
                mainThreadMs = 79
                bananaOverlapQueries = 2
                bananaColliderCandidates = 8
                summonedEnemySpawnAttempts = 4
                summonedEnemySpawns = 2
                summonedEnemyCapBlocked = 2
            }
        )
    }
    $fixtureReports = @{ "incident-001.json" = $incidentFixture }
    $fixtureResult = New-StagePerformanceDetail `
        -Session $sessionFixture `
        -IncidentReports $fixtureReports `
        -StageNumber 3 `
        -TopFrameCount 1
    if ($fixtureResult.incident_count -ne 1 -or
        $fixtureResult.incidents[0].projectile_triggers_per_second -ne 150 -or
        $fixtureResult.incidents[0].banana_queries_per_second -ne 10 -or
        $fixtureResult.incidents[0].banana_candidates_per_second -ne 40 -or
        $fixtureResult.incidents[0].summoned_enemy_cap_blocked -ne 2 -or
        $fixtureResult.top_frames[0].banana_overlap_queries -ne 2 -or
        $fixtureResult.top_frames[0].frame_ms -ne 80) {
        throw "performance-stage-detail-report self-test failed."
    }
    Write-Output "performance_stage_detail_report_self_test: passed"
    exit 0
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sessionsRoot = Join-Path $projectRoot "TokenReports\PerformanceSessions"
if ([string]::IsNullOrWhiteSpace($SessionPath)) {
    $latestPath = Join-Path $sessionsRoot "latest-session.txt"
    if (-not (Test-Path -LiteralPath $latestPath -PathType Leaf)) {
        throw "Latest performance session pointer is missing: $latestPath"
    }
    $SessionPath = (Get-Content -LiteralPath $latestPath -Raw -Encoding UTF8).Trim()
}
if (Test-Path -LiteralPath $SessionPath -PathType Container) {
    $SessionPath = Join-Path $SessionPath "session.json"
}
if (-not (Test-Path -LiteralPath $SessionPath -PathType Leaf)) {
    throw "Performance session JSON was not found: $SessionPath"
}

$resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath).Path
$session = Get-Content -LiteralPath $resolvedSessionPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$sessionDirectory = Split-Path $resolvedSessionPath -Parent
$incidentReports = @{}
foreach ($summary in @($session.incidents | Where-Object { [int]$_.stage -eq $Stage })) {
    $incidentPath = Join-Path $sessionDirectory ([string]$summary.fileName)
    if (-not (Test-Path -LiteralPath $incidentPath -PathType Leaf)) {
        throw "Incident report is missing: $incidentPath"
    }
    $incidentReports[[string]$summary.fileName] =
        Get-Content -LiteralPath $incidentPath -Raw -Encoding UTF8 |
            ConvertFrom-Json
}

$result = New-StagePerformanceDetail `
    -Session $session `
    -IncidentReports $incidentReports `
    -StageNumber $Stage `
    -TopFrameCount $TopFrames
$result | ConvertTo-Json -Depth 7
