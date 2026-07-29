# Token Measurement

TokenReportsとCodex UI使用率の公開入口は `Tools/TokenUsage/area-tool.ps1`。

## 記録

- 開始: `-Operation Token.Start -Task "<依頼>" [-UiPercent <開始率>] [-BudgetTokens <任意予算>]`
- 終了: `-Operation Token.End [-CurrentPercent <終了率>]`
- 値が不明でも作業は止めず、推測しない。
- TokenReports外の会話、推論、画像、tool metadataは自動集計されない。必要時だけ根拠付き概算として別記録する。

## 集計

- `-Operation Token.Summary -Recent <N>`
- 集計はJSONLを正とし、`Library/AreaAgentIndex/TokenReports/`の再生成可能なSQLite Indexへ追記分だけ自動反映する。手動更新やCache内容の読取は行わない。
- 構造整理の変更前後比較は `-Operation Benchmark.ReadCost [-BaselineRef <ref>]`。固定シナリオの表示テキスト推定だけを比較し、JSONは`TokenReports/Benchmarks/`へ保存する。
- 開始以降は `-SinceLastStart`、日数は `-Days <N>`、上位件数は `-MaxResults <N>`。
- `displayed_estimated_tokens` は表示command output推定、`capture_estimated_tokens` はraw上限。課金tokenや総モデル消費と呼ばない。
- coverage不足時はWrapper外tool、会話、推論等が集計外と明記する。UI開始率・終了率・budgetが揃う場合だけ全体差を比較する。

## 判断

- ファイル分割候補は複数作業の読取頻度×表示量で順位付けする。
- 構造や導線変更後は変更前履歴だけで追加分割を決めず、固定シナリオまたは数回の同種作業後に再計測する。
- Heavyベンチ、全文capture再読、Console確認反復は明示的な比較目的がある場合だけ行う。
