param(
    [Parameter(Mandatory = $true)]
    [string]$Command,
    [switch]$Json
)

$rules = @(
    [pscustomobject]@{ risk = "critical"; pattern = '(^|\s)git\s+diff(\s|$)(?!.*(--stat|--name-only|--check|--\s+\S))'; advice = "Use safe-diff -Stat, safe-diff -NameOnly, or safe-diff -Path <path>." },
    [pscustomobject]@{ risk = "high"; pattern = '(^|\s)rg(\.exe)?\s+["''][^"'']+["'']\s*$|(^|\s)rg(\.exe)?\s+\S+\s*$'; advice = "Use safe-search with a target path and -First limit." },
    [pscustomobject]@{ risk = "high"; pattern = 'Get-Content(?!.*(-TotalCount|-Tail|-First))'; advice = "Use safe-read or add -TotalCount." },
    [pscustomobject]@{ risk = "high"; pattern = 'Console\.GetLog(?!.*--maxCount)'; advice = "Use safe command ConsoleErrors or add --maxCount." },
    [pscustomobject]@{ risk = "critical"; pattern = '(Assets/AreaSurvivors/Scenes/.*\.unity|\.prefab)(?!.*(Select-Object|-TotalCount|--stat|--name-only))'; advice = "Use Scene/Prefab Structure Reporter or targeted validator before raw YAML." }
)

$riskMatches = @()
foreach ($rule in $rules) {
    if ($Command -match $rule.pattern) {
        $riskMatches += [pscustomobject]@{
            risk = $rule.risk
            pattern = $rule.pattern
            advice = $rule.advice
        }
    }
}

$result = [pscustomobject]@{
    command = $Command
    risky = $riskMatches.Count -gt 0
    matches = $riskMatches
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
} elseif ($result.risky) {
    Write-Output "Risky command detected:"
    $riskMatches | Select-Object risk, advice | Format-Table -AutoSize
    exit 2
} else {
    Write-Output "No high-output risk pattern detected."
}
