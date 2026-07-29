[CmdletBinding()]
param(
    [string]$MenuPath = "",
    [ValidateRange(1, 60)][int]$ResultWaitSeconds = 20,
    [switch]$PrintOutput,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$schemaVersion = 1
$bridgeMenuPath =
    "Area Survivors/Internal/Execute Structured Validator Request"
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validationRoot = Join-Path $projectRoot "Library\AreaValidation"
$requestPath = Join-Path $validationRoot "pending-request.json"
$resultRoot = Join-Path $validationRoot "Results"

function Write-Utf8Json {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $parent = Split-Path $Path -Parent
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $temporaryPath = $Path + ".tmp"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        ($Value | ConvertTo-Json -Depth 8),
        $utf8NoBom
    )
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Get-ValidatorId {
    param([Parameter(Mandatory = $true)][string]$TargetMenuPath)

    $value = $TargetMenuPath -replace "^Area Survivors/Validate/", ""
    $value = ($value -replace "[^A-Za-z0-9]+", "-").Trim("-").ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "MenuPath cannot be converted to a validator_id: $TargetMenuPath"
    }
    return $value
}

function Read-ValidationResult {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedRunId,
        [Parameter(Mandatory = $true)][string]$ExpectedMenuPath
    )

    $result = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 |
        ConvertFrom-Json
    foreach ($requiredProperty in @(
            "schema_version",
            "run_id",
            "validator_id",
            "menu_path",
            "status",
            "check_count",
            "failed_count",
            "warning_count",
            "error_count",
            "issues"
        )) {
        if ($result.PSObject.Properties.Name -notcontains $requiredProperty) {
            throw "Validation result is missing property: $requiredProperty"
        }
    }
    if ([int]$result.schema_version -ne $schemaVersion) {
        throw "Validation result schema_version is unsupported."
    }
    if ([string]$result.run_id -ne $ExpectedRunId) {
        throw "Validation result run_id does not match the request."
    }
    if ([string]$result.menu_path -ne $ExpectedMenuPath) {
        throw "Validation result menu_path does not match the request."
    }
    if ([string]$result.status -notin @("passed", "failed", "error")) {
        throw "Validation result status is invalid: $($result.status)"
    }
    return $result
}

function Write-ResultOutput {
    param(
        [Parameter(Mandatory = $true)][object]$Result,
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$Expanded
    )

    $checkText = if ([bool]$Result.check_count_known) {
        [string]$Result.check_count
    } else {
        "unknown"
    }
    Write-Output (
        "validator_result: status={0}; validator_id={1}; checks={2}; failed={3}; warnings={4}; errors={5}; duration_ms={6}" -f
        $Result.status,
        $Result.validator_id,
        $checkText,
        $Result.failed_count,
        $Result.warning_count,
        $Result.error_count,
        $Result.duration_ms
    )
    if ([bool]$Result.check_count_known) {
        Write-Output "validator_check_count: $($Result.check_count)"
    }
    Write-Output "validator_result_path: $Path"

    $issues = @($Result.issues)
    $issueLimit = if ($Expanded) { 20 } else { 5 }
    foreach ($issue in @($issues | Select-Object -First $issueLimit)) {
        if (-not $Expanded -and [string]$issue.severity -eq "warning") {
            continue
        }
        $message = ([string]$issue.message -replace "[`r`n]+", " ").Trim()
        Write-Output (
            "validator_issue: severity={0}; code={1}; subject={2}; message={3}" -f
            $issue.severity,
            $issue.code,
            $issue.subject,
            $message
        )
    }
    if ($issues.Count -gt $issueLimit) {
        Write-Output (
            "validator_issue_omitted: {0}" -f ($issues.Count - $issueLimit)
        )
    }
    Write-Output (
        "area_tool_data_json: " +
        ($Result | ConvertTo-Json -Depth 8 -Compress)
    )
}

