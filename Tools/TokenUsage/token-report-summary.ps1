param([int]$Days = 7, [string]$Since = "", [string[]]$Kind = @(), [int]$Top = 10, [switch]$IncludeBenchmark, [switch]$Json)
& "$PSScriptRoot\Get-TokenReportSummary.ps1" -Days $Days -Since $Since -Kind $Kind -Top $Top -IncludeBenchmark:$IncludeBenchmark -Json:$Json
