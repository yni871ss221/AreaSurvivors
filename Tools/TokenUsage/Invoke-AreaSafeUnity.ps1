param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Compile", "ConsoleLogs", "ConsoleErrors", "ConsoleWarnings", "Menu", "MenuExists", "Eval", "AssetImport", "AssetRefresh", "Screenshot", "PlayEnter", "PlayExit", "PlayStatus")]
    [string]$Action,
    [string]$MenuPath = "",
    [string]$EvalCode = "",
    [string]$AssetPath = "",
    [string]$ScreenshotPath = "",
    [int]$MaxCount = 30,
    [int]$WarnTokens = 3000,
    [int]$BlockTokens = 8000,
    [int]$TimeoutSeconds = 0,
    [ValidateRange(0, 600)][int]$CompileWaitSeconds = 0,
    [int]$PlayExitCooldownSeconds = 20,
    [int]$CompileAfterRefreshCooldownSeconds = 30,
    [int]$CommandLockTimeoutSeconds = 2,
    [switch]$PrintOutput,
    [switch]$AllowHighOutput
)

$ErrorActionPreference = "Stop"

function Quote-Value {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Assert-SafeAssetPath {
    param([Parameter(Mandatory = $true)][string]$Value)
    $normalized = $Value.Replace("\", "/")
    $segments = $normalized.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
    if (-not $normalized.StartsWith("Assets/", [System.StringComparison]::Ordinal) -or $segments -contains "..") {
        throw "AssetPath must be a project-relative path under Assets/ and must not contain '..'."
    }
    return $normalized
}

function Assert-SafeScreenshotPath {
    param([Parameter(Mandatory = $true)][string]$Value)
    $normalized = $Value.Replace("\", "/")
    $segments = $normalized.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
    $allowedRoot = $normalized.StartsWith("Temp/", [System.StringComparison]::Ordinal) -or
        $normalized.StartsWith("TokenReports/", [System.StringComparison]::Ordinal)
    if (-not $allowedRoot -or $segments -contains ".." -or -not $normalized.EndsWith(".png", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ScreenshotPath must be a project-relative PNG path under Temp/ or TokenReports/ and must not contain '..'."
    }
    return $normalized
}

function Assert-UniCliMainEditorPid {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $serverPidPath = Join-Path $ProjectRoot "Library\UniCli\server.pid"
    if (-not (Test-Path -LiteralPath $serverPidPath -PathType Leaf)) {
        throw "guard_code: 45; UniCLI server PID file is missing. Open the UniCLI Server window in the main Unity Editor and press Start Server before retrying the same safe-unity command. path=$serverPidPath"
    }

    $pidText = [System.IO.File]::ReadAllText($serverPidPath).Trim()
    $serverProcessId = 0
    if (-not [int]::TryParse($pidText, [ref]$serverProcessId)) {
        throw "guard_code: 45; UniCLI server PID file is invalid. Do not retry the Unity command until Start Server rewrites it. path=$serverPidPath value=$pidText"
    }

    $serverProcess = Get-Process -Id $serverProcessId -ErrorAction SilentlyContinue
    if ($serverProcess -eq $null -or $serverProcess.ProcessName -ne "Unity") {
        $processName = if ($serverProcess -ne $null) { $serverProcess.ProcessName } else { "missing" }
        throw "guard_code: 45; UniCLI server PID does not point to the main Unity Editor (likely overwritten by an AssetImportWorker). Do not wait through connection retries. After imports settle, open the UniCLI Server window in the main Editor and press Start Server once. pid=$serverProcessId process=$processName path=$serverPidPath"
    }

    if ($serverProcess.MainWindowHandle -eq 0) {
        try {
            $processDetails = Get-CimInstance Win32_Process -Filter ("ProcessId = {0}" -f $serverProcessId) -ErrorAction Stop
        } catch {
            throw "guard_code: 26; safe-unity preflight cannot distinguish the main Unity Editor from an AssetImportWorker because process command-line access is denied. Rerun the same safe-unity command once with elevated sandbox permission. pid=$serverProcessId path=$serverPidPath"
        }

        $commandLine = if ($processDetails -ne $null) { [string]$processDetails.CommandLine } else { "" }
        if ($commandLine -match '(?i)AssetImportWorker|(?:^|\s)-batchMode(?:\s|$)') {
            throw "guard_code: 45; UniCLI server PID points to an AssetImportWorker. Do not wait through connection retries. After imports settle, open the UniCLI Server window in the main Editor and press Start Server once. pid=$serverProcessId process=$($serverProcess.ProcessName) path=$serverPidPath"
        }

        throw "guard_code: 45; UniCLI server PID points to a Unity process without a main Editor window. Wait for the Editor to finish opening, then press Start Server once before retrying the same safe-unity command. pid=$serverProcessId process=$($serverProcess.ProcessName) path=$serverPidPath"
    }
}

$safeCommandPath = Join-Path $PSScriptRoot "Safe-Command.ps1"
$projectRoot = Split-Path $PSScriptRoot -Parent | Split-Path -Parent
$playExitMarkerPath = Join-Path (Join-Path $projectRoot "Library\AreaSafeUnity") "last-playmode-exit.utc"
$assetRefreshMarkerPath = Join-Path (Join-Path $projectRoot "Library\AreaSafeUnity") "last-asset-refresh.utc"
$command = ""
$commandMutex = $null
$commandMutexAcquired = $false

if ($Action -eq "Eval") {
    if ([string]::IsNullOrWhiteSpace($EvalCode)) { throw "Eval requires -EvalCode." }
    if ($EvalCode.Contains([char]34) -or $EvalCode.Contains([char]39) -or $EvalCode.Contains("`r") -or $EvalCode.Contains("`n")) {
        throw "guard_code: 25; inline Eval containing quotes or newlines is blocked because PowerShell/RTK/native argument transport can alter C# source. Use a temporary Editor runner through invoke-unity-editor-runner.ps1. Do not rewrite the same Eval with different escaping."
    }
    if ($EvalCode.Length -gt 500) {
        throw "EvalCode is too long for safe inline execution. Create a temporary Editor runner and call a short method instead."
    }
}

if (Test-Path -LiteralPath $playExitMarkerPath) {
    $markerText = [System.IO.File]::ReadAllText($playExitMarkerPath).Trim()
    $lastExitUtc = [datetime]::MinValue
    if ([datetime]::TryParse($markerText, [ref]$lastExitUtc)) {
        $ageSeconds = ([datetime]::UtcNow - $lastExitUtc.ToUniversalTime()).TotalSeconds
        if ($ageSeconds -lt $PlayExitCooldownSeconds) {
            $remainingSeconds = [math]::Ceiling($PlayExitCooldownSeconds - $ageSeconds)
            [Console]::Error.WriteLine("guard_code: 23; blocked UniCLI command '$Action': PlayMode.Exit was requested recently. Do not call UniCLI during the Unity play-mode transition. Retry through safe-unity.ps1 after $remainingSeconds seconds only when the follow-up is required.")
            $global:LASTEXITCODE = 23
            return
        }
    }
    Remove-Item -LiteralPath $playExitMarkerPath -Force -ErrorAction SilentlyContinue
}

if ($Action -eq "Compile" -and (Test-Path -LiteralPath $assetRefreshMarkerPath)) {
    $markerText = [System.IO.File]::ReadAllText($assetRefreshMarkerPath).Trim()
    $lastRefreshUtc = [datetime]::MinValue
    if ([datetime]::TryParse($markerText, [ref]$lastRefreshUtc)) {
        $ageSeconds = ([datetime]::UtcNow - $lastRefreshUtc.ToUniversalTime()).TotalSeconds
        if ($ageSeconds -lt $CompileAfterRefreshCooldownSeconds) {
            $remainingSeconds = [math]::Ceiling($CompileAfterRefreshCooldownSeconds - $ageSeconds)
            [Console]::Error.WriteLine("guard_code: 35; blocked Compile because AssetRefresh/AssetImport recently requested asynchronous script compilation. Do not overlap an explicit Compile with Unity's import-triggered compilation. Retry after $remainingSeconds seconds only if compile verification is still required.")
            $global:LASTEXITCODE = 35
            return
        }
    }
    Remove-Item -LiteralPath $assetRefreshMarkerPath -Force -ErrorAction SilentlyContinue
}

try {
    $commandMutex = New-Object System.Threading.Mutex($false, "AreaSurvivors.SafeUnity.Command")
    $commandMutexAcquired = $commandMutex.WaitOne([TimeSpan]::FromSeconds($CommandLockTimeoutSeconds))
    if (-not $commandMutexAcquired) {
        throw "guard_code: 34; another safe-unity command is still running. Do not contact UniCLI concurrently or retry with another command. Wait for the original command result and diagnose it first if it fails or times out."
    }

switch ($Action) {
    "Compile" {
        $compileVerifierPath = Join-Path $PSScriptRoot "verify-unity-script-compilation.ps1"
        $command = "rtk powershell -NoProfile -ExecutionPolicy Bypass -File $(Quote-Value $compileVerifierPath) -WaitTimeoutSeconds $CompileWaitSeconds"
    }
    "ConsoleLogs" {
        $command = "rtk unicli exec Console.GetLog --logType Log --maxCount $MaxCount"
    }
    "ConsoleErrors" {
        $command = "rtk unicli exec Console.GetLog --logType Error --maxCount $MaxCount"
    }
    "ConsoleWarnings" {
        $command = "rtk unicli exec Console.GetLog --logType Warning --maxCount $MaxCount"
    }
    "Menu" {
        if ([string]::IsNullOrWhiteSpace($MenuPath)) { throw "Menu requires -MenuPath." }
        $command = "rtk unicli exec Menu.Execute --menuItemPath $(Quote-Value $MenuPath)"
    }
    "MenuExists" {
        if ([string]::IsNullOrWhiteSpace($MenuPath)) { throw "MenuExists requires -MenuPath." }
        $command = "rtk unicli exec Menu.List --filterText $(Quote-Value $MenuPath) --filterType exact --maxCount 5"
    }
    "Eval" {
        Assert-UniCliMainEditorPid -ProjectRoot $projectRoot
        $statusJson = & $safeCommandPath -Command "rtk unicli exec PlayMode.Status" -TimeoutSeconds 15 -WarnTokens $WarnTokens -BlockTokens $BlockTokens -Json
        $statusRecord = $statusJson | ConvertFrom-Json
        $statusCaptured = ""
        if (Test-Path -LiteralPath ([string]$statusRecord.capture_path)) {
            $statusCaptured = [System.IO.File]::ReadAllText([string]$statusRecord.capture_path)
        }
        if ([int]$statusRecord.exit_code -ne 0) {
            throw "guard_code: 28; Eval preflight could not confirm Play Mode state. exit_code=$($statusRecord.exit_code); timed_out=$($statusRecord.timed_out); capture_path=$($statusRecord.capture_path). Stop and diagnose the server state; do not execute Eval."
        }
        if ($statusCaptured -match "isPlaying\s*:\s*True" -or $statusCaptured -match '"isPlaying"\s*:\s*true') {
            throw "guard_code: 27; Eval is blocked during Play Mode because AssemblyBuilder can trigger Domain Reload and leave UniCLI permanently busy. Use a precompiled validation hook or normal game input."
        }
        if ($statusCaptured -notmatch "isPlaying\s*:\s*False" -and $statusCaptured -notmatch '"isPlaying"\s*:\s*false') {
            throw "guard_code: 28; Eval preflight returned an unrecognized Play Mode status. capture_path=$($statusRecord.capture_path). Stop and inspect the response; do not execute Eval."
        }
        $command = "rtk unicli exec Eval --code $(Quote-Value $EvalCode)"
    }
    "AssetImport" {
        if ([string]::IsNullOrWhiteSpace($AssetPath)) { throw "AssetImport requires -AssetPath." }
        $normalizedAssetPath = Assert-SafeAssetPath $AssetPath
        $command = "rtk unicli exec AssetDatabase.Import --path $(Quote-Value $normalizedAssetPath) --forceUpdate true"
    }
    "AssetRefresh" {
        $command = "rtk unicli exec AssetDatabase.Import"
    }
    "Screenshot" {
        if ([string]::IsNullOrWhiteSpace($ScreenshotPath)) { throw "Screenshot requires -ScreenshotPath." }
        $normalizedScreenshotPath = Assert-SafeScreenshotPath $ScreenshotPath
        $command = "rtk unicli exec Screenshot.Capture --path $(Quote-Value $normalizedScreenshotPath) --superSize 1"
    }
    "PlayEnter" {
        $command = "rtk unicli exec PlayMode.Enter"
    }
    "PlayExit" {
        $command = "rtk unicli exec PlayMode.Exit"
    }
    "PlayStatus" {
        $command = "rtk unicli exec PlayMode.Status"
    }
}

if ($Action -ne "Compile" -and $Action -ne "Eval") {
    Assert-UniCliMainEditorPid -ProjectRoot $projectRoot
}

if ($TimeoutSeconds -le 0) {
    $TimeoutSeconds = switch ($Action) {
        "Compile" { 120 }
        "PlayEnter" { 45 }
        "PlayExit" { 20 }
        "PlayStatus" { 15 }
        "Screenshot" { 45 }
        default { 60 }
    }
}

$argsForSafe = @{
    Command = $command
    WarnTokens = $WarnTokens
    BlockTokens = $BlockTokens
    TimeoutSeconds = $TimeoutSeconds
}
if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
if ($AllowHighOutput) { $argsForSafe.AllowHighOutput = $true }

    $safeJsonArgs = @{}
    foreach ($key in $argsForSafe.Keys) {
        if ($key -ne "PrintOutput") { $safeJsonArgs[$key] = $argsForSafe[$key] }
    }
    $safeJsonArgs.Json = $true
    $safeJson = & $safeCommandPath @safeJsonArgs
    $safeRecord = $safeJson | ConvertFrom-Json
    $safeExitCode = [int]$safeRecord.exit_code
    $captured = ""
    if (Test-Path -LiteralPath ([string]$safeRecord.capture_path)) {
        $captured = [System.IO.File]::ReadAllText([string]$safeRecord.capture_path)
    }

if ($Action -eq "MenuExists") {
    if ($safeExitCode -eq 0) {
        $menuPattern = '"path"\s*:\s*"' + [regex]::Escape($MenuPath) + '"'
        if ($captured -notmatch $menuPattern) {
            [Console]::Error.WriteLine("guard_code: 24; exact Unity menu item was not registered: $MenuPath. Stop here and inspect AssetDatabase import/compile output; do not fall back to Eval.")
            $safeExitCode = 24
        } else {
            Write-Output ("menu_registered: {0}" -f $MenuPath)
            Write-Output ("capture_path: {0}" -f $safeRecord.capture_path)
        }
    } elseif ($captured -match "Access to the path is denied") {
        [Console]::Error.WriteLine("guard_code: 26; UniCLI named-pipe access was denied by the execution boundary. Re-run this same safe-unity command with outer sandbox escalation; do not change the Unity operation or fall back to another method.")
        [Console]::Error.WriteLine("capture_path: $($safeRecord.capture_path)")
        $safeExitCode = 26
    } else {
        [Console]::Error.WriteLine("safe-unity MenuExists failed; exit_code=$safeExitCode; timed_out=$($safeRecord.timed_out); capture_path=$($safeRecord.capture_path)")
    }
} else {
    Write-Output ("command: {0}" -f $safeRecord.command)
    Write-Output ("exit_code: {0}" -f $safeRecord.exit_code)
    Write-Output ("timeout_seconds: {0}" -f $safeRecord.timeout_seconds)
    Write-Output ("timed_out: {0}" -f $safeRecord.timed_out)
    Write-Output ("estimated_tokens: {0}" -f $safeRecord.estimate.estimated_tokens)
    Write-Output ("risk: {0}" -f $safeRecord.estimate.risk)
    Write-Output ("captured_to: {0}" -f $safeRecord.capture_path)
    Write-Output ("report_path: {0}" -f $safeRecord.report_path)
    Write-Output ("advice: {0}" -f $safeRecord.advice)

    if ($safeExitCode -ne 0 -and $captured -match "Access to the path is denied") {
        [Console]::Error.WriteLine("guard_code: 26; UniCLI named-pipe access was denied by the execution boundary. Re-run this same safe-unity command with outer sandbox escalation; do not change the Unity operation or fall back to another method.")
        [Console]::Error.WriteLine("capture_path: $($safeRecord.capture_path)")
        $safeExitCode = 26
    } elseif ($safeExitCode -ne 0) {
        [Console]::Error.WriteLine("safe-unity $Action failed; exit_code=$safeExitCode; timed_out=$($safeRecord.timed_out); capture_path=$($safeRecord.capture_path)")
    }

    if ($PrintOutput -and -not [bool]$safeRecord.blocked) {
        Write-Output ""
        Write-Output "--- captured output ---"
        Write-Output $captured
    } elseif ([bool]$safeRecord.blocked) {
        Write-Output ("output: blocked because estimated tokens >= {0}; rerun with -AllowHighOutput only if intentional" -f $BlockTokens)
    } else {
        Write-Output "output: hidden by default; use -PrintOutput to show"
    }
}

if ($Action -eq "PlayExit" -and $safeExitCode -eq 0) {
    $markerDirectory = Split-Path $playExitMarkerPath -Parent
    [System.IO.Directory]::CreateDirectory($markerDirectory) | Out-Null
    [System.IO.File]::WriteAllText($playExitMarkerPath, [datetime]::UtcNow.ToString("o"))
}

if (($Action -eq "AssetRefresh" -or $Action -eq "AssetImport") -and $safeExitCode -eq 0) {
    $markerDirectory = Split-Path $assetRefreshMarkerPath -Parent
    [System.IO.Directory]::CreateDirectory($markerDirectory) | Out-Null
    [System.IO.File]::WriteAllText($assetRefreshMarkerPath, [datetime]::UtcNow.ToString("o"))
}

$global:LASTEXITCODE = $safeExitCode
} finally {
    if ($commandMutexAcquired -and $commandMutex -ne $null) {
        $commandMutex.ReleaseMutex()
    }
    if ($commandMutex -ne $null) {
        $commandMutex.Dispose()
    }
}

exit $safeExitCode
