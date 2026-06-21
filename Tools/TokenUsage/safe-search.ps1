param(
    [Parameter(Mandatory = $true)][string]$Pattern,
    [string[]]$Path = @("Assets", "Tools", "AGENTS.md"),
    [int]$First = 20,
    [string[]]$Extension = @(),
    [switch]$FilesOnly,
    [switch]$HitSummary,
    [switch]$IncludeUnityYaml,
    [switch]$AllowMany,
    [switch]$PrintOutput
)

if ($First -gt 20 -and -not $AllowMany) {
    Write-Warning "safe-search caps -First at 20 by default. Add -AllowMany when larger output is intentional."
    $First = 20
}

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

if ($HitSummary) {
    $pathArgs = ($Path | ForEach-Object { Quote-PowerShellValue $_ }) -join " "
    if ([string]::IsNullOrWhiteSpace($pathArgs)) { $pathArgs = "Assets Tools AGENTS.md" }
    $patternArg = Quote-PowerShellValue $Pattern
    $extra = $extraArgs -join " "
    $command = "`$hits = rg -n --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs; `$hits | ForEach-Object { (`$_ -split ':', 2)[0] } | Group-Object | Sort-Object Count -Descending | Select-Object -First $First Count,Name"
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

if ($FilesOnly) {
    $pathArgs = ($Path | ForEach-Object { Quote-PowerShellValue $_ }) -join " "
    if ([string]::IsNullOrWhiteSpace($pathArgs)) { $pathArgs = "Assets Tools AGENTS.md" }
    $patternArg = Quote-PowerShellValue $Pattern
    $extra = $extraArgs -join " "
    $command = "rg -l --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs | Select-Object -First $First"
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Search -Pattern $Pattern -Path $Path -First $First -ExtraArgs ($extraArgs -join " ") -PrintOutput:$PrintOutput
