function safe-status {
    param([string[]]$Path = @(), [switch]$PrintOutput)
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Status -Path $Path -PrintOutput:$PrintOutput
}

function safe-diff {
    param([string[]]$Path = @(), [string]$RefRange = "", [switch]$Stat, [switch]$NameOnly, [switch]$PrintOutput)
    $action = if ($NameOnly) { "DiffNameOnly" } elseif ($Stat) { "DiffStat" } else { "Diff" }
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action $action -Path $Path -RefRange $RefRange -PrintOutput:$PrintOutput
}

function safe-search {
    param([Parameter(Mandatory = $true)][string]$Pattern, [string[]]$Path = @("Assets", "Tools", "AGENTS.md"), [int]$First = 120, [switch]$PrintOutput)
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Search -Pattern $Pattern -Path $Path -First $First -PrintOutput:$PrintOutput
}

function safe-read {
    param([Parameter(Mandatory = $true)][string]$Path, [int]$First = 120, [switch]$PrintOutput)
    & "$PSScriptRoot\Invoke-AreaSafeCommand.ps1" -Action Read -Path $Path -First $First -PrintOutput:$PrintOutput
}

function token-health {
    param([switch]$FailOnIncrease, [switch]$IncludeUnity, [switch]$UpdateBaseline)
    & "$PSScriptRoot\Invoke-AreaTokenHealth.ps1" -FailOnIncrease:$FailOnIncrease -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline
}

function token-benchmark-heavy {
    param([switch]$IncludeRtk, [switch]$IncludeUnity, [switch]$UpdateBaseline)
    & "$PSScriptRoot\token-benchmark-heavy.ps1" -IncludeRtk:$IncludeRtk -IncludeUnity:$IncludeUnity -UpdateBaseline:$UpdateBaseline
}

function guarded-command {
    param([Parameter(Mandatory = $true)][string]$Command, [switch]$PrintOutput, [switch]$DryRun, [switch]$ExecuteOriginalIfSafe)
    & "$PSScriptRoot\Invoke-AreaGuardedCommand.ps1" -Command $Command -PrintOutput:$PrintOutput -DryRun:$DryRun -ExecuteOriginalIfSafe:$ExecuteOriginalIfSafe
}

function safe-unity {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Compile", "ConsoleErrors", "Menu", "Eval")]
        [string]$Action,
        [string]$MenuPath = "",
        [string]$EvalCode = "",
        [int]$MaxCount = 30,
        [switch]$PrintOutput
    )
    & "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action $Action -MenuPath $MenuPath -EvalCode $EvalCode -MaxCount $MaxCount -PrintOutput:$PrintOutput
}

function safe-unity-search {
    param([Parameter(Mandatory = $true)][string]$Query, [switch]$PrintOutput)
    & "$PSScriptRoot\Invoke-AreaUnitySearch.ps1" -Query $Query -PrintOutput:$PrintOutput
}

function token-report-summary {
    param([int]$Days = 7, [int]$Top = 10, [switch]$IncludeBenchmark)
    & "$PSScriptRoot\Get-TokenReportSummary.ps1" -Days $Days -Top $Top -IncludeBenchmark:$IncludeBenchmark
}

function start-token-check {
    param([switch]$IncludeUnity)
    & "$PSScriptRoot\start-token-check.ps1" -IncludeUnity:$IncludeUnity
}

function end-token-check {
    param([switch]$IncludeUnity)
    & "$PSScriptRoot\end-token-check.ps1" -IncludeUnity:$IncludeUnity
}
