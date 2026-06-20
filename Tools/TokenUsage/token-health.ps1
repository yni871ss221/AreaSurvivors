param([switch]$FailOnIncrease, [switch]$IncludeUnity, [switch]$UpdateBaseline)
& "$PSScriptRoot\Invoke-AreaTokenHealth.ps1" -FailOnIncrease:$FailOnIncrease -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline
