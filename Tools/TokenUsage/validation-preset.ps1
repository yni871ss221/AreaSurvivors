param(
    [ValidateSet("tool-only", "csharp-only", "scene-ui", "asset-import", "combat")]
    [string]$Preset = "tool-only",
    [switch]$Run,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

$commands = switch ($Preset) {
    "tool-only" {
        @(
            "powershell -NoProfile -Command `" `$files = Get-ChildItem Tools/TokenUsage -Filter '*.ps1' -File; foreach (`$f in `$files) { `$null = [scriptblock]::Create((Get-Content -LiteralPath `$f.FullName -Raw)) }; 'syntax ok' `"",
            "git diff --check -- Tools/TokenUsage AGENTS.md Docs/AgentRules"
        )
    }
    "csharp-only" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action Compile",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30",
            "git diff --check -- Assets/AreaSurvivors/Scripts Assets/AreaSurvivors/Editor"
        )
    }
    "scene-ui" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity-search.ps1 -Query HUD",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action Compile",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30"
        )
    }
    "asset-import" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/project-weight-report.ps1 -Top 10",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action Compile",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30"
        )
    }
    "combat" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action Compile",
            "powershell -NoProfile -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30"
        )
    }
}

Write-Output ("Validation preset: {0}" -f $Preset)
foreach ($command in $commands) { Write-Output ("- {0}" -f $command) }

if (-not $Run) {
    Write-Output "Dry run only. Add -Run to execute."
    exit 0
}

foreach ($command in $commands) {
    Write-Output ""
    Write-Output ("[validation-preset] {0}" -f $command)
    & "$PSScriptRoot\guarded-command.ps1" -Command $command -PrintOutput:$PrintOutput -ExecuteOriginalIfSafe
}
