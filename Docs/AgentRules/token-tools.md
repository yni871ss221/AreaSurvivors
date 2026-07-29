# Token Tool Router

C#／PowerShell調査、検索、読取、diff、Unityコマンド、Token計測の短い入口。下表から今回の目的に一致する文書を1つだけ読む。

| 目的 | 詳細 |
|---|---|
| C#／PowerShellの型・メンバー・参照候補、検索、限定読取、diff | `Docs/AgentRules/code-navigation.md` |
| 型付き操作Schema、Wrapper実装、自己テスト | `Docs/AgentRules/command-wrappers.md` |
| Unity接続、Compile、Menu、Reporter、Editor Runner | `Docs/AgentRules/unity-command-tools.md` |
| TokenReports、開始・終了マーカー、coverage | `Docs/AgentRules/token-measurement.md` |

## 共通入口

- 通常操作は `Tools/TokenUsage/area-tool.ps1 -Operation <name>` だけを公開入口にする。
- 操作名と許可引数は `-Operation Schema [-Target <name>]` で取得する。個別Wrapperの引数を推測または事前読取しない。
- 定義・構造、文字列検索、限定読取、diff、Unity、Token計測は、それぞれSchemaの `Code.*`、`Git.*`、`Unity.*`、`Token.*` を使う。
- `safe-*`、`Invoke-Area*`、Reporter、Editor Runnerは内部実装。新しいDocsや通常コマンドから直接呼ばない。
- 入口またはSchemaの追加・変更後は `area-tool.ps1 -Operation Test.Commands` を実行する。
- 再現するTool不具合、情報漏洩、データ破損、Unity／Editor異常は `command-failure-playbook.md` に従う。

## 保守

- 操作契約の正は `Tools/TokenUsage/AreaTool/operations.psd1` とする。
- 新規操作はSchema、型付き入口、責務別自己テストを同時に更新する。コマンド例へ未登録の直接Wrapperを追加しない。
- 機械判定できる引数契約、Guard、個別障害を文書と重複管理しない。
