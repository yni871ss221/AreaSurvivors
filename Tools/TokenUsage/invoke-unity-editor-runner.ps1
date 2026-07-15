param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("RegisterAndRun", "RefreshAfterRemoval")]
    [string]$Phase,
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [string]$MenuPath = "",
    [string]$DependencyScriptPaths = "",
    [int]$ImportTimeoutSeconds = 60,
    [int]$CompileTimeoutSeconds = 120,
    [int]$MenuTimeoutSeconds = 60,
    [switch]$Concise
)

$ErrorActionPreference = "Stop"

function Invoke-SafeUnityStep {
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [hashtable]$Arguments = @{}
    )

    $stepOutput = @(& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action $Action @Arguments 2>&1)
    $stepExitCode = $LASTEXITCODE
    if (-not $Concise) {
        $stepOutput | Write-Output
    } elseif ($stepExitCode -ne 0) {
        Write-Output ("[editor-runner] concise failure evidence for {0} (last 40 lines)" -f $Action)
        $stepOutput | Select-Object -Last 40 | Write-Output
    }
    if ($stepExitCode -ne 0) {
        throw "Unity runner phase '$Phase' stopped at '$Action' with exit code $stepExitCode. No fallback was attempted."
    }
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function Wait-ForCompileCooldown {
    $markerPath = Join-Path $projectRoot "Library/AreaSafeUnity/last-asset-refresh.utc"
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { return }

    $raw = (Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8).Trim()
    $markerTime = [DateTime]::Parse($raw).ToUniversalTime()
    $elapsedSeconds = ([DateTime]::UtcNow - $markerTime).TotalSeconds
    $remainingSeconds = [Math]::Ceiling(31.0 - $elapsedSeconds)
    if ($remainingSeconds -le 0) { return }
    if ($remainingSeconds -gt 35) {
        throw "Asset refresh cooldown marker is unexpectedly in the future: $markerPath"
    }

    Write-Output ("[editor-runner] waiting {0}s for import-triggered compile cooldown" -f $remainingSeconds)
    Start-Sleep -Seconds $remainingSeconds
}

function Normalize-ProjectScriptPath {
    param([Parameter(Mandatory = $true)][string]$Value)
    $normalized = $Value.Replace("\", "/").Trim()
    if (-not $normalized.StartsWith("Assets/", [System.StringComparison]::Ordinal) -or
        $normalized.Contains("../") -or
        -not $normalized.EndsWith(".cs", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Every script path must be a project-relative C# path under Assets/: $Value"
    }
    return $normalized
}

$normalizedScriptPath = Normalize-ProjectScriptPath $ScriptPath
$absoluteScriptPath = Join-Path $projectRoot $normalizedScriptPath
$importScriptPaths = @($normalizedScriptPath)
if (-not [string]::IsNullOrWhiteSpace($DependencyScriptPaths)) {
    foreach ($dependencyPath in ($DependencyScriptPaths -split ";")) {
        if ([string]::IsNullOrWhiteSpace($dependencyPath)) { continue }
        $importScriptPaths += Normalize-ProjectScriptPath $dependencyPath
    }
}
$importScriptPaths = @($importScriptPaths | Select-Object -Unique)

switch ($Phase) {
    "RegisterAndRun" {
        if (-not (Test-Path -LiteralPath $absoluteScriptPath -PathType Leaf)) {
            throw "Editor runner does not exist: $normalizedScriptPath"
        }
        if ([string]::IsNullOrWhiteSpace($MenuPath)) {
            throw "RegisterAndRun requires -MenuPath."
        }
        foreach ($importScriptPath in $importScriptPaths) {
            $importAbsolutePath = Join-Path $projectRoot $importScriptPath
            if (-not (Test-Path -LiteralPath $importAbsolutePath -PathType Leaf)) {
                throw "Editor runner dependency does not exist: $importScriptPath"
            }
        }

        Write-Output ("[editor-runner] 1/4 import {0} script(s)" -f $importScriptPaths.Count)
        foreach ($importScriptPath in $importScriptPaths) {
            Invoke-SafeUnityStep -Action "AssetImport" -Arguments @{ AssetPath = $importScriptPath; TimeoutSeconds = $ImportTimeoutSeconds }
        }
        Write-Output "[editor-runner] 2/4 compile"
        Wait-ForCompileCooldown
        Invoke-SafeUnityStep -Action "Compile" -Arguments @{ TimeoutSeconds = $CompileTimeoutSeconds; CompileWaitSeconds = [Math]::Max(0, $CompileTimeoutSeconds - 10) }
        Write-Output "[editor-runner] 3/4 exact menu registration check"
        Invoke-SafeUnityStep -Action "MenuExists" -Arguments @{ MenuPath = $MenuPath; TimeoutSeconds = $MenuTimeoutSeconds }
        Write-Output "[editor-runner] 4/4 execute"
        Invoke-SafeUnityStep -Action "Menu" -Arguments @{ MenuPath = $MenuPath; TimeoutSeconds = $MenuTimeoutSeconds }
        Write-Output "[editor-runner] completed. Remove the temporary .cs/.meta with apply_patch, then run RefreshAfterRemoval once."
    }
    "RefreshAfterRemoval" {
        if (Test-Path -LiteralPath $absoluteScriptPath) {
            throw "Cleanup is blocked because the Editor runner still exists: $normalizedScriptPath"
        }
        if (Test-Path -LiteralPath ($absoluteScriptPath + ".meta")) {
            throw "Cleanup is blocked because the Editor runner meta still exists: $normalizedScriptPath.meta"
        }

        Write-Output "[editor-runner-cleanup] 1/2 AssetDatabase refresh"
        Invoke-SafeUnityStep -Action "AssetRefresh" -Arguments @{ TimeoutSeconds = $ImportTimeoutSeconds }
        Write-Output "[editor-runner-cleanup] 2/2 compile"
        Wait-ForCompileCooldown
        Invoke-SafeUnityStep -Action "Compile" -Arguments @{ TimeoutSeconds = $CompileTimeoutSeconds; CompileWaitSeconds = [Math]::Max(0, $CompileTimeoutSeconds - 10) }
        Write-Output "[editor-runner-cleanup] completed"
    }
}
