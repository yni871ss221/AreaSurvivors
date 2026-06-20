param([string[]]$Path = @(), [switch]$PrintOutput)
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Status -Path $Path -PrintOutput:$PrintOutput
