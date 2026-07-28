param(
    [string]$SessionPath = "",
    [ValidateRange(1, 100)][int]$Top = 10,
    [switch]$Json,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function New-PerformanceSessionSummary {
    param(
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][string]$ResolvedSessionPath,
        [Parameter(Mandatory = $true)][int]$TopCount
    )

    $incidents = @($Session.incidents)
    $sortedIncidents = @(
        $incidents |
            Sort-Object `
                @{ Expression = { [double]$_.p95FrameMs }; Descending = $true },
                @{ Expression = { [double]$_.maxFrameMs }; Descending = $true } |
            Select-Object -First $TopCount |
            ForEach-Object {
                [PSCustomObject]@{
                    incident_index = [int]$_.incidentIndex
                    reason = [string]$_.reason
                    reason_category = [string]$_.reasonCategory
                    stage = [int]$_.stage
                    game_elapsed_seconds = [double]$_.gameElapsedSeconds
                    p95_frame_ms = [double]$_.p95FrameMs
                    p99_frame_ms = [double]$_.p99FrameMs
                    max_frame_ms = [double]$_.maxFrameMs
                    frames_over_50_ms = [int]$_.framesOver50Ms
                    frames_over_100_ms = [int]$_.framesOver100Ms
                    peak_enemies = [int]$_.peakEnemies
                    gc_allocated_bytes = [long]$_.gcAllocatedBytes
                    gc_collections = [int]$_.gcCollections
                    area_candidates = [long]$_.areaCandidates
                    projectile_candidates = [long]$_.projectileCandidates
                    projectile_trigger_callbacks = [long]$_.projectileTriggerCallbacks
                    projectile_damage_hits = [long]$_.projectileDamageHits
                    excalibur_damage_hits = [long]$_.excaliburProjectileDamageHits
                    banana_damage_hits = [long]$_.bananaProjectileDamageHits
                    damage_feedback_events = [long]$_.damageFeedbackEvents
                    popup_requests = [long]$_.popupRequests
                    popup_spawns = [long]$_.popupSpawns
                    popup_instances_created = [long]$_.popupInstancesCreated
                    popup_reuses = [long]$_.popupReuses
                    popup_drops = [long]$_.popupDrops
                    peak_active_damage_popups = [int]$_.peakActiveDamagePopups
                    hit_flash_requests = [long]$_.hitFlashRequests
                    hit_flash_coalesced_requests = [long]$_.hitFlashCoalescedRequests
                    peak_active_hit_flashes = [int]$_.peakActiveHitFlashes
                    enemy_deaths = [long]$_.enemyDeaths
                    pickup_proximity_scans = [long]$_.pickupProximityScans
                    pickup_scan_candidates = [long]$_.pickupScanCandidates
                    pickup_attractions_started = [long]$_.pickupAttractionsStarted
                    pickup_movement_ticks = [long]$_.pickupMovementTicks
                    banana_overlap_queries = [long]$_.bananaOverlapQueries
                    banana_collider_candidates = [long]$_.bananaColliderCandidates
                    summoned_enemy_spawn_attempts = [long]$_.summonedEnemySpawnAttempts
                    summoned_enemy_spawns = [long]$_.summonedEnemySpawns
                    summoned_enemy_cap_blocked = [long]$_.summonedEnemyCapBlocked
                    file_name = [string]$_.fileName
                }
            }
    )
    $stageCoverage = @(
        $Session.stageCoverage |
            ForEach-Object {
                [PSCustomObject]@{
                    stage = [int]$_.stage
                    captured_incidents = [int]$_.capturedIncidents
                    suppressed_stage_limit_windows =
                        [int]$_.suppressedEvaluationWindowsByStageLimit
                    suppressed_repeated_reason_incidents =
                        [int]$_.suppressedIncidentsByRepeatedReason
                }
            }
    )

    return [PSCustomObject]@{
        report = "performance_session"
        session_path = $ResolvedSessionPath
        session_id = [string]$Session.sessionId
        started_utc = [string]$Session.startedUtc
        ended_utc = [string]$Session.endedUtc
        unity_version = [string]$Session.unityVersion
        platform = [string]$Session.platform
        graphics_device = [string]$Session.graphicsDevice
        quality_level = [string]$Session.qualityLevel
        screen = "{0}x{1}" -f [int]$Session.screenWidth, [int]$Session.screenHeight
        v_sync_count = [int]$Session.vSyncCount
        target_frame_rate = [int]$Session.targetFrameRate
        baseline_p95_ms = [double]$Session.baselineP95Ms
        incident_count = $incidents.Count
        max_incidents_per_session = [int]$Session.maxIncidentsPerSession
        max_incidents_per_stage = [int]$Session.maxIncidentsPerStage
        max_incidents_per_reason_per_stage = [int]$Session.maxIncidentsPerReasonPerStage
        suppressed_session_limit_windows =
            [int]$Session.suppressedEvaluationWindowsBySessionLimit
        stage_coverage = $stageCoverage
        sentinel_average_microseconds = [double]$Session.normalSentinelAverageMicroseconds
        sentinel_max_microseconds = [double]$Session.normalSentinelMaxMicroseconds
        max_incident_write_ms = [double]$Session.maxIncidentWriteMilliseconds
        sentinel_sample_count = [int]$Session.normalSentinelSamples
        top_incidents = $sortedIncidents
    }
}

if ($SelfTest) {
    $fixture = @'
{
  "sessionId": "self-test",
  "startedUtc": "2026-01-01T00:00:00Z",
  "endedUtc": "2026-01-01T00:01:00Z",
  "unityVersion": "2022.3",
  "platform": "WindowsEditor",
  "graphicsDevice": "Fixture GPU",
  "qualityLevel": "High",
  "screenWidth": 1920,
  "screenHeight": 1080,
  "vSyncCount": 0,
  "targetFrameRate": 60,
  "baselineP95Ms": 16.7,
  "maxIncidentsPerSession": 20,
  "maxIncidentsPerStage": 5,
  "maxIncidentsPerReasonPerStage": 2,
  "suppressedEvaluationWindowsBySessionLimit": 3,
  "stageCoverage": [
    {
      "stage": 3,
      "capturedIncidents": 5,
      "suppressedEvaluationWindowsByStageLimit": 8,
      "suppressedIncidentsByRepeatedReason": 2
    }
  ],
  "normalSentinelAverageMicroseconds": 11.5,
  "normalSentinelMaxMicroseconds": 80.0,
  "maxIncidentWriteMilliseconds": 2.5,
  "normalSentinelSamples": 100,
  "incidents": [
    { "incidentIndex": 1, "reason": "slow", "p95FrameMs": 41.0, "maxFrameMs": 60.0 },
    {
      "incidentIndex": 2,
      "reason": "critical",
      "reasonCategory": "critical-frame",
      "stage": 3,
      "p95FrameMs": 80.0,
      "maxFrameMs": 120.0,
      "excaliburProjectileDamageHits": 42,
      "popupInstancesCreated": 5,
      "popupReuses": 37,
      "popupDrops": 3,
      "peakActiveDamagePopups": 21,
      "hitFlashCoalescedRequests": 7,
      "pickupProximityScans": 150,
      "pickupScanCandidates": 12000,
      "pickupAttractionsStarted": 48,
      "pickupMovementTicks": 720,
      "bananaOverlapQueries": 60,
      "bananaColliderCandidates": 480,
      "summonedEnemySpawnAttempts": 20,
      "summonedEnemySpawns": 5,
      "summonedEnemyCapBlocked": 15
    }
  ]
}
'@ | ConvertFrom-Json

    $summary = New-PerformanceSessionSummary `
        -Session $fixture `
        -ResolvedSessionPath "self-test/session.json" `
        -TopCount 1
    if ($summary.incident_count -ne 2 -or
        $summary.top_incidents.Count -ne 1 -or
        $summary.top_incidents[0].incident_index -ne 2 -or
        $summary.top_incidents[0].reason_category -ne "critical-frame" -or
        $summary.top_incidents[0].stage -ne 3 -or
        $summary.top_incidents[0].excalibur_damage_hits -ne 42 -or
        $summary.top_incidents[0].popup_reuses -ne 37 -or
        $summary.top_incidents[0].popup_drops -ne 3 -or
        $summary.top_incidents[0].peak_active_damage_popups -ne 21 -or
        $summary.top_incidents[0].hit_flash_coalesced_requests -ne 7 -or
        $summary.top_incidents[0].pickup_proximity_scans -ne 150 -or
        $summary.top_incidents[0].pickup_scan_candidates -ne 12000 -or
        $summary.top_incidents[0].pickup_attractions_started -ne 48 -or
        $summary.top_incidents[0].pickup_movement_ticks -ne 720 -or
        $summary.top_incidents[0].banana_overlap_queries -ne 60 -or
        $summary.top_incidents[0].banana_collider_candidates -ne 480 -or
        $summary.top_incidents[0].summoned_enemy_spawn_attempts -ne 20 -or
        $summary.top_incidents[0].summoned_enemy_spawns -ne 5 -or
        $summary.top_incidents[0].summoned_enemy_cap_blocked -ne 15 -or
        $summary.max_incidents_per_session -ne 20 -or
        $summary.max_incidents_per_stage -ne 5 -or
        $summary.max_incidents_per_reason_per_stage -ne 2 -or
        $summary.suppressed_session_limit_windows -ne 3 -or
        $summary.stage_coverage.Count -ne 1 -or
        $summary.stage_coverage[0].captured_incidents -ne 5 -or
        $summary.stage_coverage[0].suppressed_stage_limit_windows -ne 8 -or
        $summary.stage_coverage[0].suppressed_repeated_reason_incidents -ne 2) {
        throw "performance-session-report self-test failed."
    }

    Write-Output "performance_session_report_self_test: passed"
    exit 0
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sessionsRoot = Join-Path $projectRoot "TokenReports\PerformanceSessions"

if ([string]::IsNullOrWhiteSpace($SessionPath)) {
    $latestPath = Join-Path $sessionsRoot "latest-session.txt"
    if (Test-Path -LiteralPath $latestPath -PathType Leaf) {
        $SessionPath = (Get-Content -LiteralPath $latestPath -Raw -Encoding UTF8).Trim()
    }
    else {
        $latestDirectory = Get-ChildItem -LiteralPath $sessionsRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -eq $latestDirectory) {
            throw "No performance session was found under $sessionsRoot."
        }
        $SessionPath = $latestDirectory.FullName
    }
}

if (Test-Path -LiteralPath $SessionPath -PathType Container) {
    $SessionPath = Join-Path $SessionPath "session.json"
}
if (-not (Test-Path -LiteralPath $SessionPath -PathType Leaf)) {
    throw "Performance session JSON was not found: $SessionPath"
}

$resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath).Path
$session = Get-Content -LiteralPath $resolvedSessionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$result = New-PerformanceSessionSummary `
    -Session $session `
    -ResolvedSessionPath $resolvedSessionPath `
    -TopCount $Top

if ($Json) {
    $result | ConvertTo-Json -Depth 6
    exit 0
}

Write-Output ("report: {0}" -f $result.report)
Write-Output ("session_id: {0}" -f $result.session_id)
Write-Output ("session_path: {0}" -f $result.session_path)
Write-Output ("duration: {0} -> {1}" -f $result.started_utc, $result.ended_utc)
Write-Output ("environment: Unity {0}; {1}; {2}; quality={3}; screen={4}; vsync={5}; target_fps={6}" -f
    $result.unity_version,
    $result.platform,
    $result.graphics_device,
    $result.quality_level,
    $result.screen,
    $result.v_sync_count,
    $result.target_frame_rate)
Write-Output ("incidents: {0}; baseline_p95_ms={1:N2}" -f
    $result.incident_count,
    $result.baseline_p95_ms)
Write-Output ("incident_budget: session={0}; per_stage={1}; per_reason_stage={2}; suppressed_session_windows={3}" -f
    $result.max_incidents_per_session,
    $result.max_incidents_per_stage,
    $result.max_incidents_per_reason_per_stage,
    $result.suppressed_session_limit_windows)
if ($result.stage_coverage.Count -gt 0) {
    Write-Output "stage_coverage:"
    foreach ($coverage in $result.stage_coverage) {
        Write-Output ("  stage={0} captured={1} suppressed_stage_windows={2} suppressed_repeated_reason={3}" -f
            $coverage.stage,
            $coverage.captured_incidents,
            $coverage.suppressed_stage_limit_windows,
            $coverage.suppressed_repeated_reason_incidents)
    }
}
Write-Output ("sentinel_overhead: avg_us={0:N2}; max_us={1:N2}; samples={2}; max_write_ms={3:N2}" -f
    $result.sentinel_average_microseconds,
    $result.sentinel_max_microseconds,
    $result.sentinel_sample_count,
    $result.max_incident_write_ms)

if ($result.top_incidents.Count -eq 0) {
    Write-Output "top_incidents: none"
    exit 0
}

Write-Output "top_incidents:"
foreach ($incident in $result.top_incidents) {
    Write-Output ("  #{0} reason={1} category={2} stage={3} elapsed={4:N1}s p95={5:N2}ms p99={6:N2}ms max={7:N2}ms over50={8} over100={9} enemies={10} gc_alloc={11} gc={12} area_candidates={13} projectile_candidates={14} feedback={15} deaths={16} file={17}" -f
        $incident.incident_index,
        $incident.reason,
        $incident.reason_category,
        $incident.stage,
        $incident.game_elapsed_seconds,
        $incident.p95_frame_ms,
        $incident.p99_frame_ms,
        $incident.max_frame_ms,
        $incident.frames_over_50_ms,
        $incident.frames_over_100_ms,
        $incident.peak_enemies,
        $incident.gc_allocated_bytes,
        $incident.gc_collections,
        $incident.area_candidates,
        $incident.projectile_candidates,
        $incident.damage_feedback_events,
        $incident.enemy_deaths,
        $incident.file_name)
}
