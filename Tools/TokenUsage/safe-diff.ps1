param([string[]]$Path = @(), [string]$RefRange = "", [switch]$Stat, [switch]$NameOnly, [switch]$SummaryOnly, [switch]$PrintOutput)

if ($SummaryOnly) {
    Write-Output "[safe-diff] changed files"
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action DiffNameOnly -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
    Write-Output ""
    Write-Output "[safe-diff] stat"
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action DiffStat -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
    exit $LASTEXITCODE
}

$action = if ($NameOnly) { "DiffNameOnly" } elseif ($Stat) { "DiffStat" } else { "Diff" }
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action $action -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
