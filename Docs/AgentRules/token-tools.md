# Token Tool Rules

- 日常の高出力候補コマンドは `Tools/TokenUsage/guarded-command.ps1`、`safe-status.ps1`、`safe-diff.ps1`、`safe-search.ps1`、`safe-read.ps1`、`safe-unity.ps1` を入口にする。
- 小規模変更では既知の中核ファイル2〜3個から読み、初手で `Assets/AreaSurvivors` 全体へ広域検索しない。
- `safe-search.ps1 -PrintOutput` は原則 `-First 20` 以下。広域検索は `-FilesOnly` または `-HitSummary` を先に使う。
- `safe-read.ps1` は `-Pattern` / `-Context` または `-StartLine` / `-EndLine` を優先し、長い `Get-Content` を避ける。
- Scene/Prefab内検索は `Tools/TokenUsage/safe-unity-search.ps1 -Query <対象名>` またはUnity Reporterを使い、YAML全文を読まない。
- Unity Reporter実行は `run-unity-report.ps1 -Report <name>` を使い、長い `unicli exec Eval --code ...` を毎回手打ちしない。
- Reporter候補や実行名の確認は `reporter-candidates.ps1` を使い、既存Reporterの有無を先に確認する。
- `git diff` は対象ファイル指定、必要なら `--name-only`、`--stat`、`safe-diff.ps1` を使う。
- TokenReportsの原因分析は `token-report-summary.ps1 -Recent <件数>` または `-SinceLastStart` を使う。
- 作業開始時は `start-token-check.ps1`、終了時は `end-token-check.ps1` を使う。Heavyベンチは明示時だけ実行する。
- プロジェクトの重いファイルや未参照候補は `project-weight-report.ps1` で候補だけを見る。削除判断は別作業にする。
- Asset Reference Reporterの結果から `review-candidate` だけを見る時は `filter-asset-reference-report.ps1 -Top <件数>` を使い、全文を読み返さない。
- アセット整理の標準手順は `run-unity-report.ps1 -Report asset-references` → `filter-asset-reference-report.ps1 -Top <件数>` → 必要時だけ `-ExportPath` で判定メモ出力、の順にする。
- 複数ファイルから実装箇所を探す場合は `focused-search.ps1` を使い、上位ファイルの該当箇所だけ読む。
- 作業種別ごとの検証コマンドは `validation-preset.ps1` で確認し、必要なプリセットだけ実行する。
- `Safe-Command.ps1` でblockedになった出力は表示せず、RTK、対象パス指定、Reporter/Validator、`Select-Object -First` などへ切り替える。
