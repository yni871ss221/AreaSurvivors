# Command Failure Playbook

再現するTool不具合、情報漏洩、データ破損、Unity／Editor異常が発生した場合だけ読む。no-match、Guard拒否、引数・Path・patch不一致は障害履歴にせず、表示された正式契約へ直して限定再実行する。

## 停止境界

- 状態変更を止める。別Shell、Eval、手動Editor操作、推測修正、自動フォールバックへ切り替えない。
- 同じ入力で再現するかを読み取り専用の最小確認1回で判定する。再現しない一過性事象を恒久ルールへ追加しない。
- 実行コマンドと引数、終了コード、経過時間、timeout、capture path、権限境界、Unity／Editor状態を保存する。
- 秘密値を含む可能性があるcommand lineやcapture本文は会話や記録へ展開せず、伏字化した事実だけを残す。

## 調査順序

1. `Safe-Command.ps1`／TokenReportsから、実コマンド、終了コード、`timed_out`、`capture_path`、状態変更の有無を確定する。
2. 境界を Transport、CLI契約、権限、Unity状態、AssetDatabase、C# Compile、対象データへ分類する。
3. Handler、Wrapper、生成物の作成・削除条件、UnityのImport／Compile／Play遷移を該当箇所だけ読む。
4. Unity状態を変えない最小入力、Parser、Reporter、Validatorのいずれかで原因を再現する。
5. 危険入力を入口で拒否するか、安全な実行順と成功条件を単一Wrapperへ固定する。
6. Wrapper固有テストと `command-tools-self-test.ps1` を通してから元の作業を再開する。

## 再発防止の置き場所

- 引数、Path、引用符、出力上限、危険操作: WrapperのGuardと責務別自己テスト。
- Unityの完了条件、serialized reference、Scene／Prefab制約: Reporter／Validator。
- 人の判断が必要で自動化できない境界: 該当する `Docs/AgentRules/*.md` に1回だけ記載。
- 現在の目的、未完了、直近検証: `ctx/current.md`。
- コードとテストで防止済みの個別障害、完了履歴、再現しない一過性事象: 保存しない。

## 再開条件

- 根本原因と影響範囲が証拠で説明できる。
- 同じ失敗を入口で防ぐ、または正常終了を機械判定できる。
- 限定自己テストが成功している。
- 原因未確定なら推測で実装を続けず、証拠と再開条件を `TODO`／Blockerにする。
