param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$DestinationDirectory,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$resolvedSource = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\', '/')
$resolvedManifest = [System.IO.Path]::GetFullPath($ManifestPath)
$resolvedDestination = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $DestinationDirectory)).TrimEnd('\', '/')
$projectPrefix = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $resolvedSource -PathType Container)) {
    throw "SourceDirectory does not exist: $resolvedSource"
}
if (-not (Test-Path -LiteralPath $resolvedManifest -PathType Leaf)) {
    throw "ManifestPath does not exist: $resolvedManifest"
}
if (-not (Test-Path -LiteralPath $resolvedDestination -PathType Container)) {
    throw "DestinationDirectory does not exist: $resolvedDestination"
}
if (-not ($resolvedDestination + [System.IO.Path]::DirectorySeparatorChar).StartsWith($projectPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "DestinationDirectory must remain inside the project: $resolvedDestination"
}

$rows = @(Import-Csv -LiteralPath $resolvedManifest -Encoding UTF8)
if ($rows.Count -eq 0) {
    throw "Manifest must contain at least one source,destination row: $resolvedManifest"
}

$operations = @()
$seenDestinations = @{}
foreach ($row in $rows) {
    $sourceName = [string]$row.source
    $destinationName = [string]$row.destination
    if ([string]::IsNullOrWhiteSpace($sourceName) -or [string]::IsNullOrWhiteSpace($destinationName)) {
        throw "Every manifest row requires non-empty source and destination columns."
    }
    if ([System.IO.Path]::IsPathRooted($sourceName) -or [System.IO.Path]::IsPathRooted($destinationName) -or
        $sourceName.Contains("..") -or $destinationName.Contains("..")) {
        throw "Manifest paths must be relative and must not contain '..': $sourceName -> $destinationName"
    }
    if ([System.IO.Path]::GetExtension($sourceName) -ne ".png" -or
        [System.IO.Path]::GetExtension($destinationName) -ne ".png") {
        throw "Manifest source and destination must both be PNG files: $sourceName -> $destinationName"
    }

    $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $resolvedSource $sourceName))
    $destinationPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedDestination $destinationName))
    $sourcePrefix = $resolvedSource + [System.IO.Path]::DirectorySeparatorChar
    $destinationPrefix = $resolvedDestination + [System.IO.Path]::DirectorySeparatorChar
    if (-not $sourcePath.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $destinationPath.StartsWith($destinationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escaped its declared directory: $sourceName -> $destinationName"
    }
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Generated source image does not exist: $sourcePath"
    }
    if (Test-Path -LiteralPath $destinationPath) {
        throw "Destination already exists; generated source copy will not overwrite it: $destinationPath"
    }
    if ($seenDestinations.ContainsKey($destinationPath)) {
        throw "Manifest contains a duplicate destination: $destinationPath"
    }
    $seenDestinations[$destinationPath] = $true
    $operations += [PSCustomObject]@{ Source = $sourcePath; Destination = $destinationPath }
}

if ($ValidateOnly) {
    Write-Output "copy_generated_image_batch_validate: passed"
    Write-Output "items: $($operations.Count)"
    Write-Output "destination: $resolvedDestination"
    exit 0
}

foreach ($operation in $operations) {
    Copy-Item -LiteralPath $operation.Source -Destination $operation.Destination
}
Write-Output "copy_generated_image_batch: completed"
Write-Output "items: $($operations.Count)"
Write-Output "destination: $resolvedDestination"
