param()

$ErrorActionPreference = "Stop"

$scripts = @(
    "Safe-Command.ps1",
    "safe-read.ps1",
    "safe-read-batch.ps1",
    "scoped-diff-check.ps1",
    "safe-status.ps1",
    "Get-TokenReportSummary.ps1",
    "start-task-token-check.ps1",
    "safe-unity.ps1",
    "Invoke-AreaSafeUnity.ps1",
    "invoke-unity-editor-runner.ps1",
    "rule-router.ps1",
    "safe-search.ps1",
    "focused-search.ps1",
    "append-vault-note.ps1",
    "temp-file-presence-report.ps1",
    "copy-generated-image-batch.ps1",
    "safe-unity-search.ps1",
    "Invoke-AreaUnitySearch.ps1",
    "unity-process-report.ps1",
    "validate-unicli-worker-guard.ps1",
    "unity-window-control.ps1",
    "verify-pause-hud.ps1",
    "verify-unity-script-compilation.ps1"
)

foreach ($scriptName in $scripts) {
    $path = Join-Path $PSScriptRoot $scriptName
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        $messages = ($errors | ForEach-Object { $_.Message }) -join "; "
        throw "PowerShell parse failed for ${scriptName}: $messages"
    }
}

$areaSafeUnityText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Invoke-AreaSafeUnity.ps1"))
if ($areaSafeUnityText -notmatch '(?m)^exit \$safeExitCode\s*$') {
    throw "Invoke-AreaSafeUnity.ps1 must propagate the Safe-Command exit code to its process caller."
}
foreach ($requiredPidGuardToken in @(
    "guard_code: 45",
    "guard_code: 26",
    "Library\UniCli\server.pid",
    "MainWindowHandle",
    "Get-CimInstance Win32_Process",
    "AssetImportWorker")) {
    if (-not $areaSafeUnityText.Contains($requiredPidGuardToken)) {
        throw "Invoke-AreaSafeUnity.ps1 must reject a UniCLI PID owned by an AssetImportWorker before contacting the server: $requiredPidGuardToken"
    }
}

& (Join-Path $PSScriptRoot "validate-unicli-worker-guard.ps1") | Out-Null

$appendVaultCommand = Get-Command (Join-Path $PSScriptRoot "append-vault-note.ps1")
foreach ($requiredParameter in @("VaultRoot", "RelativePath", "ContentPath")) {
    if (-not $appendVaultCommand.Parameters.ContainsKey($requiredParameter)) {
        throw "append-vault-note.ps1 formal contract is missing -$requiredParameter."
    }
}
foreach ($invalidRememberedParameter in @("VaultPath", "NotePath", "AppendFile")) {
    if ($appendVaultCommand.Parameters.ContainsKey($invalidRememberedParameter)) {
        throw "append-vault-note.ps1 must keep one formal contract; unexpected legacy parameter: -$invalidRememberedParameter"
    }
}

$tempPresenceOutput = @(& (Join-Path $PSScriptRoot "temp-file-presence-report.ps1") -Path "Temp/AgentAssets/__missing_temp_presence_self_test__.txt") -join "`n"
if (-not $tempPresenceOutput.Contains("temp_file_exists: false")) {
    throw "temp-file-presence-report.ps1 must report an already-cleaned temp file without failing."
}
$tempPresenceGuarded = $false
try {
    & (Join-Path $PSScriptRoot "temp-file-presence-report.ps1") -Path "AGENTS.md" | Out-Null
} catch {
    $tempPresenceGuarded = $_.Exception.Message.Contains("guard_code: 46")
}
if (-not $tempPresenceGuarded) {
    throw "temp-file-presence-report.ps1 must reject paths outside Temp/AgentAssets."
}

$copyGeneratedCommand = Get-Command (Join-Path $PSScriptRoot "copy-generated-image-batch.ps1")
foreach ($requiredParameter in @("SourceDirectory", "ManifestPath", "DestinationDirectory", "ValidateOnly")) {
    if (-not $copyGeneratedCommand.Parameters.ContainsKey($requiredParameter)) {
        throw "copy-generated-image-batch.ps1 formal contract is missing -$requiredParameter."
    }
}

