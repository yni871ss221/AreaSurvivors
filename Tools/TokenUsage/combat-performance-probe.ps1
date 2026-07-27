param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "Baseline",
        "NoPopups",
        "NoHitFlash",
        "NoFeedback",
        "PrepareExcaliburSustainedBaseline",
        "PrepareExcaliburSustainedNoPopups",
        "PrepareExcaliburSustainedNoHitFlash",
        "PrepareExcaliburSustainedNoFeedback",
        "PrepareExcaliburSustainedNoEnemyController",
        "PrepareExcaliburSustainedNoEnemyContactCheck",
        "PrepareExcaliburSustainedNoEnemyMoveMultiplier",
        "PrepareExcaliburSustainedNoEnemyPaint",
        "PrepareExcaliburSustainedNoEnemyAnimation",
        "PrepareExcaliburSustainedNoEnemyYSort",
        "PrepareExcaliburSustainedNoEnemyEnemyCollision",
        "PrepareEnemyCrowdBaseline",
        "PrepareEnemyCrowdNoEnemyEnemyCollision",
        "PrepareEnemyCrowdNoOcclusion",
        "PrepareEnemyCrowdNoOutline",
        "PrepareEnemyCrowdNoOcclusionAndOutline",
        "PrepareEnemyCrowdNoEnemyController",
        "PrepareEnemyCrowdPhysicsMultithreading",
        "RebuildPerformanceLoadMatrix",
        "PrepareEnemyLoad200Matrix",
        "PrepareEnemyLoad400Matrix",
        "PrepareEnemyLoad800Matrix",
        "PrepareExcaliburKillBurstBaseline",
        "PrepareExcaliburKillBurstNoFeedback",
        "PrepareFrostSustainedNoFeedback",
        "LastResult",
        "LastMatrixResult")]
    [string]$Action,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"
$safeUnityPath = Join-Path $PSScriptRoot "safe-unity.ps1"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$resultPath = Join-Path $projectRoot "Library\AreaSafeUnity\combat-performance-probe-last.txt"
$matrixResultPath = Join-Path $projectRoot "Library\AreaSafeUnity\combat-performance-matrix-last.txt"
$playExitMarkerPath = Join-Path $projectRoot "Library\AreaSafeUnity\last-playmode-exit.utc"
$menuRoot = "Area Survivors/Diagnostics/Combat Performance Probe/"
$performanceLoadMenuRoot = "Area Survivors/Testing/Performance Load/"

function Wait-ForPlayExitCooldown {
    if (-not (Test-Path -LiteralPath $playExitMarkerPath -PathType Leaf)) { return }

    $raw = (Get-Content -LiteralPath $playExitMarkerPath -Raw -Encoding UTF8).Trim()
    $markerTime = [DateTime]::Parse($raw).ToUniversalTime()
    $elapsedSeconds = ([DateTime]::UtcNow - $markerTime).TotalSeconds
    $remainingSeconds = [Math]::Ceiling(21.0 - $elapsedSeconds)
    if ($remainingSeconds -le 0) { return }
    if ($remainingSeconds -gt 25) {
        throw "Play Mode exit cooldown marker is unexpectedly in the future: $playExitMarkerPath"
    }

    Write-Output ("[combat-performance-probe] waiting {0}s for Play Mode exit cooldown" -f $remainingSeconds)
    Start-Sleep -Seconds $remainingSeconds
}

if ($Action -eq "LastResult") {
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Combat performance probe result does not exist yet: $resultPath"
    }

    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    Write-Output "combat_performance_probe_result: $result"
    exit 0
}

if ($Action -eq "LastMatrixResult") {
    if (-not (Test-Path -LiteralPath $matrixResultPath -PathType Leaf)) {
        throw "Combat performance matrix result does not exist yet: $matrixResultPath"
    }

    $result = Get-Content -LiteralPath $matrixResultPath -Raw -Encoding UTF8
    Write-Output "combat_performance_matrix_result:`n$result"
    exit 0
}

Wait-ForPlayExitCooldown

$menuPath = switch ($Action) {
    "Baseline" { $menuRoot + "Start Baseline (10s)" }
    "NoPopups" { $menuRoot + "Start Without Damage Popups (10s)" }
    "NoHitFlash" { $menuRoot + "Start Without Hit Flash (10s)" }
    "NoFeedback" { $menuRoot + "Start Without Damage Feedback (10s)" }
    "PrepareExcaliburSustainedBaseline" { $menuRoot + "Prepare Excalibur Sustained Baseline" }
    "PrepareExcaliburSustainedNoPopups" { $menuRoot + "Prepare Excalibur Sustained Without Damage Popups" }
    "PrepareExcaliburSustainedNoHitFlash" { $menuRoot + "Prepare Excalibur Sustained Without Hit Flash" }
    "PrepareExcaliburSustainedNoFeedback" { $menuRoot + "Prepare Excalibur Sustained Without Damage Feedback" }
    "PrepareExcaliburSustainedNoEnemyController" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Controller" }
    "PrepareExcaliburSustainedNoEnemyContactCheck" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Contact Check" }
    "PrepareExcaliburSustainedNoEnemyMoveMultiplier" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Move Multiplier" }
    "PrepareExcaliburSustainedNoEnemyPaint" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Paint" }
    "PrepareExcaliburSustainedNoEnemyAnimation" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Animation" }
    "PrepareExcaliburSustainedNoEnemyYSort" { $menuRoot + "Prepare Excalibur Sustained Without Enemy Y Sort" }
    "PrepareExcaliburSustainedNoEnemyEnemyCollision" { $menuRoot + "Prepare Excalibur Sustained Without Enemy-Enemy Collision" }
    "PrepareEnemyCrowdBaseline" { $menuRoot + "Prepare Enemy Crowd Baseline" }
    "PrepareEnemyCrowdNoEnemyEnemyCollision" { $menuRoot + "Prepare Enemy Crowd Without Enemy-Enemy Collision" }
    "PrepareEnemyCrowdNoOcclusion" { $menuRoot + "Prepare Enemy Crowd Without Occlusion" }
    "PrepareEnemyCrowdNoOutline" { $menuRoot + "Prepare Enemy Crowd Without Outline" }
    "PrepareEnemyCrowdNoOcclusionAndOutline" { $menuRoot + "Prepare Enemy Crowd Without Occlusion And Outline" }
    "PrepareEnemyCrowdNoEnemyController" { $menuRoot + "Prepare Enemy Crowd Without Enemy Controller" }
    "PrepareEnemyCrowdPhysicsMultithreading" { $menuRoot + "Prepare Enemy Crowd With Physics Multithreading" }
    "RebuildPerformanceLoadMatrix" { $performanceLoadMenuRoot + "Rebuild 200-400-800 Matrix" }
    "PrepareEnemyLoad200Matrix" { $performanceLoadMenuRoot + "Prepare 200" }
    "PrepareEnemyLoad400Matrix" { $performanceLoadMenuRoot + "Prepare 400" }
    "PrepareEnemyLoad800Matrix" { $performanceLoadMenuRoot + "Prepare 800" }
    "PrepareExcaliburKillBurstBaseline" { $menuRoot + "Prepare Excalibur Kill Burst Baseline" }
    "PrepareExcaliburKillBurstNoFeedback" { $menuRoot + "Prepare Excalibur Kill Burst Without Damage Feedback" }
    "PrepareFrostSustainedNoFeedback" { $menuRoot + "Prepare Frost Sustained Without Damage Feedback" }
}

& $safeUnityPath -Action Menu -MenuPath $menuPath -PrintOutput:$PrintOutput
exit $LASTEXITCODE
