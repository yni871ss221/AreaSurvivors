# Graphify Pilot

AreaSurvivorsのC#／PowerShell構造探索を短いサブグラフへ絞るPilotルール。ソース、Unity Scene/Prefab、既存Reporter/Validatorが常に正であり、Graphifyは候補特定の前段だけに使う。

## Scope

- Graphify `0.9.26`をリポジトリ外の隔離venvへ固定する。
- `--code-only`、`--no-cluster`、`cluster-only --no-label --no-viz`だけを使い、モデルAPI、Docs/PDF/画像抽出、MCP、watch、git hook、Codex installerは使わない。
- Vendorコードを再導入する場合は、Pilot対象へ混ぜず専用ignoreを追加してから再計測する。
- `graphify-out/`は生成物としてGit管理しない。

## Entry Point

```powershell
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Status
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action EnsureFresh
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Explain -Source "EnemyController"
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Path -Source "ProgressionStore" -Target "LobbyScreen"
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Affected -Source "AdvancedWeaponArea" -Depth 2
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Query -Question "DefeatRemainingEnemiesForStageTransition" -Context calls -Budget 400
Tools/TokenUsage/safe-graphify-pilot.ps1 -Action Update
```

`AREA_SURVIVORS_GRAPHIFY_PYTHON`を指定しない場合、Wrapperは`%USERPROFILE%\.cache\AreaSurvivors\graphify-pilot-0.9.26\Scripts\python.exe`を使う。

通常運用では、その作業で実際に`Path / Explain / Affected / Query`を使う直前に限り`EnsureFresh`を1回使う。grep/readだけで完結する作業、候補シンボルが未確定の段階、同じfresh graphを続けて読む間は再実行しない。graphがfreshなら再構築せず、staleまたは未生成の時だけコード限定の完全再構築を行う。`Update`を毎回無条件に実行しない。

## Routing

- 既知シンボルのcaller/callee等の直接近傍: `Explain`
- 2つのシンボル間の経路: `Path`
- 変更影響候補: `Affected`
- 呼び出し経路の限定探索: exact symbolの`Query -Context calls`
- 定義場所、実装内容、正確な文字列、数値、属性、コメント: `safe-search` / `focused-search`
- 実装内容の最終確認: 対象ファイルだけ`safe-read`
- `.unity`、`.prefab`、`.asset`、Animator、serialized reference: Unity Reporter / Validator

自然文をそのまま`Query`へ渡すと一般語が別シンボルへseedされ、数百nodeへ広がるため通常運用では使わない。先に`safe-search -FilesOnly`でシンボルを確定するか、`Explain`を使う。

## Freshness And Integrity

- WrapperはC#／PowerShell／Pilot Pythonの更新時刻がgraphより新しい場合、`guard_code: 61`でQueryを拒否する。
- `EnsureFresh`は同じ更新時刻判定を使い、fresh時は約0.4秒でno-op、stale時だけ約33秒の完全再構築を実行する。
- `Update`はWindows incremental mergeを使わず、コード限定の完全再構築を行う。0.9.26の再評価でも、小さなC# fixture追加で8,480から8,797 nodesへ増え、fixture削除後も8,793 nodesが残った。
- Graphify CLIの更新契約は`graphify update <path>`である。`graphify extract <path>`を`--force`なしで差分更新に転用すると、変更ファイル分だけのgraphで既存graphを上書きし得るため、incremental検証には使わない。
- Build後のraw graphが1,000 nodes未満なら`guard_code: 62`、Update後に既存の80%未満なら`guard_code: 63`で停止する。
- cluster後はnode-link schema、重複ID、絶対source path、project外source pathを`graphify-pilot-inspect.py`で検査する。
- Graphifyが警告する`KnightSpinePrototype.json`と`SceneTemplateSettings.json`の0 nodeは、C#／PowerShell構造Pilotの対象外として扱う。

## Automatic Verification And Usage Log

- `Path / Explain / Affected / Query`の実行結果を`TokenReports/graphify-pilot-usage.jsonl`へ1行JSONで追記する。
- 記録項目はversion、`production / evaluation`区分、Action、Source、Target、elapsed、推定出力token、result count、verification理由、rebuild有無、表示制限、full capture path、fallback IDとする。
- `Affected`は既定で20結果または推定500 tokenを超える全出力を会話へ展開せず、先頭20結果だけを表示する。全件は`full_capture_path`へ保存され、意図的に全件表示する場合だけ`-ShowFullAffected`を使う。
- `ambiguous`、`INFERRED`、高degree省略、Path不成立、`Affected`結果0～1件または既定上限超過を検出した場合、`graphify_verification_required: true`と理由を表示し、対象言語・Pathに対応した`focused-search`コマンドを提示する。
- 提示コマンドには`GraphifyFallbackId`を含める。そのコマンドが実行完了した場合だけ、同じJSONLへ`action: Fallback`と`fallback_executed: true`を追記する。推奨件数と実利用件数はfallback IDで対応付ける。
- 評価器は`-UsageCategory evaluation`を使う。通常のWrapper利用は既定の`production`とし、20件再評価ではproductionだけを集計する。

## Pilot Result

- 当時のThirdParty除外後: 535 code files、8,488 nodes、18,769 links、graph約15MB。
- `Explain`、`Path`、`Affected`、exact symbol＋`calls`は約1秒で限定的な構造を返した。
- 自然文Queryは700 nodes前後へ膨張し、出力budget内で重要nodeが切れるため不採用。
- Graphify内蔵benchmarkは79.7倍削減を表示したが、全コーパス読込との比較であり、既存`safe-search`との削減率としては扱わない。

## Promotion Decision

2026-07-26に実タスク相当12件を、Graphifyの完全一致`Explain / Path / Affected`と、`focused-search`（上位3ファイル、context 3、各1 match）で比較した。tokenはGraphify raw resultと`safe-read`のcapture推定値だけを数え、Wrapperコマンド・メタデータを除外した。

- 全12件で要求シンボルを取得した。
- Graphify合計推定2,217 tokens、既存検索5,288 tokensで、入口探索の出力量は58.1%減。
- Graphify合計18.4秒、既存検索35.3秒で、所要時間は47.7%減。
- `Path`は94.2%減、`Affected`は58.6%減。一方、`Explain`は既存検索より42.3%多い出力だった。
- `Path` 2件に`ambiguous`警告があり、`Affected LobbyScreen`は内部呼び出し1件だけで影響範囲として不十分だった。
- PowerShellの`Path / Affected` 5件でも全件取得し、Graphify 272 tokens／7.7秒、既存検索1,270 tokens／12.1秒で、出力量78.6%減、所要時間36.2%減だった。

以上から、複数ファイルにまたがる完全一致シンボルでは`Path`と`Affected`を「標準の第一手」へ昇格する。`Explain`は直接近傍が必要な場合だけ使い、定義・実装確認では昇格しない。Graphifyを最終根拠・網羅的影響分析・コード内容確認の代替にはせず、`ambiguous`、`INFERRED`、高degree省略、少なすぎる結果は既存検索と対象ファイル読み取りで確認する。
