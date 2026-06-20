param([Parameter(Mandatory = $true)][string]$Path, [int]$First = 120, [switch]$PrintOutput)
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Read -Path $Path -First $First -PrintOutput:$PrintOutput
