param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$First = 80,
    [int]$StartLine = 0,
    [int]$EndLine = 0,
    [string]$Pattern = "",
    [int]$Context = 20,
    [int]$MaxMatches = 5,
    [switch]$AllowMany,
    [switch]$PrintOutput
)

if ($First -gt 80 -and -not $AllowMany) {
    Write-Warning "safe-read caps -First at 80 by default. Add -AllowMany when larger output is intentional."
    $First = 80
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

if (-not [string]::IsNullOrWhiteSpace($Pattern)) {
    $patternArg = Quote-PowerShellValue $Pattern
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
