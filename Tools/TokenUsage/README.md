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

## Repeatable Benchmark

```powershell
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1 -IncludeRtk
powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/Run-TokenBenchmark.ps1 -UpdateBaseline
```

The default benchmark uses the historical high-output range `e68ca9a..2771c1c`.
When a baseline exists at `TokenReports/token-benchmark-baseline.json`, the benchmark prints current-vs-baseline deltas.
