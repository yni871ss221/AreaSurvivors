param(
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [ValidateSet("powershell", "cmd")]
    [string]$Shell = "powershell",
    [int]$WarnTokens = 3000,
    [int]$BlockTokens = 8000,
    [switch]$AllowHighOutput,
    [switch]$PrintOutput,
    [string]$ReportPath,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TokenUsageCommon.ps1")

$capturePath = Join-Path ([System.IO.Path]::GetTempPath()) ("safe-command-" + [guid]::NewGuid().ToString("N") + ".txt")
$exitCode = $null

if ($Shell -eq "cmd") {
    $stdoutPath = $capturePath + ".stdout"
    $stderrPath = $capturePath + ".stderr"
    & cmd.exe /d /c $Command 1> $stdoutPath 2> $stderrPath
    $exitCode = $LASTEXITCODE
    $parts = @()
    if (Test-Path -LiteralPath $stdoutPath) { $parts += [System.IO.File]::ReadAllText($stdoutPath) }
    if (Test-Path -LiteralPath $stderrPath) { $parts += [System.IO.File]::ReadAllText($stderrPath) }
    [System.IO.File]::WriteAllText($capturePath, ($parts -join "`n"))
} else {
    $escapedPath = $capturePath.Replace("'", "''")
    $script = "& { $Command } *>&1 | Out-File -LiteralPath '$escapedPath' -Encoding utf8; if (`$global:LASTEXITCODE -ne `$null) { exit `$global:LASTEXITCODE }"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command $script | Out-Null
    $exitCode = $LASTEXITCODE
}

$text = if (Test-Path -LiteralPath $capturePath) { [System.IO.File]::ReadAllText($capturePath) } else { "" }
$estimate = Get-TokenUsageEstimate -Text $text -Source $Command
$blocked = $estimate.estimated_tokens -ge $BlockTokens -and -not $AllowHighOutput
$warned = $estimate.estimated_tokens -ge $WarnTokens

$record = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = "safe_command"
    command = $Command
    shell = $Shell
    exit_code = $exitCode
    capture_path = $capturePath
    warn_tokens = $WarnTokens
    block_tokens = $BlockTokens
    blocked = $blocked
    estimate = $estimate
    advice = Get-TokenUsageAdvice -Estimate $estimate
}
$writtenPath = Write-TokenUsageJsonLine -Record $record -ReportPath $ReportPath
$record | Add-Member -NotePropertyName report_path -NotePropertyValue $writtenPath

if ($Json) {
    $record | ConvertTo-Json -Depth 8
} else {
    Write-Output ("command: {0}" -f $Command)
    Write-Output ("exit_code: {0}" -f $exitCode)
    Write-Output ("estimated_tokens: {0}" -f $estimate.estimated_tokens)
    Write-Output ("risk: {0}" -f $estimate.risk)
    Write-Output ("captured_to: {0}" -f $capturePath)
    Write-Output ("report_path: {0}" -f $writtenPath)
    Write-Output ("advice: {0}" -f $record.advice)
    if ($blocked) {
        Write-Output ("output: blocked because estimated tokens >= {0}; rerun with -AllowHighOutput only if intentional" -f $BlockTokens)
    } elseif ($warned -and -not $PrintOutput) {
        Write-Output ("output: hidden because estimated tokens >= {0}; use -PrintOutput to show intentionally" -f $WarnTokens)
    } else {
        Write-Output "output: hidden by default; use -PrintOutput to show"
    }
}

if ($PrintOutput -and -not $blocked) {
    Write-Output ""
    Write-Output "--- captured output ---"
    Write-Output $text
}
