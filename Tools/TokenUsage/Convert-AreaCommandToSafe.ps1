param(
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [switch]$PrintOutput,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Quote-Arg {
    param([AllowEmptyString()][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function New-Conversion {
    param(
        [string]$Reason,
        [string]$SafeCommand,
        [string]$Confidence = "medium"
    )

    [pscustomobject]@{
        original_command = $Command
        converted = -not [string]::IsNullOrWhiteSpace($SafeCommand)
        safe_command = $SafeCommand
        confidence = $Confidence
        reason = $Reason
    }
}

$trimmed = $Command.Trim()
$safe = $null
$printArg = if ($PrintOutput) { " -PrintOutput" } else { "" }

if ($trimmed -match '^git\s+status\b') {
    $safe = New-Conversion "Use compact status output." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-status.ps1$printArg" "high"
}
elseif ($trimmed -match '^git\s+diff\b') {
    if ($trimmed -match '--name-only') {
        $safe = New-Conversion "Use name-only diff through token guard." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-diff.ps1 -NameOnly$printArg" "high"
    }
    elseif ($trimmed -match '--stat') {
        $safe = New-Conversion "Use stat diff through token guard." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-diff.ps1 -Stat$printArg" "high"
    }
    elseif ($trimmed -match '--\s+(.+)$') {
        $pathText = $Matches[1].Trim()
        $safe = New-Conversion "Use path-limited diff through token guard." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Diff -Path $(Quote-Arg $pathText)$printArg" "medium"
    }
    else {
        $safe = New-Conversion "Raw git diff can be huge; use stat first." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-diff.ps1 -Stat$printArg" "high"
    }
}
elseif ($trimmed -match '^git\s+log\b') {
    $safe = New-Conversion "Limit git log rows." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Log$printArg" "medium"
}
elseif ($trimmed -match '^(rg|rg\.exe)\s+(.+)$') {
    $rest = $Matches[2].Trim()
    $pattern = $rest
    $paths = "Assets", "Tools", "AGENTS.md"

    if ($rest -match '^"([^"]+)"\s+(.+)$' -or $rest -match "^'([^']+)'\s+(.+)$") {
        $pattern = $Matches[1]
        $paths = @($Matches[2].Trim() -split '\s+') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }
    elseif ($rest -match '^(\S+)\s+(.+)$') {
        $pattern = $Matches[1]
        $paths = @($Matches[2].Trim() -split '\s+') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $pathArgs = ($paths | ForEach-Object { Quote-Arg $_ }) -join ","
    $safe = New-Conversion "Limit ripgrep to target paths and first results." "powershell -ExecutionPolicy Bypass -Command `"& 'Tools/TokenUsage/safe-search.ps1' -Pattern $(Quote-Arg $pattern) -Path @($pathArgs)$printArg`"" "medium"
}
elseif ($trimmed -match '^Get-Content\b' -and $trimmed -notmatch '(-TotalCount|-Tail|-First)') {
    $path = ""
    if ($trimmed -match "-LiteralPath\s+('([^']+)'|`"([^`"]+)`"|(\S+))") {
        $path = @($Matches[2], $Matches[3], $Matches[4] | Where-Object { $_ })[0]
    }
    elseif ($trimmed -match '^Get-Content\s+(''([^'']+)''|"([^"]+)"|(\S+))') {
        $path = @($Matches[2], $Matches[3], $Matches[4] | Where-Object { $_ })[0]
    }

    if (-not [string]::IsNullOrWhiteSpace($path)) {
        $safe = New-Conversion "Limit file read to the first lines." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-read.ps1 -Path $(Quote-Arg $path) -First 120$printArg" "high"
    }
}
elseif ($trimmed -match 'Console\.GetLog' -and $trimmed -notmatch '--maxCount') {
    $safe = New-Conversion "Limit Unity console log count." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30$printArg" "high"
}
elseif ($trimmed -match '^unicli\s+exec\s+Compile\b') {
    $safe = New-Conversion "Run Unity compile through token guard." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action Compile$printArg" "high"
}
elseif ($trimmed -match '^unicli\s+exec\s+Menu\.Execute\b') {
    if ($trimmed -match "--menu(Item)?Path\s+('([^']+)'|`"([^`"]+)`"|(.+))") {
        $menuPath = @($Matches[3], $Matches[4], $Matches[5] | Where-Object { $_ })[0].Trim()
        $safe = New-Conversion "Run Unity menu through token guard." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action Menu -MenuPath $(Quote-Arg $menuPath)$printArg" "medium"
    }
}

if ($null -eq $safe) {
    $safe = New-Conversion "No automatic safe conversion rule matched." "" "none"
}

if ($Json) {
    $safe | ConvertTo-Json -Depth 5
} else {
    $safe
}
