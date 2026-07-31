param(
    [string]$ProjectRoot = "",
    [ValidateRange(30, 120)][int]$MaxAgentLines = 70,
    [ValidateRange(20, 200)][int]$MaxProjectLines = 60
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}

function Assert-CompactCurrentFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$MaxLines,
        [Parameter(Mandatory = $true)][string[]]$RequiredHeadings,
        [string[]]$RequiredText = @(),
        [switch]$RejectDatedSections
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Current context file not found: $Path"
    }

    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($lines.Count -gt $MaxLines) {
        throw "Current context exceeds line budget: path=$Path lines=$($lines.Count) max=$MaxLines"
    }

    foreach ($heading in $RequiredHeadings) {
        if (-not ($lines -contains $heading)) {
            throw "Required current-context heading is missing: path=$Path heading=$heading"
        }
    }

    $text = [string]::Join([Environment]::NewLine, $lines)
    foreach ($requiredTextEntry in $RequiredText) {
        if (-not [string]::IsNullOrWhiteSpace($requiredTextEntry) -and
            $text.IndexOf($requiredTextEntry, [StringComparison]::Ordinal) -lt 0) {
            throw "Required current-context reference is missing: path=$Path text=$requiredTextEntry"
        }
    }

    if ($RejectDatedSections) {
        $datedSections = @($lines | Where-Object { $_ -match '^## 20\d{2}-\d{2}-\d{2}' })
        if ($datedSections.Count -gt 0) {
            throw "Dated history must not remain in current note: path=$Path count=$($datedSections.Count)"
        }
    }

    return [pscustomobject]@{
        path = $Path
        lines = $lines.Count
        max_lines = $MaxLines
        status = "ok"
    }
}

$agentsPath = Join-Path $ProjectRoot "AGENTS.md"
$projectCurrentPath = Join-Path $ProjectRoot "ctx/current.md"
$results = @(
    Assert-CompactCurrentFile `
        -Path $agentsPath `
        -MaxLines $MaxAgentLines `
        -RequiredHeadings @(
            "# AGENTS.md",
            "## Always",
            "## Task Routing",
            "## Failure Boundary",
            "## Project Facts",
            "## Maintenance"
        ) `
        -RequiredText @(
            "Docs/AgentRules/command-failure-playbook.md",
            "Docs/AgentRules/token-tools.md",
            "PowerShell"
        )

    Assert-CompactCurrentFile `
        -Path $projectCurrentPath `
        -MaxLines $MaxProjectLines `
        -RejectDatedSections `
        -RequiredHeadings @(
            "# Current Task",
            "## Goal",
            "## Latest Decision",
            "## Latest Verification",
            "## TODO",
            "## Blocker"
        ) `
        -RequiredText "Git"
)

[pscustomobject]@{
    status = "ok"
    checked = $results
} | ConvertTo-Json -Depth 4
