param([switch]$FailOnIncrease, [switch]$IncludeRtk, [switch]$IncludeUnity, [switch]$UpdateBaseline)
& "$PSScriptRoot\Invoke-AreaTokenHealth.ps1" -FailOnIncrease:$FailOnIncrease -IncludeRtk:$IncludeRtk -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline
