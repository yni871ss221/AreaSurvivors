# Token Usage Tools

PowerShell tools for estimating and recording command/file output token cost without pasting large raw output into the chat.

## Estimate Before Reading

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Estimate-TokenCost.ps1 -File AGENTS.md
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Estimate-TokenCost.ps1 -Command "git diff --stat" -SaveReport
```

`-Command` captures output to a temp file and reports only size/risk by default.
Use it for read-only commands. It still executes the command; it just avoids pasting the captured output into chat.

## Run With Report

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-WithTokenReport.ps1 -Command "git status --short --branch"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Safe-Command.ps1 -Command "git diff --stat"
```

Reports are written to `TokenReports/YYYY-MM-DD.jsonl`.

Use `-PrintOutput` only when the estimate is small enough to inspect.

## AreaSurvivors Safe Shortcuts

Use `Invoke-AreaSafeCommand.ps1` as the default entrypoint for commands that often grow large.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Status -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action DiffNameOnly -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action DiffStat -Path AGENTS.md -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Search -Pattern "BuildMode" -Path Assets/AreaSurvivors/Scripts -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaSafeCommand.ps1 -Action Read -Path AGENTS.md -First 80 -PrintOutput
```

Short wrappers are available for daily use:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-status.ps1 -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-diff.ps1 -Stat -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-search.ps1 -Pattern "BuildMode" -Path Assets/AreaSurvivors/Scripts -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-read.ps1 -Path AGENTS.md -First 80 -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-health.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action Compile
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity.ps1 -Action ConsoleErrors -MaxCount 30 -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/guarded-command.ps1 -Command "git diff"
```

For interactive PowerShell sessions, import aliases once:

```powershell
. Tools/TokenUsage/Import-AreaTokenAliases.ps1
safe-status -PrintOutput
safe-diff -Stat -PrintOutput
safe-search "BuildMode" -Path Assets/AreaSurvivors/Scripts -PrintOutput
safe-read AGENTS.md -First 80 -PrintOutput
token-health
safe-unity Compile
guarded-command "git diff"
```

`Diff` requires `-Path` or `-RefRange` so broad raw diffs are not run accidentally.
Check risky raw commands before running them:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Test-AreaCommandRisk.ps1 -Command "git diff"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/guarded-command.ps1 -Command "git diff" -PrintOutput
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Convert-AreaCommandToSafe.ps1 -Command "Get-Content AGENTS.md"
```

`guarded-command.ps1` automatically converts known risky commands to safe wrappers.
For example, raw `git diff` becomes `safe-diff.ps1 -Stat`, and unrestricted `Get-Content` becomes `safe-read.ps1 -First 120`.

For Scene/Prefab/C# exploration, prefer Unity reports before raw YAML or broad grep:

- `Area Survivors/Reports/C# Symbol Overview`
- `Area Survivors/Reports/C# Symbol Index`
- `Area Survivors/Reports/Scene Prefab Overview`
- `Area Survivors/Reports/Scene Prefab Structure`
- `Area Survivors/Reports/Scene Prefab Search`

Unity report menu output is saved under `TokenReports/UnityReports/`.
The Unity Console receives only the saved path plus line/character counts.

Recommended low-token flow for asset cleanup review:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report asset-references
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/filter-asset-reference-report.ps1 -Top 10
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/filter-asset-reference-report.ps1 -Top 10 -ExportPath TokenReports/UnityReports/asset-review-notes.md -ExportFormat md
```

Use the first command only when the underlying references may have changed.
For repeated review, prefer reusing the latest report with `filter-asset-reference-report.ps1`.

Run Scene/Prefab search from the CLI:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/safe-unity-search.ps1 -Query BuildMode
```

## Repeatable Benchmark

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaTokenHealth.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-health.ps1 -FailOnIncrease
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-health.ps1 -IncludeUnity
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-benchmark-heavy.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-benchmark-heavy.ps1 -IncludeRtk -IncludeUnity
```

`token-health.ps1` is the daily lightweight check and uses `TokenReports/token-daily-baseline.json`.
`token-benchmark-heavy.ps1` runs the historical high-output range `e68ca9a..2771c1c`; use it only when intentionally checking heavy-output regressions.
Use `token-health.ps1 -FailOnIncrease` for a compact periodic check that exits non-zero when a daily health row increases beyond the configured threshold.

Summarize recorded command token estimates:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Days 7 -Top 10
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Days 1 -Top 8
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Path TokenReports/2026-06-23.jsonl -Top 8
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Days 1 -Top 8 -IncludeBenchmark
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-report-summary.ps1 -Since "2026-06-20 16:30" -Kind safe_command,daily_health
```

Benchmark records are excluded by default from `token-report-summary.ps1`.
Passing `-Path TokenReports/YYYY-MM-DD.jsonl` summarizes only that report file.

Archive old JSONL reports instead of deleting them:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/archive-token-reports.ps1 -OlderThanDays 1 -WhatIf
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/archive-token-reports.ps1 -OlderThanDays 1
```

Use lightweight start/end checks:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/start-token-check.ps1 -UiPercent 12.5 -BudgetTokens 1000000 -Note "Phase 4 HUD work"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/end-token-check.ps1 -CurrentPercent 50.0 -IncludeUnity
```

## Coverage for Untracked Usage

Command reports do not include Codex fixed context, chat text, screenshots, or tool output that was run outside `safe-*` / `Run-WithTokenReport.ps1`.
Record the Codex UI usage percentage at the start and end of a long session, then compare it with recorded command output:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/start-token-check.ps1 -UiPercent 12.5 -BudgetTokens 1000000 -Note "Phase 4 HUD work"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/session-coverage.ps1 -CurrentPercent 50.0 -Save
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/end-token-check.ps1 -CurrentPercent 50.0 -CoverageNote "Phase 4 HUD work"
```

If `-BudgetTokens` is unknown, omit it. The script will still record the UI percentage delta and TokenReports total, but untracked token count will remain a percentage-only estimate.

Use `record-untracked-usage.ps1` to add manual estimates for known non-command usage:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/record-untracked-usage.ps1 -Category chat_summary -EstimatedTokens 1200 -Note "Long final answer and user clarification"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/record-untracked-usage.ps1 -Category screenshot -ImagePath "C:\path\screen.png" -Note "HUD screenshot inspected by Codex"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/record-untracked-usage.ps1 -Category direct_tool_output -EstimatedTokens 3000 -Note "exec_command output was returned directly instead of safe-command"
```

Suggested categories:

- `chat`
- `assistant_response`
- `fixed_context`
- `screenshot`
- `direct_tool_output`
- `reasoning`
- `manual_adjustment`

`session-coverage.ps1` subtracts both command reports and `manual_untracked_usage` records from the UI-derived estimate, so repeated manual entries make the remaining unknown bucket smaller.

## Screenshot Lightening

Create a smaller image before asking Codex to inspect a large screenshot:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Optimize-AreaScreenshot.ps1 -InputPath "C:\path\screen.png"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Optimize-AreaScreenshot.ps1 -InputPath "C:\path\screen.png" -CenterCrop -MaxWidth 640 -MaxHeight 360
```
