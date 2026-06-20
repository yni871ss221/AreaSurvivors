param(
    [string]$ReportDirectory = "TokenReports",
    [int]$OlderThanDays = 1,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReportDirectory)) {
    Write-Output "No TokenReports directory found."
    exit 0
}

$archiveDirectory = Join-Path $ReportDirectory "Archive"
if (-not (Test-Path -LiteralPath $archiveDirectory)) {
    New-Item -ItemType Directory -Force -Path $archiveDirectory | Out-Null
}

$cutoff = (Get-Date).Date.AddDays(-[math]::Max(0, $OlderThanDays))
$files = @(Get-ChildItem -LiteralPath $ReportDirectory -Filter "*.jsonl" -File | Where-Object { $_.LastWriteTime -lt $cutoff })

foreach ($file in $files) {
    $target = Join-Path $archiveDirectory $file.Name
    if (Test-Path -LiteralPath $target) {
        $stamp = $file.LastWriteTime.ToString("yyyyMMdd-HHmmss")
        $target = Join-Path $archiveDirectory ("{0}-{1}{2}" -f [System.IO.Path]::GetFileNameWithoutExtension($file.Name), $stamp, $file.Extension)
    }
    if ($WhatIf) {
        Write-Output ("would_archive: {0} -> {1}" -f $file.FullName, $target)
    } else {
        Move-Item -LiteralPath $file.FullName -Destination $target
        Write-Output ("archived: {0} -> {1}" -f $file.Name, $target)
    }
}

Write-Output ("archived_count: {0}" -f $files.Count)