if ($SelfTest) {
    $testMenuPath = "Area Survivors/Validate/Self Test Fixture"
    $testPath = [System.IO.Path]::GetTempFileName()
    try {
        foreach ($testStatus in @("passed", "failed", "error")) {
            $testRunId = [guid]::NewGuid().ToString("N")
            Write-Utf8Json -Path $testPath -Value ([pscustomobject]@{
                    schema_version = $schemaVersion
                    run_id = $testRunId
                    validator_id = "self-test-fixture"
                    menu_path = $testMenuPath
                    status = $testStatus
                    adapter_mode = "fixture"
                    started_at = [DateTime]::UtcNow.ToString("o")
                    finished_at = [DateTime]::UtcNow.ToString("o")
                    duration_ms = 1
                    check_count_known = $true
                    check_count = 1
                    passed_count = [int]($testStatus -eq "passed")
                    failed_count = [int]($testStatus -eq "failed")
                    warning_count = 0
                    error_count = [int]($testStatus -eq "error")
                    issues = @()
                })
            $testResult = Read-ValidationResult `
                -Path $testPath `
                -ExpectedRunId $testRunId `
                -ExpectedMenuPath $testMenuPath
            if ($testResult.status -ne $testStatus) {
                throw "Structured validation status was not preserved: $testStatus"
            }
        }

        $mismatchRejected = $false
        try {
            Read-ValidationResult `
                -Path $testPath `
                -ExpectedRunId ([guid]::NewGuid().ToString("N")) `
                -ExpectedMenuPath $testMenuPath |
                Out-Null
        } catch {
            $mismatchRejected = $_.Exception.Message -match
                "run_id does not match"
        }
        if (-not $mismatchRejected -or
            (Get-ValidatorId -TargetMenuPath $testMenuPath) -ne
                "self-test-fixture") {
            throw "Structured validation run identity self-test failed."
        }
    } finally {
        Remove-Item -LiteralPath $testPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath ($testPath + ".tmp") `
            -Force `
            -ErrorAction SilentlyContinue
    }
    Write-Output "menu_validator_self_test: passed"
    return
}

if ([string]::IsNullOrWhiteSpace($MenuPath)) {
    throw "MenuPath is required."
}
if ($MenuPath -eq $bridgeMenuPath) {
    throw "MenuPath cannot target the structured validation bridge itself."
}

$runId = [guid]::NewGuid().ToString("N")
$validatorId = Get-ValidatorId -TargetMenuPath $MenuPath
$resultPath = Join-Path $resultRoot ($runId + ".json")
$request = [pscustomobject][ordered]@{
    schema_version = $schemaVersion
    run_id = $runId
    validator_id = $validatorId
    menu_path = $MenuPath
    requested_at = [DateTime]::UtcNow.ToString("o")
}
Write-Utf8Json -Path $requestPath -Value $request

$safeUnityPath = Join-Path $PSScriptRoot "safe-unity.ps1"
$unityOutput = @(
    & $safeUnityPath -Action Menu -MenuPath $bridgeMenuPath
)
$unityExitCode = $LASTEXITCODE

$deadlineUtc = [DateTime]::UtcNow.AddSeconds($ResultWaitSeconds)
while ([DateTime]::UtcNow -lt $deadlineUtc -and
    -not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    $capturePath = ""
    $captureMatch = [regex]::Match(
        ($unityOutput -join "`n"),
        "(?m)^(?:captured_to|capture_path):\s*(.+)$"
    )
    if ($captureMatch.Success) {
        $capturePath = $captureMatch.Groups[1].Value.Trim()
    }
    Write-Output (
        "validator_result: status=infrastructure_failure; validator_id={0}; unity_exit_code={1}" -f
        $validatorId,
        $unityExitCode
    )
    if (-not [string]::IsNullOrWhiteSpace($capturePath)) {
        Write-Output "capture_path: $capturePath"
    }
    Write-Output "validator_result_path: $resultPath"
    exit 33
}

$result = Read-ValidationResult `
    -Path $resultPath `
    -ExpectedRunId $runId `
    -ExpectedMenuPath $MenuPath
Write-ResultOutput -Result $result -Path $resultPath -Expanded:$PrintOutput

switch ([string]$result.status) {
    "passed" { exit 0 }
    "failed" { exit 31 }
    default { exit 32 }
}
