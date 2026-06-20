param([Parameter(Mandatory = $true)][string]$Query, [switch]$PrintOutput)
& "$PSScriptRoot\Invoke-AreaUnitySearch.ps1" -Query $Query -PrintOutput:$PrintOutput
