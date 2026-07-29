# Closeout Rules

- ユーザーが「締め作業」「作業終了」を明示した場合は、Projectの状態確認、低コストToken集計、current整理、検証、commit、現在branchのpushまでを一連の締め作業として行う。通常タスクの完了だけでは自動実行しない。
- 最初にstatusとdiff概要を確認し、必要な対象ファイル／hunkだけを追加確認する。Scene／Prefab本文は限定Validatorで確認できない場合を除いて展開しない。
- Token集計は型付き入口の`Token.Summary`を使い、表示出力として計測できた範囲とcoverage不足を区別して報告する。UI使用率や予算が未提示なら推測せず、締め作業を止めない。
- `ctx/current.md`は現在の目的、最新判断、最新検証、TODO／Blockerだけを保持し、完了履歴は`ctx/archive/`へ移す。
- コード、Wrapper、Validator、Tool Schemaで再発防止済みの障害を別の履歴へ重複記録しない。
- 最新のタスク相応の検証結果を確認し、対象差分の`git diff --check`と`current-context-guard.ps1`を通す。Unity Compile／Play Modeは未検証差分に必要な場合だけ行い、見た目確認のためには開始しない。
- Projectリポジトリだけを対象に、秘密情報、Temp、lock、意図しない生成物を除外してcommitし、現在branchをpushする。外部記憶リポジトリは扱わない。
- 最終報告へ検証結果、Token計測範囲、commit hash、push結果、残存TODO／Blockerを明記する。
