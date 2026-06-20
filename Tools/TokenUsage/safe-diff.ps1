param([string[]]$Path = @(), [string]$RefRange = "", [switch]$Stat, [switch]$NameOnly, [switch]$PrintOutput)
$action = if ($NameOnly) { "DiffNameOnly" } elseif ($Stat) { "DiffStat" } else { "Diff" }
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action $action -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
