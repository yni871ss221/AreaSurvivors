# Unity Command Tools

Unity操作も公開入口は `Tools/TokenUsage/area-tool.ps1`。ユーザーの明示依頼なしに `Unity.Play -PlayAction Enter` を実行しない。

## 操作

- Console: `-Operation Unity.Console -ConsoleLevel Error|Warning|Log -MaxResults <N>`
- Compile確認: `-Operation Unity.Compile`
- Menu: `-Operation Unity.Menu -MenuPath <path>`
- 構造化Validator: `-Operation Unity.Validate -MenuPath <path>`
- Asset Import／Refresh: `Unity.Import`／`Unity.Refresh`
- Scene／Prefab検索: `Unity.Search -Pattern <name>`
- Editor Runner: `Unity.Runner -Phase <phase> -ScriptPath <path> ... -Concise`
- Play状態: `Unity.Play -PlayAction Status|Enter|Exit`

## CompileとRunner

- Unity外で変更したC#は、変更した全スクリプトをRunnerの `-ScriptPath`／`-DependencyPaths` へ渡してImportしてからCompileする。
- RunnerはImport→Compile鮮度確認→Menu完全一致→実行の順を固定する。
- compile manifestに削除済みC#が残る場合は `guard_code: 46` で停止する。同じRunnerへ `-BatchRefresh` を付けて削除を反映する。
- Menu Validatorは共通Bridgeが発行した今回の`run_id`と一致する構造化結果を必須とし、Menu受付や終了コード0だけで完了扱いにしない。
- 結果は `Library/AreaValidation/Results/<run_id>.json` に保存し、通常表示はstatusと件数、失敗issue最大5件だけにする。既存Validatorの内部check数は推測せず`unknown`、共通`Require`利用時だけ実数を返す。
- `passed`は検証成功、`failed`は`Debug.LogError`等の不整合検出、`error`は例外または未登録Menu、結果未生成は`infrastructure_failure`として区別する。

## 状態境界

- Play中のEval、引用符・改行を含むEval、PlayExit直後の追加Unityコマンドを行わない。
- named pipe拒否はUnity無応答と混同せず、権限境界を確認して同じ操作を最大1回だけ再実行する。
- Unity操作は内部Mutexで直列化し、ReporterのQueryやValidatorの`run_id`が今回要求と一致しなければ利用しない。
- Console件数は `console_matched_count` を使い、UniCLIの全体 `totalCount` を対象種別件数に使わない。

## 検証

- Compileと関連Validatorをまとめ、Console Error確認は最後に1回だけ行う。
- Unity入口変更後は `Test.Commands` を先に通し、初回Unity実行と並列にしない。
