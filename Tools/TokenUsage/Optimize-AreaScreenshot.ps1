param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$OutputPath = "",
    [int]$MaxWidth = 960,
    [int]$MaxHeight = 540,
    [switch]$CenterCrop,
    [int]$CropX = -1,
    [int]$CropY = -1,
    [int]$CropWidth = 0,
    [int]$CropHeight = 0,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Get-OutputPath {
    param([string]$InputPath, [string]$OutputPath)
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) { return $OutputPath }
    $directory = Join-Path (Get-Location) "TokenReports\Screenshots"
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)
    return Join-Path $directory ($name + "-lite.png")
}

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedOutput = Get-OutputPath -InputPath $resolvedInput -OutputPath $OutputPath
$image = [System.Drawing.Image]::FromFile($resolvedInput)

try {
    $sourceRect = [System.Drawing.Rectangle]::new(0, 0, $image.Width, $image.Height)
    if ($CropWidth -gt 0 -and $CropHeight -gt 0) {
        $x = if ($CropX -ge 0) { $CropX } else { [math]::Floor(($image.Width - $CropWidth) / 2) }
        $y = if ($CropY -ge 0) { $CropY } else { [math]::Floor(($image.Height - $CropHeight) / 2) }
        $x = [math]::Max(0, [math]::Min($x, $image.Width - 1))
        $y = [math]::Max(0, [math]::Min($y, $image.Height - 1))
        $w = [math]::Min($CropWidth, $image.Width - $x)
        $h = [math]::Min($CropHeight, $image.Height - $y)
        $sourceRect = [System.Drawing.Rectangle]::new($x, $y, $w, $h)
    } elseif ($CenterCrop) {
        $targetAspect = $MaxWidth / [double]$MaxHeight
        $sourceAspect = $image.Width / [double]$image.Height
        if ($sourceAspect -gt $targetAspect) {
            $w = [int]($image.Height * $targetAspect)
            $x = [int](($image.Width - $w) / 2)
            $sourceRect = [System.Drawing.Rectangle]::new($x, 0, $w, $image.Height)
        } else {
            $h = [int]($image.Width / $targetAspect)
            $y = [int](($image.Height - $h) / 2)
            $sourceRect = [System.Drawing.Rectangle]::new(0, $y, $image.Width, $h)
        }
    }

    $scale = [math]::Min($MaxWidth / [double]$sourceRect.Width, $MaxHeight / [double]$sourceRect.Height)
    $scale = [math]::Min(1.0, $scale)
    $width = [math]::Max(1, [int][math]::Round($sourceRect.Width * $scale))
    $height = [math]::Max(1, [int][math]::Round($sourceRect.Height * $scale))

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($image, [System.Drawing.Rectangle]::new(0, 0, $width, $height), $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
        } finally {
            $graphics.Dispose()
        }
        $outDir = Split-Path -Parent $resolvedOutput
        if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        }
        $bitmap.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }

    $inputInfo = Get-Item -LiteralPath $resolvedInput
    $outputInfo = Get-Item -LiteralPath $resolvedOutput
    $result = [pscustomobject]@{
        input = $resolvedInput
        output = $resolvedOutput
        original_width = $image.Width
        original_height = $image.Height
        output_width = $width
        output_height = $height
        input_bytes = $inputInfo.Length
        output_bytes = $outputInfo.Length
        crop = "$($sourceRect.X),$($sourceRect.Y),$($sourceRect.Width),$($sourceRect.Height)"
    }

    if ($Json) {
        $result | ConvertTo-Json -Depth 4
    } else {
        $result | Format-List
    }
} finally {
    $image.Dispose()
}