$safeReadPathGuarded = $false
try {
    & "$PSScriptRoot\safe-read.ps1" -Path "__missing_safe_read_self_test__.cs" -First 1 | Out-Null
} catch {
    $safeReadPathGuarded = $_.Exception.Message.Contains("guard_code: 33")
}
if (-not $safeReadPathGuarded) {
    throw "safe-read must reject a missing file before constructing or running Get-Content."
}

$safeReadCommand = Get-Command (Join-Path $PSScriptRoot "safe-read.ps1")
if (-not $safeReadCommand.Parameters.ContainsKey("AllowHighOutput")) {
    throw "safe-read formal contract is missing -AllowHighOutput."
}
$safeReadOutputGuarded = $false
try {
    & "$PSScriptRoot\safe-read.ps1" -Path "$PSScriptRoot\safe-read.ps1" -StartLine 1 -EndLine 81 -AllowMany -PrintOutput | Out-Null
} catch {
    $safeReadOutputGuarded = $_.Exception.Message.Contains("guard_code: 39")
}
if (-not $safeReadOutputGuarded) {
    throw "safe-read must reject oversized interactive output before invoking Safe-Command."
}
& "$PSScriptRoot\safe-read.ps1" -Path "$PSScriptRoot\safe-read.ps1" -StartLine 1 -EndLine 161 -AllowMany -AllowHighOutput | Out-Null

$safeReadBatchCommand = Get-Command (Join-Path $PSScriptRoot "safe-read-batch.ps1")
foreach ($requiredParameter in @("Path", "Ranges", "AllowMany")) {
    if (-not $safeReadBatchCommand.Parameters.ContainsKey($requiredParameter)) {
        throw "safe-read-batch.ps1 formal contract is missing -$requiredParameter."
    }
}
& "$PSScriptRoot\safe-read-batch.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Ranges "1-2;3-4" | Out-Null
& "$PSScriptRoot\safe-read-batch.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Ranges "1-81" -AllowMany | Out-Null
$safeReadBatchSplitOutput = @(& "$PSScriptRoot\safe-read-batch.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Ranges "1-81")
if (-not ($safeReadBatchSplitOutput -contains "safe_read_batch_range: 1-80") -or
    -not ($safeReadBatchSplitOutput -contains "safe_read_batch_range: 81-81")) {
    throw "safe-read-batch must split ranges longer than 80 lines before invoking safe-read."
}
$safeReadBatchRangeGuarded = $false
try {
    & "$PSScriptRoot\safe-read-batch.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Ranges "1-x" | Out-Null
} catch {
    $safeReadBatchRangeGuarded = $_.Exception.Message.Contains("guard_code: 38")
}
if (-not $safeReadBatchRangeGuarded) {
    throw "safe-read-batch must reject malformed ranges before invoking safe-read."
}

$safeReadLiteralGuarded = $false
try {
    & "$PSScriptRoot\safe-read.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Pattern "kills++" | Out-Null
} catch {
    $safeReadLiteralGuarded = $_.Exception.Message.Contains("guard_code: 36")
}
if (-not $safeReadLiteralGuarded) {
    throw "safe-read must reject unescaped code quantifier literals before running a regex search."
}

$safeReadInvalidRegexGuarded = $false
try {
    & "$PSScriptRoot\safe-read.ps1" -Path "$PSScriptRoot\safe-read.ps1" -Pattern "EnsureWeaponLevels(" | Out-Null
} catch {
    $safeReadInvalidRegexGuarded = $_.Exception.Message.Contains("guard_code: 37")
}
if (-not $safeReadInvalidRegexGuarded) {
    throw "safe-read must reject an invalid regex before constructing the line-matching command."
}

$safeReadText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-read.ps1"))
if (-not $safeReadText.Contains('[switch]$LiteralPattern') -or
    -not $safeReadText.Contains('[regex]::Escape($Pattern)') -or
    -not $safeReadText.Contains('guard_code: 36') -or
    -not $safeReadText.Contains('guard_code: 37') -or
    -not $safeReadText.Contains('[int]$Last = 0') -or
    -not $safeReadText.Contains('guard_code: 42') -or
    -not $safeReadText.Contains('-Tail $Last')) {
    throw "safe-read must preserve its literal-pattern escape and guard contract."
}

$evalGuarded = $false
try {
    & "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action Eval -EvalCode 'var text = "quoted";'
} catch {
    $evalGuarded = $_.Exception.Message.Contains("guard_code: 25")
}
if (-not $evalGuarded) {
    throw "Quoted Eval was not rejected by guard_code: 25."
}

$assetPathGuarded = $false
try {
    & "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action AssetImport -AssetPath "Assets/AreaSurvivors/Editor/../Unsafe.cs"
} catch {
    $assetPathGuarded = $_.Exception.Message.Contains("must not contain '..'")
}
if (-not $assetPathGuarded) {
    throw "Asset path traversal was not rejected before Unity access."
}

$screenshotPathGuarded = $false
try {
    & "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action Screenshot -ScreenshotPath "../unsafe.png"
} catch {
    $screenshotPathGuarded = $_.Exception.Message.Contains("ScreenshotPath must be")
}
if (-not $screenshotPathGuarded) {
    throw "Unsafe screenshot output path was not rejected before Unity access."
}

$runnerGuarded = $false
try {
    & "$PSScriptRoot\invoke-unity-editor-runner.ps1" -Phase RegisterAndRun -ScriptPath "Assets/AreaSurvivors/Editor/__missing_command_tools_self_test__.cs" -MenuPath "Area Survivors/Self Test/Missing"
} catch {
    $runnerGuarded = $_.Exception.Message.Contains("Editor runner does not exist")
}
if (-not $runnerGuarded) {
    throw "Missing Editor runner was not rejected before Unity access."
}

$editorRunnerText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "invoke-unity-editor-runner.ps1"))
if (-not $editorRunnerText.Contains('DependencyScriptPaths -split ";"') -or -not $editorRunnerText.Contains('foreach ($importScriptPath in $importScriptPaths)')) {
    throw "Editor runner must explicitly import semicolon-delimited dependency scripts before Compile."
}
if (-not $editorRunnerText.Contains('last-asset-refresh.utc') -or ([regex]::Matches($editorRunnerText, 'Wait-ForCompileCooldown').Count -lt 3)) {
    throw "Editor runner must wait for the AssetImport/AssetRefresh cooldown before both Compile phases."
}
if (-not $editorRunnerText.Contains('[switch]$Concise') -or
    -not $editorRunnerText.Contains('$stepOutput | Select-Object -Last 40 | Write-Output') -or
    -not $editorRunnerText.Contains('if (-not $Concise)')) {
    throw "Editor runner concise mode must suppress successful step payloads and preserve bounded failure evidence."
}

