param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [Parameter(Mandatory = $true)]
    [string]$Ranges,
    [switch]$PrintOutput,
    [switch]$AllowMany
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "safe-read-batch path must be an existing file (guard_code: 33): $Path"
}

$rangeItems = @($Ranges -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
if ($rangeItems.Count -lt 1 -or $rangeItems.Count -gt 8) {
    throw "safe-read-batch requires 1 to 8 semicolon- or comma-separated ranges (guard_code: 38)."
}

$safeReadPath = Join-Path $PSScriptRoot "safe-read.ps1"
foreach ($rangeItem in $rangeItems) {
    if ($rangeItem -notmatch '^(\d+)-(\d+)$') {
        throw "safe-read-batch range must use start-end syntax (guard_code: 38): $rangeItem"
    }

    $startLine = [int]$Matches[1]
    $endLine = [int]$Matches[2]
    if ($startLine -lt 1 -or $endLine -lt $startLine) {
        throw "safe-read-batch range is invalid (guard_code: 38): $rangeItem"
    }

    $chunkStart = $startLine
    while ($chunkStart -le $endLine) {
        # Interactive output remains bounded even if callers mistakenly combine
        # -AllowMany with -PrintOutput. Remove -PrintOutput to request one large
        # capture-only range intentionally.
        $chunkEnd = if ($AllowMany -and -not $PrintOutput) {
            $endLine
        } else {
            [Math]::Min($endLine, $chunkStart + 79)
        }
        Write-Output "safe_read_batch_range: $chunkStart-$chunkEnd"
        $arguments = @{
            Path = $Path
            StartLine = $chunkStart
            EndLine = $chunkEnd
        }
        if ($PrintOutput) { $arguments.PrintOutput = $true }
        if ($AllowMany -and -not $PrintOutput) { $arguments.AllowMany = $true }
        & $safeReadPath @arguments
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $chunkStart = $chunkEnd + 1
    }
}
