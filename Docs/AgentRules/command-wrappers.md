# Command Wrapper Rules

公開入口は `Tools/TokenUsage/area-tool.ps1`、操作契約の正は `Tools/TokenUsage/AreaTool/operations.psd1`。

## 公開契約

- `-Operation Schema` は操作名、risk、必須引数、許可引数を返す。
- 入力は入口の型宣言と操作Schemaの両方で検証し、余分な引数を内部Wrapper実行前に拒否する。
- 結果は `operation`、`status`、`exit_code`、`result_count`、`capture_path`、`displayed_estimated_tokens` を共通項目とする。
- `status` は `success`、`guarded`、`failed`。no-matchは状態を変えない正常結果として扱う。
- `-Json` は機械処理用の共通Envelope、通常表示は内部結果と末尾1行の `area_tool_result` を返す。

## 内部実装

- `safe-*`、`Invoke-Area*`、Reporter、Editor Runnerは内部実装として保持し、公開Docsから直接呼ばない。
- Dispatcherは文字列Evalや入れ子 `powershell -Command` を使わず、検証済みhashtableをスプラットして内部実装を呼ぶ。
- 内部WrapperのGuard、Mutex、TokenReports、capture、終了コードを上書きしない。
- 読み取り専用とUnity状態変更はSchemaのriskで区別する。自動フォールバックしない。

## 自己テスト

- 新規操作はSchema、`area-tool.ps1`の型・mapping、`CommandToolSelfTests/*.tests.ps1`を同時に更新する。
- 自己テストはSchemaと型の一致、実装Path、余分な引数拒否、共通Envelope、危険な文字列Eval不在を検証する。
- Orchestratorへ個別検査を書かず、責務別テストへ追加する。
- 参照0になった旧公開Alias／薄いShimは削除し、互換入口として残さない。