$tokenSummaryText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Get-TokenReportSummary.ps1"))
if (-not $tokenSummaryText.Contains('timeout_seconds = $record.timeout_seconds') -or
    -not $tokenSummaryText.Contains('timed_out = [bool]$record.timed_out') -or
    -not $tokenSummaryText.Contains('capture_path = $record.capture_path') -or
    -not $tokenSummaryText.Contains('if ($FailedOnly)')) {
    throw "Token report summary JSON must preserve timeout and capture evidence for failed command diagnosis."
}

$startTaskText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "start-task-token-check.ps1"))
if (-not $startTaskText.Contains('$startArgs = @{ Note = $Task }')) {
    throw "start-task-token-check.ps1 must use hashtable splatting for named parameter forwarding."
}

$safeUnityText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Invoke-AreaSafeUnity.ps1"))
if (-not $safeUnityText.Contains("guard_code: 26") -or -not $safeUnityText.Contains("capture_path")) {
    throw "Named-pipe access denial must emit guard_code: 26 and a capture path."
}
if (-not $safeUnityText.Contains('$safeExitCode -ne 0 -and $captured -match "Access to the path is denied"')) {
    throw "Named-pipe access denial must be normalized for every safe-unity action, not only MenuExists."
}
$safeUnityEntryText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-unity.ps1"))
if (-not $safeUnityEntryText.Contains('[int]$MaxCount = 30') -or -not $safeUnityEntryText.Contains('-MaxCount $MaxCount')) {
    throw "safe-unity ConsoleErrors/ConsoleWarnings count contract must remain -MaxCount."
}
if (-not $safeUnityEntryText.Contains('"ConsoleLogs"') -or -not $safeUnityText.Contains('"ConsoleLogs"') -or -not $safeUnityText.Contains('--logType Log --maxCount $MaxCount')) {
    throw "safe-unity must expose bounded ConsoleLogs retrieval through the same -MaxCount contract."
}
if (-not $safeUnityText.Contains("guard_code: 27") -or -not $safeUnityText.Contains("Eval preflight") -or -not $safeUnityText.Contains("PlayMode.Status")) {
    throw "Eval must preflight Play Mode status and reject in-Play execution with guard_code: 27."
}
if (-not $safeUnityText.Contains("System.Threading.Mutex") -or -not $safeUnityText.Contains("guard_code: 34") -or -not $safeUnityText.Contains("AreaSurvivors.SafeUnity.Command")) {
    throw "safe-unity must reject concurrent Unity commands before contacting UniCLI."
}
if (-not $safeUnityText.Contains("last-asset-refresh.utc") -or -not $safeUnityText.Contains("guard_code: 35") -or -not $safeUnityText.Contains("CompileAfterRefreshCooldownSeconds")) {
    throw "safe-unity must block explicit Compile during AssetRefresh-triggered asynchronous compilation."
}
if ($safeUnityText.Contains("unicli exec Compile") -or -not $safeUnityText.Contains("verify-unity-script-compilation.ps1")) {
    throw "safe-unity Compile must verify auto-compiled assemblies externally and must not trigger UniCLI Compile/Domain Reload."
}
$compileVerifierPath = Join-Path $PSScriptRoot "verify-unity-script-compilation.ps1"
$compileVerifierText = [System.IO.File]::ReadAllText($compileVerifierPath)
if (-not $compileVerifierText.Contains("current via Bee artifact hash") -or -not $compileVerifierText.Contains("Get-FileHash")) {
    throw "Compile verification must accept a current, hash-identical Bee artifact when Unity preserves the ScriptAssemblies timestamp."
}
if (-not $compileVerifierText.Contains("guard_code: 41") -or -not $compileVerifierText.Contains("verification-only")) {
    throw "Compile verification must classify stale assemblies and explain the required import boundary."
}
if (-not $compileVerifierText.Contains('WaitTimeoutSeconds') -or
    -not $compileVerifierText.Contains('Start-Sleep -Milliseconds $PollIntervalMilliseconds') -or
    -not $safeUnityText.Contains('-WaitTimeoutSeconds $CompileWaitSeconds') -or
    -not $editorRunnerText.Contains('CompileWaitSeconds = [Math]::Max(0, $CompileTimeoutSeconds - 10)')) {
    throw "Editor Runner Compile must wait for background assembly freshness inside one bounded verification command."
}
$compileVerifierTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("area-compile-verifier-" + [guid]::NewGuid().ToString("N"))
try {
    $runtimeSourceRoot = Join-Path $compileVerifierTestRoot "Assets/Runtime"
    $editorSourceRoot = Join-Path $compileVerifierTestRoot "Assets/Editor"
    $scriptAssembliesRoot = Join-Path $compileVerifierTestRoot "Library/ScriptAssemblies"
    $beeRoot = Join-Path $compileVerifierTestRoot "Library/Bee/artifacts/self-test"
    [System.IO.Directory]::CreateDirectory($runtimeSourceRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($editorSourceRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($scriptAssembliesRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($beeRoot) | Out-Null

    $runtimeSource = Join-Path $runtimeSourceRoot "Runtime.cs"
    $editorSource = Join-Path $editorSourceRoot "Editor.cs"
    $runtimeAssembly = Join-Path $scriptAssembliesRoot "Assembly-CSharp.dll"
    $editorAssembly = Join-Path $scriptAssembliesRoot "Assembly-CSharp-Editor.dll"
    $runtimeBee = Join-Path $beeRoot "Assembly-CSharp.dll"
    $editorBee = Join-Path $beeRoot "Assembly-CSharp-Editor.dll"
    [System.IO.File]::WriteAllText($runtimeSource, "class RuntimeSelfTest {}")
    [System.IO.File]::WriteAllText($editorSource, "class EditorSelfTest {}")
    [System.IO.File]::WriteAllBytes($runtimeAssembly, [byte[]]@(1, 2, 3, 4))
    [System.IO.File]::WriteAllBytes($editorAssembly, [byte[]]@(5, 6, 7, 8))
    [System.IO.File]::WriteAllBytes($runtimeBee, [byte[]]@(1, 2, 3, 4))
    [System.IO.File]::WriteAllBytes($editorBee, [byte[]]@(5, 6, 7, 8))

    $oldUtc = [DateTime]::UtcNow.AddMinutes(-2)
    $sourceUtc = [DateTime]::UtcNow.AddMinutes(-1)
    $beeUtc = [DateTime]::UtcNow
    [System.IO.File]::SetLastWriteTimeUtc($runtimeAssembly, $oldUtc)
    [System.IO.File]::SetLastWriteTimeUtc($editorAssembly, $oldUtc)
    [System.IO.File]::SetLastWriteTimeUtc($runtimeSource, $sourceUtc)
    [System.IO.File]::SetLastWriteTimeUtc($editorSource, $sourceUtc)
    [System.IO.File]::SetLastWriteTimeUtc($runtimeBee, $beeUtc)
    [System.IO.File]::SetLastWriteTimeUtc($editorBee, $beeUtc)

    $compileVerifierOutput = (& $compileVerifierPath -ProjectRoot $compileVerifierTestRoot 2>&1) -join "`n"
    if (-not $compileVerifierOutput.Contains("runtime: current via Bee artifact hash") -or
        -not $compileVerifierOutput.Contains("editor: current via Bee artifact hash")) {
        throw "Compile verifier did not accept hash-identical Bee artifacts with preserved ScriptAssemblies timestamps."
    }
} finally {
    if (Test-Path -LiteralPath $compileVerifierTestRoot) {
        Remove-Item -LiteralPath $compileVerifierTestRoot -Recurse -Force
    }
}
$unityProcessReportText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "unity-process-report.ps1"))
if (-not $unityProcessReportText.Contains('[switch]$IncludeCommandLine') -or -not $unityProcessReportText.Contains('$MaxCommandLineLength')) {
    throw "unity-process-report must keep command lines opt-in and bounded."
}
$evalStaticGuardIndex = $safeUnityText.IndexOf('if ($Action -eq "Eval")', [System.StringComparison]::Ordinal)
$playExitCooldownIndex = $safeUnityText.IndexOf('if (Test-Path -LiteralPath $playExitMarkerPath)', [System.StringComparison]::Ordinal)
if ($evalStaticGuardIndex -lt 0 -or $playExitCooldownIndex -lt 0 -or $evalStaticGuardIndex -gt $playExitCooldownIndex) {
    throw "Static Eval argument guards must run before the PlayExit cooldown so self-tests never depend on Unity transition timing."
}

$safeSearchText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-search.ps1"))
if (-not $safeSearchText.Contains('[Alias("Query")]')) {
    throw "safe-search.ps1 must accept -Query as an alias for -Pattern."
}
if ($safeSearchText.Contains("rg -l --hidden") -and $safeSearchText.Contains('$pathArgs | Select-Object -First')) {
    throw "safe-search FilesOnly must fully capture rg output before applying the First limit."
}
if (-not $safeSearchText.Contains('`$items = @(rg -l') -or -not $safeSearchText.Contains('`$rgExit = `$LASTEXITCODE')) {
    throw "safe-search FilesOnly must capture all rg output and preserve its exit code before limiting output."
}
if (-not $safeSearchText.Contains('exit 0')) {
    throw "safe-search must normalize rg no-match exit code 1 to a successful empty result."
}
if (-not $safeSearchText.Contains("-g '!.sandbox-secrets/**'")) {
    throw "safe-search must exclude the restricted .codex/.sandbox-secrets subtree from broad roots."
}
$areaSafeCommandText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Invoke-AreaSafeCommand.ps1"))
if (-not $areaSafeCommandText.Contains('`$hits = @(rg -n') -or -not $areaSafeCommandText.Contains('`$rgExit = `$LASTEXITCODE')) {
    throw "Standard safe-search must fully capture rg output and its exit code before applying the First limit."
}
if ($areaSafeCommandText.Contains('$pathArgs | Select-Object -First')) {
    throw "Standard safe-search must not pipe native rg directly into Select-Object -First."
}

$safeSearchGlobGuarded = $false
try {
    & "$PSScriptRoot\safe-search.ps1" -Pattern "*Evolution*" -Path $PSScriptRoot -FilesOnly | Out-Null
} catch {
    $safeSearchGlobGuarded = $_.Exception.Message.Contains("regular expression, not a file glob")
}
if (-not $safeSearchGlobGuarded) {
    throw "safe-search must reject the common '*term*' file-glob misuse before invoking rg."
}

$safeSearchInvalidRegexGuarded = $false
try {
    & "$PSScriptRoot\safe-search.ps1" -Pattern "param(" -Path $PSScriptRoot -FilesOnly | Out-Null
} catch {
    $safeSearchInvalidRegexGuarded = $_.Exception.Message.Contains("guard_code: 43")
}
if (-not $safeSearchInvalidRegexGuarded) {
    throw "safe-search must reject invalid regular expressions before invoking rg."
}

$codexRoot = Join-Path $env:USERPROFILE ".codex"
if (Test-Path -LiteralPath $codexRoot) {
    $safeSearchCodexRootGuarded = $false
    try {
        & "$PSScriptRoot\safe-search.ps1" -Pattern "skill" -Path $codexRoot -FilesOnly | Out-Null
    } catch {
        $safeSearchCodexRootGuarded = $_.Exception.Message.Contains("guard_code: 44")
    }
    if (-not $safeSearchCodexRootGuarded) {
        throw "safe-search must reject the broad .codex root before invoking rg."
    }
}

$safeSearchSecondPathGuarded = $false
try {
    & "$PSScriptRoot\safe-search.ps1" -Pattern "param" -Path $PSScriptRoot -Extension "Assets/AreaSurvivors/Editor" | Out-Null
} catch {
    $safeSearchSecondPathGuarded = $_.Exception.Message.Contains("second -Path value was likely positionally bound")
}
if (-not $safeSearchSecondPathGuarded) {
    throw "safe-search must reject path-like values accidentally bound to -Extension."
}

$safeCommandText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Safe-Command.ps1"))
if (-not $safeCommandText.Contains('[Console]::OutputEncoding = `$utf8NoBom') -or -not $safeCommandText.Contains('`$OutputEncoding = `$utf8NoBom')) {
    throw "Safe-Command must force UTF-8 at the Windows PowerShell/native-command boundary."
}

$utf8SearchRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("area-safe-search-utf8-" + [guid]::NewGuid().ToString("N"))
try {
    [System.IO.Directory]::CreateDirectory($utf8SearchRoot) | Out-Null
    $utf8SearchFile = Join-Path $utf8SearchRoot "utf8-search.txt"
    $utf8Needle = -join ([char[]]@(0x6B66, 0x5668, 0x9032, 0x5316))
    $utf8Expected = $utf8Needle + "-utf8-self-test"
    [System.IO.File]::WriteAllText($utf8SearchFile, $utf8Expected, (New-Object System.Text.UTF8Encoding($false)))
    $utf8SearchOutput = (& "$PSScriptRoot\safe-search.ps1" -Pattern $utf8Needle -Path $utf8SearchRoot -First 5 -IncludeUnityYaml -PrintOutput 2>&1) -join "`n"
    if (-not $utf8SearchOutput.Contains($utf8Expected)) {
        throw "safe-search did not preserve UTF-8 text from native rg output."
    }
    $hitSummaryOutput = (& "$PSScriptRoot\safe-search.ps1" -Pattern $utf8Needle -Path $utf8SearchRoot -First 5 -HitSummary -IncludeUnityYaml -PrintOutput 2>&1) -join "`n"
    if (-not $hitSummaryOutput.Contains("utf8-search.txt")) {
        throw "safe-search HitSummary must emit explicit strings before exit so captured output is not empty."
    }
} finally {
    if (Test-Path -LiteralPath $utf8SearchRoot) {
        Remove-Item -LiteralPath $utf8SearchRoot -Recurse -Force
    }
}

$focusedSearchText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "focused-search.ps1"))
if (-not $focusedSearchText.Contains('exit 0') -or -not $focusedSearchText.Contains('Each -Path item must exist')) {
    throw "focused-search must validate paths and normalize no-match results."
}
if (-not $focusedSearchText.Contains('[Alias("First")]')) {
    throw "focused-search must accept -First as an alias for -TopFiles to match safe-search usage."
}
if (-not $focusedSearchText.Contains('[Alias("Include")]') -or -not $focusedSearchText.Contains('TrimStart("*").TrimStart(".")')) {
    throw "focused-search must bind -Include explicitly and normalize wildcard extension syntax."
}

