<#
.SYNOPSIS
Runs a bounded repository text search.

.DESCRIPTION
Formal usage: safe-search.ps1 -Pattern <regex> [-Path <existing path>] [-First N]
[-FilesOnly|-HitSummary] [-Extension <name>] [-IncludeUnityYaml] [-PrintOutput].
Use -Path for the search root. This wrapper does not accept -Root.
#>
param(
    [Parameter(Mandatory = $true)][Alias("Query")][string]$Pattern,
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

foreach ($item in $Path) {
    if ([string]::IsNullOrWhiteSpace($item) -or -not (Test-Path -LiteralPath $item)) {
        throw "Each -Path item must exist. For powershell -File calls, use one path per invocation instead of a comma-joined value: $item"
    }
    $resolvedItem = (Resolve-Path -LiteralPath $item).Path.TrimEnd([char]'/', [char]'\')
    if ([System.IO.Path]::GetFileName($resolvedItem) -eq ".codex") {
        throw "safe-search refuses the broad .codex root because it contains restricted runtime subtrees. Specify a known readable subdirectory such as .codex/skills. (guard_code: 44)"
    }
}

foreach ($ext in $Extension) {
    if ($ext -match '[:\\/]') {
        throw "safe-search -Extension accepts extension names only. A second -Path value was likely positionally bound; use one -Path per powershell -File invocation: $ext"
    }
}

if ($Pattern -match '^\*.*\*$') {
    throw "safe-search -Pattern is a regular expression, not a file glob. Use 'Evolution' or '.*Evolution.*' instead of '*Evolution*'."
}

try {
    [void][regex]::new($Pattern)
} catch [System.ArgumentException] {
    throw "safe-search -Pattern is not a valid regular expression. Escape regex metacharacters such as '(' when searching them literally. pattern=$Pattern (guard_code: 43)"
}

function Quote-PowerShellValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$extraArgs = @("-g '!.sandbox-secrets/**'")
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
    $command = "`$hits = @(rg -n --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs 2>&1); `$rgExit = `$LASTEXITCODE; if (`$rgExit -gt 1) { `$hits | ForEach-Object { [Console]::Error.WriteLine(`$_) }; exit `$rgExit }; `$hits | ForEach-Object { (`$_ -replace ':\d+:.*$', '') } | Group-Object | Sort-Object Count -Descending | Select-Object -First $First | ForEach-Object { Write-Output (('{0}`t{1}' -f `$_.Count, `$_.Name)) }; exit 0"
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
    $command = "`$items = @(rg -l --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $extra $patternArg $pathArgs 2>&1); `$rgExit = `$LASTEXITCODE; if (`$rgExit -gt 1) { `$items | ForEach-Object { [Console]::Error.WriteLine(`$_) }; exit `$rgExit }; `$items | Select-Object -First $First; exit 0"
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Search -Pattern $Pattern -Path $Path -First $First -ExtraArgs ($extraArgs -join " ") -PrintOutput:$PrintOutput
