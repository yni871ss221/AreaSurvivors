param([int]$Days = 7, [int]$Top = 10, [switch]$IncludeBenchmark, [switch]$Json)
& "$PSScriptRoot\Get-TokenReportSummary.ps1" -Days $Days -Top $Top -IncludeBenchmark:$IncludeBenchmark -Json:$Json
