# Token Efficiency Architecture Closeout

## Scope

長期作業で増大していた常時コンテキスト、コード読取、検証出力、Tool導線、外部記憶、TokenReports全件集計を構造的に縮小した。

## Completed

- `AGENTS.md`を常時必要な安全境界とルーティングへ縮小し、詳細ルールを`Docs/AgentRules/`へ分離した。
- 不要なRTK説明、形骸化したSkill、直接Wrapper導線を整理した。
- C#／PowerShellの自動構造Indexを導入し、`Code.Symbol`／`Code.File`から限定探索できるようにした。
- `GameManager`を同一partial MonoBehaviourのFacadeと7責務ファイルへ分割し、Scene参照とpublic APIを維持した。
- `area-tool.ps1`を検索、読取、diff、Graphify、Unity、Token計測の型付き単一入口とし、操作契約を`AreaTool/operations.psd1`へ集約した。
- Validator実行を`AreaValidationBridge`のrun ID付きJSON結果へ統一し、公開`Unity.Validate`からmarker契約を削除した。
- Unity Compileの固定回数上限を廃止し、成功後の同内容反復を避ける規則へ変更した。Play Mode最大2回は維持した。
- SHA一致時だけ使うC#／PowerShell意味要約Cacheを導入し、正式ルール、Skill、Schema、Scene系を対象外にした。
- Obsidian外部記憶を廃止し、重複ルール、Vault用スクリプト、個人Skill、死んだグローバルRTK参照を削除した。Vaultデータはアーカイブとして残した。
- TokenReportsをJSONL正、Library内SQLite Indexの増分集計へ移行し、追記、途中行、短縮、削除、破損復旧を自動化した。
- `area-tool`経由で生成されるTokenReportsへOperation名を付与し、Operation別集計を可能にした。

## Verification

- `area-tool -Operation Test.Commands`: 7 modules passed。
- `current-context-guard.ps1`: AGENTS／currentともstatus `ok`。
- 締め作業の直近8 recordsは表示7,232 token、measurement coverage 100%、blocked／high／critical 0件。
- コード、文書、Tools、ctxの対象差分は`git diff --check`成功。Unity生成Prefabの空シリアライズ値は一般テキスト検査の対象外とし、既存のCompile／Validator／通しプレイ結果を正とした。
- 固定11シナリオ読取コストはGit HEAD比で、AGENTS 79.5%減、構造Index 90.5%減、GameManager責務分割88.8%減、合計88.9%減。
- 意味要約3件は全文基準95.2%減、先頭50行基準49.9%減。
- Token集計は旧全件走査と5条件の件数、capture、displayed、blockedが一致した。
- Token集計時間は旧6.8〜9.3秒、初回Index約1.7秒、変更なし約0.4〜0.7秒、型付き入口約0.76秒。
- Validator契約はpassed／failed／error／run ID不一致を通過し、Bridge経由の実Validatorはstructured passed。
- 許可済み追加分を含むUnity Compile 3回成功。最終Console Error 0件。
- 20%逓減後の通しプレイはユーザー確認済みで問題なし。

## Remaining Observation

- 数回の実運用後、Operation別TokenReportsと`Code.Summary.Stats`のhit率を再集計し、固定Benchmarkとの差を確認する。

## Notes

- Branch: `feature/02_GameSystemUpdate`。
- 初回の締め作業では、Skill整理時にcommit／pushを個別明示制へ変更していたため公開工程が抜けていた。
- 旧Closeout Skillとの比較により、Project状態確認、Token集計、検証結果とcommit／push結果の報告も標準工程として不足していたことを確認した。
- 「締め作業」の明示依頼をProjectのcommit／pushまで含む一連の許可として`closeout.md`へ再定義した。廃止済みのObsidian、外部記憶リポジトリ、自動Skill更新は復活させない。
