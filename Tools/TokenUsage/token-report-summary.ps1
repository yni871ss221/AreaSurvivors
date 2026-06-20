param([int]$Days = 7, [int]$Top = 10, [switch]$ExcludeBenchmark, [switch]$Json)
& "$PSScriptRoot\Get-TokenReportSummary.ps1" -Days $Days -Top $Top -ExcludeBenchmark:$ExcludeBenchmark -Json:$Json
