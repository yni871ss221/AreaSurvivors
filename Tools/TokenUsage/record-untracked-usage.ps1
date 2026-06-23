param(
    [string]$Category = "note",
    [string]$Source = "",
    [string]$Note = "",
    [string]$Text = "",
    [string]$ImagePath = "",
    [int]$Count = 1,
    [int]$EstimatedTokens = 0,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TokenUsageCommon.ps1"

if ($Count -lt 1) { $Count = 1 }

$estimateSource = $Category
$bytes = 0
$chars = 0
$lines = 0
$words = 0
$tokens = $EstimatedTokens
$imageInfo = $null

if (-not [string]::IsNullOrWhiteSpace($Text)) {
    $textEstimate = Get-TokenUsageEstimate -Text $Text -Source $Category
    $estimateSource = $textEstimate.source
    $bytes = $textEstimate.bytes
    $chars = $textEstimate.chars
    $lines = $textEstimate.lines
    $words = $textEstimate.words
    if ($tokens -le 0) { $tokens = [int]$textEstimate.estimated_tokens }
}

if (-not [string]::IsNullOrWhiteSpace($ImagePath)) {
    $resolvedImagePath = Resolve-Path -LiteralPath $ImagePath
    $file = Get-Item -LiteralPath $resolvedImagePath
    $bytes = [int64]$file.Length
    $estimateSource = "image:$resolvedImagePath"
    try {
        Add-Type -AssemblyName System.Drawing
        $image = [System.Drawing.Image]::FromFile($resolvedImagePath)
        try {
            $width = [int]$image.Width
            $height = [int]$image.Height
            $pixelEstimate = [int][math]::Ceiling(($width * $height) / 750.0) + 85
            if ($tokens -le 0) { $tokens = $pixelEstimate * $Count }
            $imageInfo = [pscustomobject]@{
                path = [string]$resolvedImagePath
                width = $width
                height = $height
                bytes = $bytes
                count = $Count
                heuristic = "ceil(width*height/750)+85 per image"
            }
        } finally {
            $image.Dispose()
        }
    } catch {
        if ($tokens -le 0) {
            $tokens = [int][math]::Ceiling(($bytes / 1024.0) * 2.0) * $Count
        }
        $imageInfo = [pscustomobject]@{
            path = [string]$resolvedImagePath
            bytes = $bytes
            count = $Count
            heuristic = "fallback ceil(kilobytes*2) per image"
        }
    }
}

if ($tokens -le 0) { $tokens = 0 }
$risk = Get-TokenUsageRisk -Source $estimateSource -Text $Text -EstimatedTokens $tokens

$record = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = "manual_untracked_usage"
    category = $Category
    command = "record-untracked-usage"
    shell = "powershell"
    exit_code = 0
    capture_path = ""
    warn_tokens = 0
    block_tokens = 0
    blocked = $false
    source = $Source
    note = $Note
    count = $Count
    image = $imageInfo
    estimate = [pscustomobject]@{
        source = $estimateSource
        bytes = $bytes
        chars = $chars
        lines = $lines
        words = $words
        estimated_tokens = $tokens
        risk = $risk
    }
    advice = "Manual estimate for Codex usage that command reports cannot see, such as chat, screenshots, fixed context, reasoning, or direct tool output."
}

$path = Write-TokenUsageJsonLine -Record $record

if ($Json) {
    $record | Add-Member -NotePropertyName report_path -NotePropertyValue $path
    $record | ConvertTo-Json -Depth 8
    exit 0
}

Write-Output ("recorded_manual_untracked_usage: {0} tokens ({1})" -f $tokens, $Category)
if (-not [string]::IsNullOrWhiteSpace($Note)) {
    Write-Output ("note: {0}" -f $Note)
}
Write-Output ("saved_to: {0}" -f $path)
