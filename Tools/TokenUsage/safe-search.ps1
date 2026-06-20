param([Parameter(Mandatory = $true)][string]$Pattern, [string[]]$Path = @("Assets", "Tools", "AGENTS.md"), [int]$First = 120, [switch]$PrintOutput)
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Search -Pattern $Pattern -Path $Path -First $First -PrintOutput:$PrintOutput
