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
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Test.Commands",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Git.Check -Path 'Tools/TokenUsage;AGENTS.md;Docs/AgentRules'"
        )
    }
    "csharp-only" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Git.Check -Path 'Assets/AreaSurvivors/Scripts;Assets/AreaSurvivors/Editor'"
        )
    }
    "scene-ui" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Search -Pattern HUD",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30"
        )
    }
    "asset-import" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Project.Weight -MaxResults 10",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30"
        )
    }
    "combat" {
        @(
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Compile",
            "powershell -NoProfile -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30"
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
    & "$PSScriptRoot\area-tool.ps1" `
        -Operation Command.Guard `
        -CommandText $command `
        -PrintOutput:$PrintOutput `
        -ExecuteOriginalIfSafe
}
