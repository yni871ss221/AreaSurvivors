Set-StrictMode -Version 2.0

function Get-TokenUsageEstimate {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text,
        [string]$Source = ""
    )

    $chars = $Text.Length
    $bytes = [System.Text.Encoding]::UTF8.GetByteCount($Text)
    $lines = if ($chars -eq 0) { 0 } else { ($Text -split "`r?`n").Count }
    $words = if ([string]::IsNullOrWhiteSpace($Text)) { 0 } else { ([regex]::Matches($Text, "\S+")).Count }

    # Conservative heuristic for mixed English/Japanese/code/log output.
    $charEstimate = [math]::Ceiling($chars / 3.2)
    $wordEstimate = [math]::Ceiling($words * 1.35)
    $estimatedTokens = [int][math]::Max($charEstimate, $wordEstimate)

    [pscustomobject]@{
        source = $Source
        bytes = $bytes
        chars = $chars
        lines = $lines
        words = $words
        estimated_tokens = $estimatedTokens
        risk = Get-TokenUsageRisk -Source $Source -Text $Text -EstimatedTokens $estimatedTokens
    }
}

function Get-TokenUsageRisk {
    param(
        [string]$Source,
        [string]$Text,
        [int]$EstimatedTokens
    )

    $risk = "low"
    if ($EstimatedTokens -ge 3000) { $risk = "medium" }
    if ($EstimatedTokens -ge 8000) { $risk = "high" }
    if ($EstimatedTokens -ge 20000) { $risk = "critical" }

    $dangerPatterns = @(
        "\\Library\\",
        "/Library/",
        "\\Temp\\",
        "/Temp/",
        "\\.git\\",
        "/.git/",
        "\.dll$",
        "\.pdb$",
        "\.png$",
        "\.jpg$",
        "\.jpeg$",
        "\.asset$",
        "\.unity$",
        "\.prefab$"
    )

    foreach ($pattern in $dangerPatterns) {
        if ($Source -match $pattern) {
            if ($risk -eq "low") { $risk = "medium" }
            break
        }
    }

    if ($EstimatedTokens -ge 3000 -and $Text -match "Fast-forward|files? changed|create mode|delete mode|rename " -and $Text -match "Assets/AreaSurvivors") {
        if ($risk -eq "low" -or $risk -eq "medium") { $risk = "high" }
    }

    return $risk
}

function Get-TokenUsageAdvice {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Estimate
    )

    if ($Estimate.risk -eq "critical") {
        return "Do not paste this output into chat. Use RTK, --name-only/--stat, a reporter, or a targeted validator."
    }
    if ($Estimate.risk -eq "high") {
        return "Avoid raw output. Prefer RTK, --no-stat, --name-only, --stat, Select-Object -First, or a compact reporter."
    }
    if ($Estimate.risk -eq "medium") {
        return "Review before pasting. Consider truncating or summarizing first."
    }
    return "Likely safe to inspect directly."
}

function New-TokenUsageReportPath {
    param(
        [string]$Root = ""
    )

    if ([string]::IsNullOrWhiteSpace($Root)) {
        $Root = Join-Path (Get-Location) "TokenReports"
    }

    if (-not (Test-Path -LiteralPath $Root)) {
        New-Item -ItemType Directory -Force -Path $Root | Out-Null
    }

    return Join-Path $Root ((Get-Date).ToString("yyyy-MM-dd") + ".jsonl")
}

function Write-TokenUsageJsonLine {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Record,
        [string]$ReportPath = ""
    )

    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        $ReportPath = New-TokenUsageReportPath
    }

    $areaToolOperation = [Environment]::GetEnvironmentVariable(
        "AREA_TOOL_OPERATION",
        [EnvironmentVariableTarget]::Process
    )
    if (-not [string]::IsNullOrWhiteSpace($areaToolOperation) -and
        $null -eq $Record.PSObject.Properties["area_tool_operation"]) {
        $Record | Add-Member -NotePropertyName "area_tool_operation" -NotePropertyValue $areaToolOperation
    }

    $resolvedReportPath = [System.IO.Path]::GetFullPath($ReportPath)
    $pathBytes = [System.Text.Encoding]::UTF8.GetBytes(
        $resolvedReportPath.ToLowerInvariant()
    )
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $pathHash = [System.BitConverter]::ToString(
            $sha256.ComputeHash($pathBytes)
        ).Replace("-", "").Substring(0, 24)
    } finally {
        $sha256.Dispose()
    }

    $line = $Record | ConvertTo-Json -Depth 8 -Compress
    $mutex = New-Object System.Threading.Mutex(
        $false,
        "AreaSurvivors.TokenReportWriter.$pathHash"
    )
    $lockTaken = $false
    try {
        try {
            $lockTaken = $mutex.WaitOne([TimeSpan]::FromSeconds(10))
        } catch [System.Threading.AbandonedMutexException] {
            $lockTaken = $true
        }
        if (-not $lockTaken) {
            throw "Timed out waiting for the TokenReports writer lock."
        }
        [System.IO.File]::AppendAllText(
            $resolvedReportPath,
            $line + [Environment]::NewLine,
            [System.Text.UTF8Encoding]::new($false)
        )
    } finally {
        if ($lockTaken) {
            $mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
    return $resolvedReportPath
}
