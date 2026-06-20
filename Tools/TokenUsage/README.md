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

## Repeatable Benchmark

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1 -IncludeRtk
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1 -UpdateBaseline
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Invoke-AreaTokenHealth.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-health.ps1 -FailOnIncrease
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/token-health.ps1 -IncludeUnity
```

The default benchmark uses the historical high-output range `e68ca9a..2771c1c`.
When a baseline exists at `TokenReports/token-benchmark-baseline.json`, the benchmark prints current-vs-baseline deltas.
Use `Invoke-AreaTokenHealth.ps1 -FailOnIncrease` for a compact periodic check that exits non-zero when a benchmark row increases beyond the configured threshold.

## Screenshot Lightening

Create a smaller image before asking Codex to inspect a large screenshot:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Optimize-AreaScreenshot.ps1 -InputPath "C:\path\screen.png"
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Optimize-AreaScreenshot.ps1 -InputPath "C:\path\screen.png" -CenterCrop -MaxWidth 640 -MaxHeight 360
```
