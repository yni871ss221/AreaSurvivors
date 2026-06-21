param([int]$Days = 7, [string]$Since = "", [string[]]$Kind = @(), [int]$Top = 10, [int]$Recent = 0, [switch]$SinceLastStart, [switch]$IncludeBenchmark, [switch]$Json)
& "$PSScriptRoot\Get-TokenReportSummary.ps1" -Days $Days -Since $Since -Kind $Kind -Top $Top -Recent $Recent -SinceLastStart:$SinceLastStart -IncludeBenchmark:$IncludeBenchmark -Json:$Json
