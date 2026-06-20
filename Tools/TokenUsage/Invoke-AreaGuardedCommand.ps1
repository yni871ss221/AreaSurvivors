param(
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [switch]$PrintOutput,
    [switch]$DryRun,
    [switch]$ExecuteOriginalIfSafe,
    [int]$WarnTokens = 3000,
    [int]$BlockTokens = 8000
)

$ErrorActionPreference = "Stop"

$converterPath = Join-Path $PSScriptRoot "Convert-AreaCommandToSafe.ps1"
$safeCommandPath = Join-Path $PSScriptRoot "Safe-Command.ps1"
$conversion = & $converterPath -Command $Command -PrintOutput:$PrintOutput

Write-Output ("original: {0}" -f $conversion.original_command)
Write-Output ("converted: {0}" -f $conversion.converted)
Write-Output ("reason: {0}" -f $conversion.reason)

if ($conversion.converted) {
    Write-Output ("safe_command: {0}" -f $conversion.safe_command)
    if ($DryRun) { exit 0 }
    Invoke-Expression $conversion.safe_command
    exit $LASTEXITCODE
}

if (-not $ExecuteOriginalIfSafe) {
    Write-Output "output: no conversion rule matched; original command was not executed. Use -ExecuteOriginalIfSafe to run through Safe-Command."
    exit 2
}

$argsForSafe = @{
    Command = $Command
    WarnTokens = $WarnTokens
    BlockTokens = $BlockTokens
}
if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
& $safeCommandPath @argsForSafe