$focusedPathGuarded = $false
try {
    & "$PSScriptRoot\focused-search.ps1" -Pattern "param" -Path "missing-one.cs,missing-two.cs" | Out-Null
} catch {
    $focusedPathGuarded = $_.Exception.Message.Contains("Each -Path item must exist")
}
if (-not $focusedPathGuarded) {
    throw "focused-search must reject missing or comma-joined paths before invoking rg."
}

$scopedDiffGuarded = $false
try {
    & "$PSScriptRoot\scoped-diff-check.ps1" -Path "__missing_scoped_diff_check__.cs" | Out-Null
} catch {
    $scopedDiffGuarded = $_.Exception.Message.Contains("Each -Path item must exist")
}
if (-not $scopedDiffGuarded) {
    throw "scoped-diff-check must reject missing paths before invoking git."
}

$scopedDiffCommaGuarded = $false
try {
    & "$PSScriptRoot\scoped-diff-check.ps1" -Path "one.cs,two.cs" | Out-Null
} catch {
    $scopedDiffCommaGuarded = $_.Exception.Message.Contains("Comma-delimited -Path values are not preserved")
}
if (-not $scopedDiffCommaGuarded) {
    throw "scoped-diff-check must reject comma-delimited paths and require its RTK-safe semicolon contract."
}

