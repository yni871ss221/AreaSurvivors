param(
    [string[]]$Path = @(),
    [string]$RefRange = "",
    [switch]$Stat,
    [switch]$NameOnly,
    [switch]$SummaryOnly,
    [ValidateRange(1, 200)][int]$MaxLines = 80,
    [switch]$PrintOutput
)

if ($SummaryOnly) {
    Write-Output "[safe-diff] changed files"
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action DiffNameOnly -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
    Write-Output ""
    Write-Output "[safe-diff] stat"
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action DiffStat -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
    exit $LASTEXITCODE
}

$action = if ($NameOnly) { "DiffNameOnly" } elseif ($Stat) { "DiffStat" } else { "Diff" }
$containsUnityText = @($Path | Where-Object {
        [System.IO.Path]::GetExtension($_) -in @(".unity", ".prefab", ".asset")
    }).Count -gt 0
$effectiveMaxLines = if ($containsUnityText) {
    [Math]::Min($MaxLines, 40)
} else {
    $MaxLines
}
& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" `
    -Action $action `
    -Path $Path `
    -RefRange $RefRange `
    -First $effectiveMaxLines `
    -PrintOutput:$PrintOutput
