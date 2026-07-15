<#
.SYNOPSIS
Runs git diff --check for an explicit set of existing files.

.DESCRIPTION
Formal usage: scoped-diff-check.ps1 -Path "path/a.cs;path/b.cs" [-Cached] [-ExcludeUnityMeta] [-PrintOutput]
This wrapper does not provide -Mode or summary output. Use the dedicated safe diff
entry point when a stat/name summary is required.
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Path,
    [switch]$Cached,
    [switch]$ExcludeUnityMeta,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"
$normalizedPaths = @()
foreach ($rawItem in $Path) {
    if ($rawItem.Contains(",")) {
        throw "Comma-delimited -Path values are not preserved through the RTK/PowerShell -File boundary. Pass one quoted semicolon-delimited value instead."
    }
    foreach ($item in ($rawItem -split ";")) {
        if (-not [string]::IsNullOrWhiteSpace($item)) {
            $normalizedPaths += $item.Trim()
        }
    }
}

foreach ($item in $normalizedPaths) {
    if ([string]::IsNullOrWhiteSpace($item) -or -not (Test-Path -LiteralPath $item)) {
        throw "Each -Path item must exist before git diff --check: $item"
    }
}

$checkPaths = @($normalizedPaths)
if ($ExcludeUnityMeta) {
    $projectRoot = [System.IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\', '/')
    $checkPaths = @()
    foreach ($item in $normalizedPaths) {
        if (Test-Path -LiteralPath $item -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $item -Recurse -File) {
                if ($file.Extension -ieq ".meta") { continue }
                $fullPath = [System.IO.Path]::GetFullPath($file.FullName)
                if (-not $fullPath.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Expanded diff path must remain inside the project: $fullPath"
                }
                $checkPaths += $fullPath.Substring($projectRoot.Length + 1)
            }
        } elseif ([System.IO.Path]::GetExtension($item) -ine ".meta") {
            $checkPaths += $item
        }
    }
    if ($checkPaths.Count -eq 0) {
        throw "-ExcludeUnityMeta left no files to validate."
    }
}

$diffArgs = if ($Cached) { "--cached --check" } else { "--check" }
for ($offset = 0; $offset -lt $checkPaths.Count; $offset += 40) {
    $batchEnd = [Math]::Min($checkPaths.Count - 1, $offset + 39)
    $batch = @($checkPaths[$offset..$batchEnd])
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Diff -Path $batch -ExtraArgs $diffArgs -PrintOutput:$PrintOutput
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit 0