$scopedDiffText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "scoped-diff-check.ps1"))
if (-not $scopedDiffText.Contains('$rawItem -split ";"') -or -not $scopedDiffText.Contains('-Path $batch')) {
    throw "scoped-diff-check must normalize semicolon-delimited paths and forward bounded batches."
}
$scopedDiffCommand = Get-Command (Join-Path $PSScriptRoot "scoped-diff-check.ps1")
if (-not $scopedDiffCommand.Parameters.ContainsKey("Cached") -or -not $scopedDiffText.Contains('"--cached --check"')) {
    throw "scoped-diff-check must provide a captured -Cached contract for staged diff validation."
}
if (-not $scopedDiffCommand.Parameters.ContainsKey("ExcludeUnityMeta") -or -not $scopedDiffText.Contains('$file.Extension -ieq ".meta"')) {
    throw "scoped-diff-check must support excluding Unity-generated meta YAML from text whitespace validation."
}
if (-not $scopedDiffText.Contains('Formal usage: scoped-diff-check.ps1 -Path') -or $scopedDiffText.Contains('[string]$Mode')) {
    throw "scoped-diff-check must document its Path/Cached/ExcludeUnityMeta/PrintOutput contract and must not imply a Mode parameter."
}

$imageCopyWrapper = Join-Path (Split-Path $PSScriptRoot -Parent) "AssetGeneration\copy-generated-image-batch.ps1"
if (-not (Test-Path -LiteralPath $imageCopyWrapper -PathType Leaf)) {
    throw "Generated image copy must use the manifest-based batch wrapper."
}
$imageCopyText = [System.IO.File]::ReadAllText($imageCopyWrapper)
if ($imageCopyText.Contains('Invoke-Expression') -or $imageCopyText.Contains('powershell -Command') -or -not $imageCopyText.Contains('[switch]$ValidateOnly')) {
    throw "Generated image copy wrapper must avoid inline command evaluation and provide ValidateOnly preflight."
}

