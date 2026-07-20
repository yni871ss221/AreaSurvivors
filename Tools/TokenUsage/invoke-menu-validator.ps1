param(
    [Parameter(Mandatory = $true)][string]$MenuPath,
    [Parameter(Mandatory = $true)][string]$SuccessMarkerPath,
    [ValidateRange(1, 60)][int]$MarkerWaitSeconds = 20,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$projectPrefix = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd([char]92, [char]47) + [System.IO.Path]::DirectorySeparatorChar
$markerPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $SuccessMarkerPath))
if (-not $markerPath.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "SuccessMarkerPath must remain inside the project: $SuccessMarkerPath"
}

$startedUtc = [DateTime]::UtcNow
$safeUnityPath = Join-Path $PSScriptRoot "safe-unity.ps1"
$arguments = @{ Action = "Menu"; MenuPath = $MenuPath }
if ($PrintOutput) { $arguments.PrintOutput = $true }
& $safeUnityPath @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$deadlineUtc = $startedUtc.AddSeconds($MarkerWaitSeconds)
while ([DateTime]::UtcNow -lt $deadlineUtc) {
    if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
        $marker = Get-Item -LiteralPath $markerPath
        if ($marker.LastWriteTimeUtc -ge $startedUtc) {
            Write-Output "menu_validator_marker: $markerPath"
            Write-Output "menu_validator_completed: $MenuPath"
            exit 0
        }
    }
    Start-Sleep -Milliseconds 250
}

throw "Menu validator did not create a fresh success marker within $MarkerWaitSeconds seconds: $markerPath"
