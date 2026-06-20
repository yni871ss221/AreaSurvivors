param(
    [string]$File,
    [string]$Command,
    [ValidateSet("powershell", "cmd")]
    [string]$Shell = "powershell",
    [switch]$SaveReport,
    [string]$ReportPath,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "TokenUsageCommon.ps1")

if ([string]::IsNullOrWhiteSpace($File) -and [string]::IsNullOrWhiteSpace($Command)) {
    throw "Specify -File or -Command."
}

if (-not [string]::IsNullOrWhiteSpace($File) -and -not [string]::IsNullOrWhiteSpace($Command)) {
    throw "Specify only one of -File or -Command."
}

$source = ""
$text = ""
$exitCode = $null
$capturePath = $null
$stdoutPath = $null
$stderrPath = $null

if (-not [string]::IsNullOrWhiteSpace($File)) {
    $source = (Resolve-Path -LiteralPath $File).Path
    $text = [System.IO.File]::ReadAllText($source)
} else {
    $source = $Command
    $capturePath = Join-Path ([System.IO.Path]::GetTempPath()) ("token-estimate-" + [guid]::NewGuid().ToString("N") + ".txt")
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
}

$estimate = Get-TokenUsageEstimate -Text $text -Source $source
$record = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = if ($File) { "file" } else { "command_estimate" }
    command = $Command
    file = $File
    shell = if ($Command) { $Shell } else { $null }
    exit_code = $exitCode
    capture_path = $capturePath
    estimate = $estimate
    advice = Get-TokenUsageAdvice -Estimate $estimate
}

if ($SaveReport) {
    $writtenPath = Write-TokenUsageJsonLine -Record $record -ReportPath $ReportPath
    $record | Add-Member -NotePropertyName report_path -NotePropertyValue $writtenPath
}

if ($Json) {
    $record | ConvertTo-Json -Depth 8
} else {
    Write-Output ("source: {0}" -f $estimate.source)
    Write-Output ("estimated_tokens: {0}" -f $estimate.estimated_tokens)
    Write-Output ("risk: {0}" -f $estimate.risk)
    Write-Output ("lines: {0}, chars: {1}, bytes: {2}" -f $estimate.lines, $estimate.chars, $estimate.bytes)
    if ($null -ne $exitCode) { Write-Output ("exit_code: {0}" -f $exitCode) }
    if ($capturePath) { Write-Output ("captured_to: {0}" -f $capturePath) }
    Write-Output ("advice: {0}" -f $record.advice)
    if ($record.PSObject.Properties.Name -contains "report_path") { Write-Output ("report_path: {0}" -f $record.report_path) }
}