$unitySearchText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Invoke-AreaUnitySearch.ps1"))
if ($unitySearchText.Contains("-Action Eval") -or -not $unitySearchText.Contains("scene-prefab-search-query.txt")) {
    throw "Scene/prefab search must use the temporary query file and must not use Eval."
}
if (-not $unitySearchText.Contains("System.Threading.Mutex") -or -not $unitySearchText.Contains("guard_code: 30") -or -not $unitySearchText.Contains('Query: {0}')) {
    throw "Scene/prefab search must serialize shared query-file access and verify the exact Query header of a new report."
}
if (-not $unitySearchText.Contains("guard_code: 32") -or -not $unitySearchText.Contains("-Action PlayStatus")) {
    throw "Scene/prefab search must reject Play Mode before writing the shared query file."
}
$unitySearchWhitespaceGuarded = $false
try {
    & "$PSScriptRoot\Invoke-AreaUnitySearch.ps1" -Query " Icon" | Out-Null
} catch {
    $unitySearchWhitespaceGuarded = $_.Exception.Message.Contains("guard_code: 40")
}
if (-not $unitySearchWhitespaceGuarded) {
    throw "Scene/prefab search must reject leading or trailing query whitespace before Unity access."
}

$directEvalPattern = "unicli exec " + "Eval"
$unsafeDirectEvalScripts = Get-ChildItem -LiteralPath $PSScriptRoot -Filter "*.ps1" -File |
    Where-Object { $_.Name -ne "Invoke-AreaSafeUnity.ps1" } |
    Where-Object { [System.IO.File]::ReadAllText($_.FullName).Contains($directEvalPattern) }
