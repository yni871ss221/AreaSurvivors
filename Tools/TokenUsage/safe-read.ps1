param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$First = 80,
    [int]$Last = 0,
    [int]$StartLine = 0,
    [int]$EndLine = 0,
    [string]$Pattern = "",
    [int]$Context = 20,
    [int]$MaxMatches = 5,
    [switch]$LiteralPattern,
    [switch]$AllowMany,
    [switch]$AllowHighOutput,
    [switch]$PrintOutput
)

$maxInteractiveOutputLines = 80

if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "safe-read path must be an existing file before reading (guard_code: 33): $Path"
}

if (-not $LiteralPattern -and -not [string]::IsNullOrWhiteSpace($Pattern) -and $Pattern -match '(?<!\\)(\+\+|\*\*|\?\?)') {
    throw "safe-read -Pattern is a regular expression. Code literals containing ++, **, or ?? require -LiteralPattern (guard_code: 36): $Pattern"
}

if (-not $LiteralPattern -and -not [string]::IsNullOrWhiteSpace($Pattern)) {
    try {
        [void][regex]::new($Pattern)
    } catch {
        throw "safe-read -Pattern is not a valid regular expression. Use -LiteralPattern for code text containing unmatched regex punctuation (guard_code: 37): $Pattern"
    }
}

if ($Last -gt 0 -and (-not [string]::IsNullOrWhiteSpace($Pattern) -or $StartLine -gt 0 -or $EndLine -gt 0)) {
    throw "safe-read -Last cannot be combined with -Pattern, -StartLine, or -EndLine (guard_code: 42)."
}

if ($PrintOutput -and -not $AllowHighOutput) {
    $estimatedPrintedLines = if ($Last -gt 0) {
        $Last
    } elseif (-not [string]::IsNullOrWhiteSpace($Pattern)) {
        $MaxMatches * (($Context * 2) + 4)
    } elseif ($StartLine -gt 0 -or $EndLine -gt 0) {
        $startForEstimate = [Math]::Max(1, $StartLine)
        $endForEstimate = if ($EndLine -gt 0) { [Math]::Max($startForEstimate, $EndLine) } else { $startForEstimate + $First - 1 }
        $endForEstimate - $startForEstimate + 1
    } else {
        $First
    }

    if ($estimatedPrintedLines -gt $maxInteractiveOutputLines) {
        $suggestion = ""
        if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
            $linesPerMatch = ($Context * 2) + 4
            $suggestedMaxMatches = [Math]::Max(1, [Math]::Floor($maxInteractiveOutputLines / $linesPerMatch))
            $suggestion = " suggested_max_matches=$suggestedMaxMatches"
        } elseif ($StartLine -gt 0 -or $EndLine -gt 0) {
            $suggestedStartLine = [Math]::Max(1, $StartLine)
            $suggestedEndLine = $suggestedStartLine + $maxInteractiveOutputLines - 1
            $suggestion = " suggested_end_line=$suggestedEndLine use_safe_read_batch=1"
        } elseif ($StartLine -le 0 -and $EndLine -le 0 -and $Last -le 0) {
            $suggestion = " suggested_first=$maxInteractiveOutputLines"
        }
        throw "safe-read refuses more than $maxInteractiveOutputLines estimated output lines with -PrintOutput (guard_code: 39). Narrow the range/matches/context, run reads sequentially, or add -AllowHighOutput only when the single-call output budget has been reserved. estimated_lines=$estimatedPrintedLines$suggestion"
    }
}

if ($First -gt 80 -and -not $AllowMany) {
    Write-Warning "safe-read caps -First at 80 by default. Add -AllowMany when larger output is intentional."
    $First = 80
}
if ($Last -gt 80 -and -not $AllowMany) {
    Write-Warning "safe-read caps -Last at 80 by default. Add -AllowMany when larger output is intentional."
    $Last = 80
}
if ($Context -gt 40 -and -not $AllowMany) {
    Write-Warning "safe-read caps -Context at 40 by default. Add -AllowMany when larger context is intentional."
    $Context = 40
}
if ($MaxMatches -gt 10 -and -not $AllowMany) {
    Write-Warning "safe-read caps -MaxMatches at 10 by default. Add -AllowMany when more matches are intentional."
    $MaxMatches = 10
}

function Quote-PowerShellValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$fileArg = Quote-PowerShellValue $Path

if ($Last -gt 0) {
    $command = "Get-Content -LiteralPath $fileArg -Encoding UTF8 -Tail $Last"
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
    $effectivePattern = if ($LiteralPattern) { [regex]::Escape($Pattern) } else { $Pattern }
    $patternArg = Quote-PowerShellValue $effectivePattern
    $command = @"
`$lines = Get-Content -LiteralPath $fileArg -Encoding UTF8
`$pattern = $patternArg
`$matchLines = @()
for (`$lineIndex = 0; `$lineIndex -lt `$lines.Count; `$lineIndex++) {
    if (`$lines[`$lineIndex] -match `$pattern) {
        `$matchLines += (`$lineIndex + 1)
        if (`$matchLines.Count -ge $MaxMatches) { break }
    }
}
foreach (`$lineNumber in `$matchLines) {
    `$start = [Math]::Max(1, `$lineNumber - $Context)
    `$end = [Math]::Min(`$lines.Count, `$lineNumber + $Context)
    Write-Output ('match at line {0}' -f `$lineNumber)
    for (`$i = `$start; `$i -le `$end; `$i++) {
        `$prefix = if (`$i -eq `$lineNumber) { [char]62 } else { [char]32 }
        Write-Output ('{0}{1,5}: {2}' -f `$prefix, `$i, `$lines[`$i - 1])
    }
    Write-Output ''
}
"@
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

if ($StartLine -gt 0 -or $EndLine -gt 0) {
    $start = [Math]::Max(1, $StartLine)
    $end = if ($EndLine -gt 0) { [Math]::Max($start, $EndLine) } else { $start + $First - 1 }
    $count = $end - $start + 1
    if ($count -gt 80 -and -not $AllowMany) {
        Write-Warning "safe-read caps line ranges at 80 lines by default. Add -AllowMany when a larger range is intentional."
        $count = 80
    }
    $skip = $start - 1
    $command = "Get-Content -LiteralPath $fileArg -Encoding UTF8 | Select-Object -Skip $skip -First $count"
    $argsForSafe = @{ Command = $command }
    if ($PrintOutput) { $argsForSafe.PrintOutput = $true }
    & "$PSScriptRoot\Safe-Command.ps1" @argsForSafe
    exit $LASTEXITCODE
}

& "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Read -Path $Path -First $First -PrintOutput:$PrintOutput
