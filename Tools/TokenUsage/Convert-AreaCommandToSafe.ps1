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

function Join-ArrayArgs {
    param([string[]]$Values)
    return (($Values | ForEach-Object { Quote-Arg $_ }) -join ",")
}

function Split-CommandArgs {
    param([string]$Text)
    $matches = [regex]::Matches($Text, "'([^']*)'|`"([^`"]*)`"|(\S+)")
    $items = @()
    foreach ($match in $matches) {
        if ($match.Groups[1].Success) { $items += $match.Groups[1].Value }
        elseif ($match.Groups[2].Success) { $items += $match.Groups[2].Value }
        else { $items += $match.Groups[3].Value }
    }
    return $items
}

function Test-BroadPath {
    param([string[]]$Paths)
    if ($Paths.Count -eq 0) { return $true }
    foreach ($path in $Paths) {
        $normalized = $path.Trim("'`"").Replace("\", "/").TrimEnd("/")
        if ($normalized -in @("Assets", "Assets/AreaSurvivors", "Assets/AreaSurvivors/Scripts", "Assets/AreaSurvivors/Editor", "Assets/AreaSurvivors/Scenes", "Assets/AreaSurvivors/Prefabs", ".")) {
            return $true
        }
        if ($normalized -notmatch '\.[A-Za-z0-9]+$') { return $true }
        if ($normalized -match '\.(unity|prefab|asset)$') { return $true }
    }
    return $false
}

function Get-RgSearchParts {
    param([string]$Rest)
    $args = @(Split-CommandArgs $Rest)
    $positionals = @()
    $skipNext = $false
    $optionsWithValue = @{
        "-g" = $true; "--glob" = $true; "-t" = $true; "--type" = $true; "--type-not" = $true;
        "-e" = $true; "--regexp" = $true; "-f" = $true; "--file" = $true; "-m" = $true; "--max-count" = $true;
        "-A" = $true; "-B" = $true; "-C" = $true; "--after-context" = $true; "--before-context" = $true; "--context" = $true
    }

    for ($i = 0; $i -lt $args.Count; $i++) {
        $arg = $args[$i]
        if ($skipNext) {
            $skipNext = $false
            continue
        }
        if ($arg -eq "--") { continue }
        if ($optionsWithValue.ContainsKey($arg)) {
            if ($arg -in @("-e", "--regexp") -and $i + 1 -lt $args.Count) {
                $positionals += $args[$i + 1]
            }
            $skipNext = $true
            continue
        }
        if ($arg.StartsWith("--glob=") -or $arg.StartsWith("--type=") -or $arg.StartsWith("--type-not=") -or $arg.StartsWith("--max-count=")) {
            continue
        }
        if ($arg.StartsWith("-") -and $arg -notmatch '^-\.') { continue }
        $positionals += $arg
    }

    $pattern = if ($positionals.Count -gt 0) { $positionals[0] } else { $Rest }
    $paths = if ($positionals.Count -gt 1) { @($positionals | Select-Object -Skip 1) } else { @("Assets", "Tools", "AGENTS.md") }
    [pscustomobject]@{ Pattern = $pattern; Paths = $paths }
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
        $paths = @(Split-CommandArgs $Matches[1].Trim())
        $pathArgs = Join-ArrayArgs $paths
        if (Test-BroadPath $paths) {
            $safe = New-Conversion "Path-limited git diff can still be huge for directories, scenes, prefabs, or assets; use summary first." "powershell -ExecutionPolicy Bypass -Command `"& 'Tools/TokenUsage/safe-diff.ps1' -Path @($pathArgs) -SummaryOnly$printArg`"" "high"
        }
        else {
            $safe = New-Conversion "Use file-limited diff through token guard." "powershell -ExecutionPolicy Bypass -Command `"& 'Tools/TokenUsage/safe-diff.ps1' -Path @($pathArgs)$printArg`"" "high"
        }
    }
    else {
        $safe = New-Conversion "Raw git diff can be huge; use summary first." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-diff.ps1 -SummaryOnly$printArg" "high"
    }
}
elseif ($trimmed -match '^git\s+log\b') {
    $safe = New-Conversion "Limit git log rows." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Log$printArg" "medium"
}
elseif ($trimmed -match '^(rg|rg\.exe)\s+(.+)$') {
    $parts = Get-RgSearchParts $Matches[2].Trim()
    $pathArgs = Join-ArrayArgs $parts.Paths
    $broadSearchArg = if (Test-BroadPath $parts.Paths) { " -HitSummary" } else { "" }
    $reason = if ($broadSearchArg) { "Broad ripgrep can be huge; summarize matching files first." } else { "Limit ripgrep to first results through token guard." }
    $safe = New-Conversion $reason "powershell -ExecutionPolicy Bypass -Command `"& 'Tools/TokenUsage/safe-search.ps1' -Pattern $(Quote-Arg $parts.Pattern) -Path @($pathArgs)$broadSearchArg$printArg`"" "high"
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
elseif ($trimmed -match 'run-unity-report\.ps1' -or $trimmed -match '\b(asset-references|building-prefab-visuals|hud-layout|construction-menu-layout)\b') {
    if ($trimmed -match '\b(asset-references|building-prefab-visuals|hud-layout|construction-menu-layout)\b') {
        $reportName = $Matches[1]
        $safe = New-Conversion "Use the lightweight Unity report runner." "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report $reportName" "high"
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
