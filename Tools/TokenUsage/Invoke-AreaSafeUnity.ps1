param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Compile", "ConsoleErrors", "Menu", "Eval")]
    [string]$Action,
    [string]$MenuPath = "",
    [string]$EvalCode = "",
    [int]$MaxCount = 30,
    [int]$WarnTokens = 3000,
    [int]$BlockTokens = 8000,
    [switch]$PrintOutput,
    [switch]$AllowHighOutput
)

$ErrorActionPreference = "Stop"

function Quote-Value {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$safeCommandPath = Join-Path $PSScriptRoot "Safe-Command.ps1"
$command = ""

switch ($Action) {
    "Compile" {
        $command = "unicli exec Compile"
    }
    "ConsoleErrors" {
        $command = "unicli exec Console.GetLog --logType Error --maxCount $MaxCount"
    }
    "Menu" {
        if ([string]::IsNullOrWhiteSpace($MenuPath)) { throw "Menu requires -MenuPath." }
        $command = "unicli exec Menu.Execute --menuItemPath $(Quote-Value $MenuPath)"
    }
    "Eval" {
        if ([string]::IsNullOrWhiteSpace($EvalCode)) { throw "Eval requires -EvalCode." }
        if ($EvalCode.Length -gt 500) {
            throw "EvalCode is too long for safe inline execution. Create a temporary Editor runner and call a short method instead."
        }
        $command = "unicli exec Eval --code $(Quote-Value $EvalCode)"
    }
}

$argsForSafe = @{
    Command = $command
    WarnTokens = $WarnTokens
    BlockTokens = $BlockTokens
}
if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
if ($AllowHighOutput) { $argsForSafe.AllowHighOutput = $true }

& $safeCommandPath @argsForSafe
