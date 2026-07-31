param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Status", "Diff", "DiffStat", "DiffNameOnly", "Log", "Search", "Read", "Compile", "ConsoleErrors", "Benchmark")]
    [string]$Action,
    [string[]]$Path = @(),
    [string]$Pattern = "",
    [string]$RefRange = "",
    [string]$ExtraArgs = "",
    [int]$First = 120,
    [int]$WarnTokens = 3000,
    [int]$BlockTokens = 8000,
    [switch]$PrintOutput,
    [switch]$AllowHighOutput
)

$ErrorActionPreference = "Stop"

function Quote-PowerShellValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Join-QuotedPaths {
    param([string[]]$Values)
    if ($Values.Count -eq 0) { return "" }
    return ($Values | ForEach-Object { Quote-PowerShellValue $_ }) -join " "
}

function Normalize-PathValues {
    param([string[]]$Values)
    $result = @()
    foreach ($value in $Values) {
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        foreach ($part in ($value -split ",")) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) { $result += $trimmed }
        }
    }
    return $result
}

$safeCommandPath = Join-Path $PSScriptRoot "Safe-Command.ps1"
$benchmarkPath = Join-Path $PSScriptRoot "Run-TokenBenchmark.ps1"
$command = ""
$Path = @(Normalize-PathValues $Path)

switch ($Action) {
    "Status" {
        $pathArgs = Join-QuotedPaths $Path
        $command = if ([string]::IsNullOrWhiteSpace($pathArgs)) {
            "git status --short --branch"
        } else {
            "git status --short --branch -- $pathArgs"
        }
    }
    "Diff" {
        if ($Path.Count -eq 0 -and [string]::IsNullOrWhiteSpace($RefRange)) {
            throw "Diff requires -Path or -RefRange. Use DiffStat/DiffNameOnly first for broad checks."
        }
        $pathArgs = Join-QuotedPaths $Path
        $rangeArgs = if ([string]::IsNullOrWhiteSpace($RefRange)) { "" } else { $RefRange }
        $gitDiffCommand = "git diff $rangeArgs $ExtraArgs"
        if (-not [string]::IsNullOrWhiteSpace($pathArgs)) {
            $gitDiffCommand = "$gitDiffCommand -- $pathArgs"
        }
        $command = "`$diffOutput = @(& { $gitDiffCommand } 2>&1); " +
            "`$gitExit = `$LASTEXITCODE; " +
            "`$diffOutput | Select-Object -First $First; " +
            "if (`$diffOutput.Count -gt $First) { " +
            "Write-Output ('[diff output truncated: shown=$First total={0}]' -f `$diffOutput.Count) }; " +
            "exit `$gitExit"
    }
    "DiffStat" {
        $pathArgs = Join-QuotedPaths $Path
        $rangeArgs = if ([string]::IsNullOrWhiteSpace($RefRange)) { "" } else { $RefRange }
        $command = "git diff --stat $rangeArgs $ExtraArgs"
        if (-not [string]::IsNullOrWhiteSpace($pathArgs)) { $command = "$command -- $pathArgs" }
    }
    "DiffNameOnly" {
        $pathArgs = Join-QuotedPaths $Path
        $rangeArgs = if ([string]::IsNullOrWhiteSpace($RefRange)) { "" } else { $RefRange }
        $command = "git diff --name-only $rangeArgs $ExtraArgs"
        if (-not [string]::IsNullOrWhiteSpace($pathArgs)) { $command = "$command -- $pathArgs" }
    }
    "Log" {
        $pathArgs = Join-QuotedPaths $Path
        $limitArgs = if ($ExtraArgs -match "--max-count|-n\s+\d+") { $ExtraArgs } else { "--max-count=20 $ExtraArgs" }
        $command = "git log --oneline $limitArgs"
        if (-not [string]::IsNullOrWhiteSpace($pathArgs)) { $command = "$command -- $pathArgs" }
    }
    "Search" {
        if ([string]::IsNullOrWhiteSpace($Pattern)) { throw "Search requires -Pattern." }
        $pathArgs = Join-QuotedPaths $Path
        if ([string]::IsNullOrWhiteSpace($pathArgs)) { $pathArgs = "Assets Tools AGENTS.md" }
        $patternArg = Quote-PowerShellValue $Pattern
        $command = "`$hits = @(rg -n --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' $ExtraArgs $patternArg $pathArgs 2>&1); `$rgExit = `$LASTEXITCODE; if (`$rgExit -gt 1) { `$hits | ForEach-Object { [Console]::Error.WriteLine(`$_) }; exit `$rgExit }; `$hits | Select-Object -First $First; exit 0"
    }
    "Read" {
        if ($Path.Count -ne 1) { throw "Read requires exactly one -Path." }
        $fileArg = Quote-PowerShellValue $Path[0]
        $command = "Get-Content -LiteralPath $fileArg -Encoding UTF8 -TotalCount $First"
    }
    "Compile" {
        $command = "unicli exec Compile"
    }
    "ConsoleErrors" {
        $command = "unicli exec Console.GetLog --logType Error --maxCount $First"
    }
    "Benchmark" {
        $benchmarkArgs = @()
        if (-not [string]::IsNullOrWhiteSpace($RefRange)) {
            $parts = $RefRange -split "\.\.", 2
            if ($parts.Count -eq 2) {
                $benchmarkArgs += "-BaseRef"
                $benchmarkArgs += $parts[0]
                $benchmarkArgs += "-HeadRef"
                $benchmarkArgs += $parts[1]
            }
        }
        & $benchmarkPath @benchmarkArgs
        exit $LASTEXITCODE
    }
}

$argsForSafe = @{
    Command = $command
    WarnTokens = $WarnTokens
    BlockTokens = $BlockTokens
}
if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
if ($AllowHighOutput) { $argsForSafe.AllowHighOutput = $true }

& $safeCommandPath @argsForSafe
