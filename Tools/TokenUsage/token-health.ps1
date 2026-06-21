param([switch]$FailOnIncrease, [switch]$IncludeUnity, [switch]$UpdateBaseline, [int]$Top = 5)
& "$PSScriptRoot\Invoke-AreaTokenHealth.ps1" -FailOnIncrease:$FailOnIncrease -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline -Top $Top
