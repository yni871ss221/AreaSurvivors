param(
    [Parameter(Mandatory = $true)][string]$Pattern,
    [string[]]$Path = @("Assets/AreaSurvivors"),
    [int]$TopFiles = 3,
    [int]$Context = 12,
    [int]$MaxMatchesPerFile = 2,
    [string[]]$Extension = @("cs"),
    [switch]$IncludeUnityYaml,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

function Quote-PowerShellValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$extraArgs = @()
foreach ($ext in $Extension) {
    $trimmed = $ext.Trim().TrimStart(".")
    if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
        $extraArgs += "-g"
        $extraArgs += Quote-PowerShellValue "*.$trimmed"
    }
}
if (-not $IncludeUnityYaml) {
    $extraArgs += "-g '!*.unity'"
    $extraArgs += "-g '!*.prefab'"
    $extraArgs += "-g '!*.asset'"
}

$pathArgs = ($Path | ForEach-Object { Quote-PowerShellValue $_ }) -join " "
$patternArg = Quote-PowerShellValue $Pattern
$extra = $extraArgs -join " "
$command = "rg -l --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs | Select-Object -First $TopFiles"
$json = & "$PSScriptRoot\Safe-Command.ps1" -Command $command -Json
$record = $json | ConvertFrom-Json
$filesText = if (Test-Path -LiteralPath $record.capture_path) { Get-Content -LiteralPath $record.capture_path -Encoding UTF8 } else { @() }
$files = @($filesText | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

Write-Output "Focused files:"
foreach ($file in $files) { Write-Output ("- {0}" -f $file) }

foreach ($file in $files) {
    Write-Output ""
    Write-Output ("[focused-search] {0}" -f $file)
    & "$PSScriptRoot\safe-read.ps1" -Path $file -Pattern $Pattern -Context $Context -MaxMatches $MaxMatchesPerFile -PrintOutput:$PrintOutput
}