if ($unsafeDirectEvalScripts.Count -gt 0) {
    $unsafeNames = ($unsafeDirectEvalScripts | ForEach-Object { $_.Name }) -join ", "
    throw "Direct UniCLI Eval bypasses the shared guard: $unsafeNames"
}

$safeReadText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-read.ps1"))
if (-not $safeReadText.Contains('suggested_max_matches=') -or -not $safeReadText.Contains('$maxInteractiveOutputLines / $linesPerMatch')) {
    throw "safe-read output guard must provide a calculated suggested_max_matches value."
}
if (-not $safeReadText.Contains('suggested_first=$maxInteractiveOutputLines')) {
    throw "safe-read output guard must provide suggested_first for oversized -First reads."
}
if (-not $safeReadText.Contains('suggested_end_line=$suggestedEndLine') -or
    -not $safeReadText.Contains('use_safe_read_batch=1')) {
    throw "safe-read output guard must route oversized line ranges to safe-read-batch."
}

Write-Output "command_tools_self_test: passed"
Write-Output ("parsed_scripts: {0}" -f $scripts.Count)
Write-Output "eval_quote_guard: passed"
Write-Output "asset_path_guard: passed"
Write-Output "screenshot_path_guard: passed"
Write-Output "editor_runner_preflight_guard: passed"
Write-Output "editor_runner_dependency_import_guard: passed"
Write-Output "named_parameter_splat_guard: passed"
Write-Output "named_pipe_diagnostic_guard: passed"
Write-Output "named_pipe_all_actions_guard: passed"
Write-Output "safe_unity_max_count_contract_guard: passed"
Write-Output "eval_play_mode_guard: passed"
Write-Output "unity_command_concurrency_guard: passed"
Write-Output "compile_after_refresh_guard: passed"
Write-Output "compile_no_domain_reload_guard: passed"
Write-Output "compile_freshness_wait_guard: passed"
Write-Output "unicli_main_editor_pid_guard: passed"
Write-Output "unity_process_report_output_guard: passed"
Write-Output "eval_static_guard_order: passed"
Write-Output "search_parameter_alias_guard: passed"
Write-Output "search_invalid_regex_guard: passed"
Write-Output "search_restricted_subtree_guard: passed"
Write-Output "search_codex_root_guard: passed"
Write-Output "search_files_only_limit_guard: passed"
Write-Output "focused_search_path_guard: passed"
Write-Output "focused_search_first_alias_guard: passed"
Write-Output "scoped_diff_path_guard: passed"
Write-Output "search_no_match_guard: passed"
Write-Output "safe_read_tail_guard: passed"
Write-Output "safe_read_output_suggestion_guard: passed"
Write-Output "safe_search_utf8_guard: passed"
Write-Output "unity_search_no_eval_guard: passed"
Write-Output "unity_search_concurrency_guard: passed"
Write-Output "unity_search_play_mode_guard: passed"
Write-Output "direct_unicli_eval_guard: passed"
$safeSearchContractText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-search.ps1"))
if (-not $safeSearchContractText.Contains('Use -Path for the search root. This wrapper does not accept -Root.') -or $safeSearchContractText.Contains('[Alias("Root")]')) {
    throw "safe-search must document Path as its only search-root parameter and must not accept the ambiguous Root alias."
}

$safeUnitySearchContractText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "safe-unity-search.ps1"))
if (-not $safeUnitySearchContractText.Contains('Formal usage: safe-unity-search.ps1 -Query') -or -not $safeUnitySearchContractText.Contains('does not accept -Path') -or -not $safeUnitySearchContractText.Contains('connects to Unity and executes an Editor Menu Reporter')) {
    throw "safe-unity-search must document its Query/PrintOutput-only contract."
}
