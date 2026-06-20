param(
    [string]$BaseRef = "e68ca9a",
    [string]$HeadRef = "2771c1c",
    [switch]$IncludeRtk,
    [switch]$IncludeUnity,
    [switch]$UpdateBaseline
)

& "$PSScriptRoot\Run-TokenBenchmark.ps1" -BaseRef $BaseRef -HeadRef $HeadRef -IncludeRtk:$IncludeRtk -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline
