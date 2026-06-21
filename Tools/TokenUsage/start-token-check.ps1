param([switch]$IncludeUnity)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\TokenUsageCommon.ps1"

$marker = [pscustomobject]@{
    timestamp = (Get-Date).ToString("o")
    kind = "token_start_marker"
    command = "start-token-check"
    shell = "powershell"
    exit_code = 0
    capture_path = ""
    warn_tokens = 0
    block_tokens = 0
    blocked = $false
    estimate = [pscustomobject]@{
        source = "start-token-check"
        bytes = 0
        chars = 0
        lines = 0
        words = 0
        estimated_tokens = 0
        risk = "low"
    }
    advice = "Marker for token-report-summary -SinceLastStart."
}
Write-TokenUsageJsonLine -Record $marker | Out-Null

Write-Output "[start-token-check] status"
& "$PSScriptRoot\safe-status.ps1"
Write-Output ""
Write-Output "[start-token-check] token health"
& "$PSScriptRoot\token-health.ps1" -IncludeUnity:$IncludeUnity
