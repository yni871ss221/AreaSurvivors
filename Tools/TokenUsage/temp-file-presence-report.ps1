param(
    [Parameter(Mandatory = $true)][string]$Path
)

$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Temp\AgentAssets"))
$candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
    [System.IO.Path]::GetFullPath($Path)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
}

$allowedPrefix = $allowedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $candidate.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Path must remain under Temp/AgentAssets (guard_code: 46): $Path"
}

$exists = Test-Path -LiteralPath $candidate -PathType Leaf
Write-Output ("temp_file_exists: " + $exists.ToString().ToLowerInvariant())
Write-Output ("path: " + $candidate)
