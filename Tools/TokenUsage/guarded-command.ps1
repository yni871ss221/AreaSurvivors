param(
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [switch]$PrintOutput,
    [switch]$DryRun,
    [switch]$ExecuteOriginalIfSafe
)

& "$PSScriptRoot\Invoke-AreaGuardedCommand.ps1" -Command $Command -PrintOutput:$PrintOutput -DryRun:$DryRun -ExecuteOriginalIfSafe:$ExecuteOriginalIfSafe
