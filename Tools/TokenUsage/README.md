# Token Usage Tools

AreaSurvivorsの検索、限定読取、diff、Unity操作、Token計測は型付き単一入口を使います。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Schema
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Schema -Target Code.Read
```

## Examples

```powershell
# C# / PowerShell structure
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Symbol -Symbol PlayerController
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.File -Path Tools/TokenUsage/area-tool.ps1

# Search and focused reading
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Search -Pattern BuildMode -Path Assets/AreaSurvivors/Scripts -SearchMode Files
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Code.Read -Path AGENTS.md -StartLine 1 -EndLine 40 -PrintOutput

# Git
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Git.Status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Git.Diff -DiffMode Summary

# Unity
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Unity.Console -ConsoleLevel Error -MaxResults 30

# TokenReports
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Token.Summary -Recent 20

# Self-test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/TokenUsage/area-tool.ps1 -Operation Test.Commands
```

`-Json`は共通Envelopeを返します。通常表示は内部結果に続いて `area_tool_result` を1行返します。

`safe-*`、`Invoke-Area*`、Reporter、Editor Runnerは内部実装です。新しい通常手順やDocsから直接呼びません。

`Token.Summary`はPython標準SQLiteを使い、JSONLの追記分だけを`Library/AreaAgentIndex/TokenReports/`へ自動Index化します。JSONLが正で、Indexの更新・失効・破損復旧に手作業は不要です。

Scene／Prefab探索は `Unity.Search` または専用Reporterを使い、YAML全文を読みません。Unity reportは `TokenReports/UnityReports/` に保存します。
