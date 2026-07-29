# Current Task

## Goal

武器レベルアップの逓減処理を入れた現行ビルドで再度通しプレイを行い、新しい`token_run_log.jsonl`を前回結果と比較して難易度調整を判断する。

## Latest Decision (2026-07-28 Retest After Weapon Diminishing Returns)

- 武器レベルアップの逓減処理により、主力武器の強化量と終盤火力が抑えられる見込み。
- 前回ログから提示したXP終盤曲線、ドラゴンHP、武器選択weight／新武器保証、高難易度時の建造物復活率の調整案は、次の通しプレイ結果が出るまで保留する。
- 次回ログでは、到達レベルとXP、各ステージ・ボスの所要時間、武器別ダメージ比率とDPS、プレイヤーの最低HP・被ダメージ・死亡、建造物の被ダメージ・破壊・復活・中心塔被害、敵の撃破数とpeak aliveを前回値と比較する。
- 新しいログを確認するまでは追加の難易度変更を行わない。
- TODO: 次回の通しプレイ後に新しい`token_run_log.jsonl`を受領し、前回ログとの差分から最終的な難易度調整を提案する。
- Blockerなし。

## Latest Investigation (2026-07-28 Difficulty Log Review)

- 対象ログは14ラン。最終ランはS1 D4→S2 D3→S3 D2→S4 D2をクリアし、非ボス時間480.3秒＋ボス戦420.5秒＝実戦約15:01。5,444体出現、5,256体撃破、peak alive 200。
- 最終ランはLv32、基礎XP 71,461、適用XP 85,753、XP倍率1.2。現行曲線でLv40にはLv4以降237,793 XP必要。同じ敵処理で倍率2.5でも178,653 XP、到達予測Lv37。
- `xpRequirementGrowthEnd`を1.1→1.075へ下げるとLv4→40必要XPは178,329となり、同じ基礎XP・倍率2.5でLv40直後へ到達する。倍率1.2の同ランはLv34予測。Lv18以降の段差ではなく全体を連続的に緩和する。
- ボス時間はオークキング63.0秒、ゴブリンロード80.5秒、リッチ86.1秒、ドラゴン191.0秒。前ランでもドラゴン168.2秒。ドラゴンHP17,920→13,440（リッチの1.5倍）なら単純比例で約126〜143秒を見込む。
- 最終ランのダメージ構成は黄金の弓由来の「弓」1,226,788／89.1%、監視塔6.2%、フロスト2.4%、旗1.1%。前ランもソードラッシュ由来の「スラッシュ」93.8%。初期武器へ強化と進化が集中し、後発武器がほぼ育たない構造が共通。
- 最終ランのプレイヤーは最大HP350、最低HP20、被ダメージ497、回復624、死亡0。個人耐久は十分に危険域へ入っており、敵攻撃力の全体強化は不要。
- 建造物は被ダメージ24,608、破壊68、復活51だが、中心塔は最大HP900・被ダメージ0・終了時満タン。外壁128枚＋ボスごとの50%復活が高難易度でも中心塔への圧力をほぼ遮断している。
- 調整優先度案: 1) XP終盤曲線、2) ドラゴンHP、3) 初期武器偏重の選択weight／新武器保証、4) 高難易度時だけ建造物復活率を下げる。敵攻撃力の一律強化は見送る。
- ログ上の改善候補: JSONL先頭行にUTF-8 BOMがありstrict JSON parserで失敗する、`survivedSeconds`がボス中停止して総プレイ時間にならない、敗北理由fieldがなく塔破壊／プレイヤー死亡を建造物HPから推定する必要がある。
- TODO: ユーザーが調整案の採否と優先順位を決める。今回は分析のみで実装変更なし。
- Blockerなし。

## Latest Completed Work (2026-07-28 Weapon Upgrade Diminishing Returns)

- 武器ごと・強化項目ごとの選択回数を`WeaponController`へ保持し、当初の10%減少からユーザー確認後に20%減少へ調整した。1回目100%、2回目80%、3回目64%、4回目51.2%と強化量が逓減する。別武器と別項目の選択回数は互いに影響しない。
- 対象項目は攻撃範囲、飛翔距離、爆発範囲、ノックバック、攻撃間隔、速度低下。攻撃力、攻撃回数、効果時間は従来どおり固定強化量を維持する。
- 攻撃間隔は短縮率を逓減させる。基礎倍率0.9の場合、選択ごとの適用倍率は0.9、0.92、0.936となり、短縮量が10%、8%、6.4%へ減少する。
- 進化後も進化前武器の同項目選択履歴を引き継ぐ。ラン開始時は全逓減履歴をリセットする。
- 全16件の加算強化と全8件の攻撃間隔強化を共通逓減経路へ統一し、レベルアップパネルの変更後数値も実際に適用する逓減後の値を表示する。
- 進化武器テスト開始プロファイルも、対象項目だけ逓減後の累積値を使うよう同期した。
- `RunWeaponUpgradeDiminishingValidator`を追加し、逓減計算、武器／項目間の独立性、進化履歴引継ぎ、ランリセット、全選択肢の共通経路使用を検証する。
- 初回の全進化武器Validatorは成功marker待機で停止した。原因はゲーム設定が確定仕様のエクスカリバー3秒である一方、既存Validatorだけが旧5秒を要求していたこと。ゲーム設定は変更せず、Validator期待値を3秒へ同期した。
- 実装5 C#を明示Import後のUnity Compile成功。Validator同期後の2回目Compileも成功。`Area Survivors/Validate/Run Weapon Upgrade Diminishing Returns`と`Area Survivors/Validate/Weapon Evolution Batch`はいずれもfresh success marker確認済み。
- 20%逓減への追調整は変更2 C#を明示Importし、Unity Compile 1回成功。専用逓減Validatorと全進化武器Validatorのfresh success markerを再確認した。
- 読取診断では、最初に実行ラッパー内ツール名とユーザー領域のWrapperパスを誤認してコマンド開始前に停止した。利用可能ツールを`shell_command`、Wrapperをプロジェクト内`Tools/TokenUsage`へ確定し、以後その正式入口だけを使用した。Unity／ソースへの状態変更はなかった。
- Play Modeは開始していない。Scene／Prefab／HUDレイアウトは変更していない。
- TODO: ユーザー実機で、同じ武器の同じ対象項目を連続選択した際に表示上の増分が100%→80%→64%となり、別項目と別武器の初回増分は100%のままであることを確認する。
- Blockerなし。

## Latest Completed Work (2026-07-28 Fire Explosion Range Visual)

- 共通の`ProjectileExplosionHitbox` Prefabへ、監視塔Range Fillと同じSprite／楕円Shape Spriteを使う薄赤のFillとOutlineを追加した。Area/Range VisualはRotation X/Y=0、`PaperBillboard`なし。
- Fill色はRGBA `(1.00, 0.18, 0.14, 0.22)`、Outline色は`(1.00, 0.30, 0.24, 0.68)`。爆発半径をそのまま横半径／縦半径へ使い、PaperMeshVisual内部Shapeの縦横比だけ相殺して実ダメージのCircleCollider2Dと一致させる。
- 爆発HitboxはFixedUpdate同期後にダメージを一度だけ処理し、Collider／Rigidbodyを無効化してから範囲Visualだけを爆発演出と同じ0.26秒まで保持する。
- `Projectile.showExplosionRangeVisual`を追加し、Fireball／FireMissile PrefabだけONにした。共通Hitboxを参照するArrow、BallistaArrow、GoldenArrow、PlayerArrow、TowerCannonballはOFFのままなので表示対象は水平展開せず限定される。
- `FireExplosionRangeVisualMigration`と`FireExplosionRangeVisualValidator`を追加し、Prefab参照、色、Sprite、Shape Sprite、初期非表示、Sorting Order、Rotation、Billboard不在、表示時間、対象2種だけの有効化を固定した。BootstrapでFireballを再生成する場合もフラグを復元する。
- 変更5 C#を明示AssetImport後、Unity Compile成功。Unity保存時に新規Prefab Componentの空プロパティ4行へ末尾空白が付く既知境界を検出したため、Migrationへ保存後YAML正規化を追加し、2回目Compile／移行成功。
- `Area Survivors/Validate/Fire Explosion Range Visual`と`Area Survivors/Validate/Combat Visual Rotation Guard`のfresh success marker確認、Console Error 0件、対象`git diff --check`成功。
- ユーザー実機確認で、監視塔から流用したFillの元テクスチャに含まれる魔法陣模様まで赤く描画されていることを確認した。楕円Shape Sprite参照は維持し、`PaperMeshVisual.useTexture=false`の単色Fillへ変更して中央模様だけを除去した。Validatorへ単色描画契約を追加し、再Compile、Prefab移行、両Validator、Console Error 0件、対象差分検査まで成功。
- 作業開始時の読取Wrapperパスを`.codex`配下と誤認した失敗と、Windows PowerShell 5.1非対応の三項演算子による診断式失敗を原因確定し、`command-failure-playbook.md`へ固定パス／PS5.1構文の再発防止を追加した。Unity／実装への状態変更前に解消済み。
- Play Modeは開始していない。
- TODO: ユーザー実機でFireball／FireMissileそれぞれを爆発させ、魔法陣模様がなく薄赤の楕円だけが約0.26秒表示され、見た目の外周が実際の巻き込み範囲と一致することを確認する。
- Blockerなし。

## Latest Completed Work (2026-07-28 WatchTower Revival Range Color)

- ユーザー実機確認で、復活後の建物当たり判定／ダメージと、宝箱・レベルアップパネル競合の修正はいずれも問題なし。
- 監視塔復活時、`WatchTower.RestoreAfterRevive`がRange Fillへ`rangeFillColor`を設定した後、`BuildingRevivalState.ApplyDestroyedVisual(false)`が全`PaperMeshVisual.color`を`Color.white`へ上書きしていた。
- 復活順序を、Health復元→共通Visual／Collider復元→建造物固有`RestoreAfterRevive`へ変更した。監視塔の半透明Range Fill色が最終値となり、壁・バリスタの固有Visual復元も維持する。
- `July25GameplayBugFixValidator`へ上記順序と、監視塔が`rangeFillColor`を再適用する契約を追加した。
- 変更2 C#を明示AssetImport後、Unity Compile成功。`Area Survivors/Validate/July 25 Gameplay Bug Fixes`と`Area Survivors/Validate/Combat Visual Rotation Guard`のfresh success marker確認、Console Error 0件、対象`git diff --check`成功。
- Play Modeは開始していない。Scene／Prefab／HUDレイアウト、監視塔Prefabの色・半径・Rotationは変更していない。
- TODO: ユーザー実機で監視塔を破壊→ボス撃破で復活させ、攻撃範囲が通常時と同じ半透明色で表示されることを確認する。
- Blockerなし。

## Latest Completed Work (2026-07-28 Revived Building Damage)

- 原因は、壁・バリスタ・監視塔の`breaking`破壊ラッチが初回破壊時の`true`のまま復活後も残っていたこと。
- 復活時のHealth／Collider自体は戻っていたが、復活後にHPが再び0になると二度目の`Break`がラッチで中断された。この結果、見た目とColliderは残る一方でHealthは死亡済みとなり、リザードマン／ドラゴン接触とドラゴンブレスの両方が以後ダメージ0になった。
- `IBuildableConstruction.RestoreAfterRevive`を追加し、`BuildingRevivalState.TryRevive`が正HP復元後、Collider再有効化前に各建造物の破壊ラッチ、完成状態、Visualを復元するよう変更した。
- 壁は`ApplyVisuals`、バリスタは`ApplyBuildVisuals`、監視塔は`ApplyVisuals`を再適用する。監視塔は範囲表示も復活する。
- `July25GameplayBugFixValidator`へ、復活順序、3実装のラッチ解除／完成状態／Visual復元、毎回`BuildingRevivalState`へ破壊を渡す契約を追加した。
- 初回Compileは`WatchTower.ApplyRangeVisual`の裸の`breaking`参照1件を見落としてCS0103。capture `C:\Users\yni87\AppData\Local\Temp\safe-command-9c0bda648c75430591efeff472f6efe3.txt`、Bee ExitCode 3、Play Mode停止／Unity応答中。全シンボル検索後、ラッチを削除せず復活時に明示リセットする構成へ修正した。
- 変更6 C#を再Import後、2回目Unity Compile成功。`Area Survivors/Validate/July 25 Gameplay Bug Fixes` fresh success marker確認、Console Error表示0件。
- Play Modeは開始していない。Scene／Prefab／HUDレイアウトは変更していない。
- ユーザー実機確認: 復活後の建物当たり判定／ダメージは正常になった。
- Blockerなし。

## Latest Completed Work (2026-07-28 Relic / Level-up Modal Priority)

- 原因は、`RelicAcquisitionPanel.Update`と`GameManager.UpdateLevelUpButtonHover`が同じフレームのController Submitと`EventSystem`選択をそれぞれ処理し、宝箱を閉じる入力を背面のレベルアップも再消費できたこと。
- 宝箱モーダル表示中は新しいXPレベルアップを待機列へ保持し、レベルアップパネルの選択、controller submit、Button actionを遮断するようにした。
- 宝箱を閉じたフレームもレベルアップ入力を遮断し、同じSubmit／Clickが背面へ伝播しないようにした。待機中のレベルアップは宝箱終了後に通常表示する。
- 既にポーズ中のレベルアップへ宝箱が重なった場合、宝箱終了時に`Time.timeScale`を1へ戻さず、元の0を維持するよう修正した。
- `RelicDropEligibilityValidator`へ、モーダル遮断、同一フレーム入力防止、ポーズ復元の静的契約を追加した。
- 変更3 C#を明示AssetImport後、Unity Compile成功。`Area Survivors/Validate/Relic Drop Eligibility` fresh success marker確認、Console Error 0件、対象`git diff --check`成功。
- Play Modeは開始していない。Scene／Prefab／HUDレイアウトは変更していない。
- ユーザー実機確認: 宝箱とレベルアップパネルの入力競合は解消した。
- Blockerなし。

## Latest Investigation (2026-07-28 First Clear vs Difficulty 1)

- 難易度1では`DifficultySpawnCount`が1倍、最大同時生存敵数も基礎値の1倍。敵HP、攻撃力、移動速度、spawn intervalにはクリア済み状態による補正がない。
- 初回クリア時は対象ステージのボス撃破で即座にSTAGE CLEARとなりランが終了する。
- 同じステージをクリア後に難易度1で再プレイすると、ボス撃破後に次ステージへ移行する。以後、次の未クリアステージまたはステージ4まで継続するため、同じ敵性能でも勝利条件が長くなる。
- 初回クリアで難易度2は解放されるが、保存中の選択難易度`difficulty`は自動で2へ変更されない。ロビー表示とEnemySpawnerは同じ`GetStageDifficulty`を参照する。
- 難易度2を選んだ場合のみ、通常spawn batch、エリートtimed spawn count、最大同時生存敵数が2倍になる。HP、攻撃力、移動速度は難易度では変化しない。
- TODO: ユーザーの敗北がボス前か、ボス撃破後の次ステージかを確認する。新ログの`startStageDifficulty`と`difficultyCheckpoints`で実選択難易度と敗北区間を確定できる。
- Blockerなし。

## Latest Completed Work (2026-07-28 Difficulty Tuning Log)

- 既存の「1ラン＝JSONL 1行」を維持したまま、`TokenRunLogEntry`をschemaVersion 3へ拡張した。
- `difficultyCheckpoints`へ、stage_start／boss_spawn／boss_clear／run_end時点のステージ、難易度、時間、レベル、XP、キル、被ダメージ、建造物損耗、敵数、ボスHP／戦闘時間を記録する。
- 最終ログへ、基礎XP／倍率適用後XP、レベルアップ履歴、取得強化履歴、敵種別ごとの出現／撃破数・HP・攻撃力・XP、建造物種別ごとのHP／被ダメージ／破壊数、プレイヤー被ダメージ／回復／死亡／最終能力、peak alive enemies、既存武器・建造物別damage reportを追加した。
- ボス撃破履歴へ、ボス最大HP、戦闘時間、ボス戦中のプレイヤー／建造物被ダメージを追加した。
- `Health.LastDamageDealt`で残HPを超えたoverkillを除いた実被ダメージを集計し、`BuildingRevivalState.IsDestroyed`で復活後を含む現在破壊状態を記録する。
- `TokenRuntimeServiceValidator`へschema、JSON field、XP履歴、取得強化履歴、敵spawn／kill、checkpoint、実被ダメージの配線検証を追加した。
- 初回Compileは外部編集C#をImportしないverification-only入口を使ったため120秒timeout。capture `C:\Users\yni87\AppData\Local\Temp\safe-command-81fc16ad92984a42a56d6a13cdcd6d86.txt`、exit 124、timed_out true。原因確定後、変更8 scriptを`RegisterAndRun`で明示Importした。
- 2回目のUnity Compile成功。Runtime／Editor Assembly current、`Area Survivors/Validate/Token Runtime Service` fresh success marker確認、Console Errorは`logs: []`／`displayedCount: 0`。
- Play Modeは開始していない。
- TODO: ユーザーがビルド後に全ステージを通しプレイし、新しい`logs/token_run_log.jsonl`を共有する。受領後にステージ別XP効率、ボス戦、被ダメージ、建造物損耗、敵処理率、武器DPSを集計する。
- Blockerなし。

## Latest Completed Work (2026-07-27 Fixed Boss HP)

- 通常の時間指定ボス生成`SpawnOne`を、召喚生成と同じ`EnemyHp(definition)`へ統一した。
- `EnemySpawner.CalculateEnemyHp`を共通の純粋計算入口とし、一般敵とボスの両方で難易度によるHP倍率を適用しない固定仕様にした。
- 全難易度のボスHPを、オークキング1,120、ゴブリンロード4,480、リッチ8,960、ドラゴン17,920へ設定した。
- HP14あたりXP1を維持し、リッチXPを640、ドラゴンXPを1,280へ変更した。
- `July25GameplayBugFixValidator`へ、全4ボスの固定HP、全敵の固定HP計算、HP/14 XP、`SpawnOne`の共通計算使用を追加した。
- 固定仕様への訂正2件を個別AssetImportし、Unity Compile 1回成功。
- `Area Survivors/Validate/July 25 Gameplay Bug Fixes`がfresh success markerを生成して成功。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- Play Modeは開始していない。
- TODO: ユーザーが実機で全難易度のボスHPが同じであることを確認する。
- Blockerなし。

## Constraints

- 既存の未コミット変更を戻さない。
- Externalの`*Source.png`原本は参照0だけで削除しない。
- Scene/Prefab/assetはUnity Reporterを正とし、Play Modeは開始しない。
- 削除はGUID参照、コード名参照、AssetDatabase依存、既存差分を確認できた対象に限定する。

## Current Status

- ユーザーがTilePalette修復後のステージ1を実機確認し、地面・塗り・建造物タイルを含め違和感なしと確認済み。
- `project-cleanliness-report.ps1`を追加し、missing meta、orphan meta、未解決GUID、重複ハッシュ、旧版フォルダ、TODO/FIXME/HACKを全件監査できるようにした。
- `AssetReferenceReporter`を各ルート上位24件からAreaSurvivors全626候補の走査へ拡張し、570 serialized/code reference files、全Prefab Missing Script、AssetDatabase外部依存を確認できるようにした。
- `game-manager-responsibility-report.ps1`の旧`Scripts/Game/GameManager.cs`固定パスを廃止し、現行`Scripts/Game/Runtime/GameManager.cs`を一意解決するよう修正した。
- 不要物として以下を削除した。
  - OpeningStoryの`Archive`、`PreviousHighDetail`、`PreviousComical`（旧画像15枚とmeta、約14.5MB）。
  - 未追跡のArcher足修正用中間画像2枚とmeta（約0.2MB）。
  - `Spine/PlayerExperimental`と`ThirdParty/Spine`一式。外部依存は実験フォルダ内2件だけで、本番Scripts/Scenes/Prefabsから参照0（約2.1MB、約579ファイル）。
  - 参照0・marker不在の旧`MissingScriptCleaner.cs`とmeta。
  - ThirdParty削除後に不要になった`.graphifyignore`。
- TilePaletteは05_Gameから依存されていたため削除していない。旧GUID欠損5件を`Repair Tile Palette References`で再生成・修復し、未解決GUIDを0件にした。WatchTower tileも再生成対象へ水平展開した。
- 削除・整理後のディスク監査はpayload 953、meta 1046、missing meta 0、orphan meta 0、未解決GUID 0、旧版フォルダ0、TODO/FIXME/HACK 0。
- 最終Asset Reference Reportは候補605件、review-candidate 0、archive-review-candidate 0、Unresolved serialized GUID 0、全Prefab Missing Script 0。
- 05_Gameは864 objects、Missing Script 0。全44 PrefabもMissing Script 0。
- `ProjectileImpact.prefab`のnull `PaperMeshVisual.sprite` 1件は、`ProjectileImpactFlash.Play(Sprite, ...)`で発生時に設定する仕様のため意図通り。
- GeneratedSpriteCatalogは566 entries、null sprite 0、duplicate name 0。Legacy `Resources/Generated` folderは存在しない。
- Build Settingsは本番9 Sceneが有効で、`90_GameplayTest`はBuild対象外。
- 分割前のGameManager基準値は3,389行・method-like entry 220件。
- 完全一致重複は33 group／約30.6MBだが、全件を用途別に確認済み。External原本とGenerated版、地面variantの意味別名と出現比率、stand/walk中央フレーム、用途別SE、Importerが異なる地面chunkとして保持し、未分類のreview重複は0件。
- Editor配下のMigration 16本は`migration-inventory-report.ps1`でMenu入口、外部参照、対Validator、生成対象を棚卸しした。全件が現行Scene/Prefab/Configの再構築または検証経路を持ち、削除可能な旧一回限り処理は0件。
- `project-cleanliness-report.ps1`へGUID・timeCreatedを除いたImporter設定比較と、意図的な意味別重複の分類を追加した。地面variantは`TileGrid`がSprite名でpath/dirt/grassへ分類し、同一画像の別名数も出現比率へ影響するため統合しない。
- GameManager責務分割の第一段階として、同一ファイルに同居していた`GameHudController`を専用ファイルへ物理分離した。クラス内容、Scene参照、`GameManager.gameHud`の型と初期化経路は変更していない。
- RuntimeのProfilerRecorder、Scene遷移前オブジェクト／Material集計を`RuntimeResourceDiagnostics`へ抽出した。GameManagerは開始、破棄、Snapshot要求だけを担当する。
- 旧`ResourceRuntimeService`からWood／Stone所持・加算・消費・永続化を削除し、トークン会計専用の`TokenRuntimeService`へ改名した。
- 前回は建造在庫、手動配置、旧セーブ互換ID、木・岩の配置／撤去を誤って維持した。ユーザー訂正により、これらは完全削除対象へ変更した。
- 現行固定建造物は、旧`BuildPlacementController`の保存／復元流用を廃止し、`FixedBuildingLayoutService`によるスキル解放連動の直接自動配置へ移行完了。
- ボス撃破時の建造物復活は、旧保存データ依存を外した`BuildingRevivalState`へ移行完了。壁復活時のプレイヤー衝突回避処理は維持した。
- 参照0を確認した`WoodIcon`、`StoneIcon`、`StatResource`の画像・meta・GeneratedSpriteCatalog登録と`HarvestResourcePopup`を削除した。
- `LegacyFeatureRemovalValidator`を追加し、旧型／API／設定／画像／Catalog登録／HUD名／旧serialized fieldの不在と新サービスの存在を固定した。
- GameManagerファイルは3,389行・method-like 220件から2,157行・136件へ縮小した（1,232行、84件削減）。
- `game-manager-responsibility-report.ps1`へ抽出済みComponentと残る分割候補を表示するよう更新した。
- Graphifyの空行出力バインドとSource欠落ノードの正常扱いを修正し、空行許可と`missing-source-path` fallback判定を自己テストへ追加した。
- `TemporaryLegacyConstructionRemovalRunner`によるScene移行は成功marker確認済み。`05_Game.unity`の`Game Manager`へ`FixedBuildingLayoutService`を1件配置し、`BuildPlacementController`、`NaturalLandmarkSpawner`、`Build Preview Tilemap`はScene/Prefabとも0件になった。
- 手動建造メニュー／配置／在庫／永続配置、木・岩の自然物生成・撤去、Wood/Stone経済に関するRuntime型、Editor経路、Save型、UpgradeType、Config field、GameplayTest field、画像、Sceneの旧serialized fieldを削除した。
- 固定建造物のスキル解放連動自動配置は`FixedBuildingLayoutService`、ボス撃破時復活と壁スタック回避は`BuildingRevivalState`へ分離して維持した。
- `LegacyFeatureRemovalValidator`へ、旧型／API／設定／画像／Scene object名の不在と新サービスの存在確認を集約した。
- `RefreshAfterRemoval`へAssembly-current事前ガードを追加し、`-BatchRefresh`はserialized asset専用、外部変更C#は`DependencyScriptPaths`で明示Importする規約へ修正した。

## Verification

- Unity Compile 5回すべて成功。最終Runtime/Editor assembly current。
- Console Error表示0件。
- `Area Survivors/Validate/HUD Layout Mutation Guard`成功marker確認。
- `command-tools-self-test.ps1`: 31 scripts parse/guard成功。
- 続行後のProject Cleanliness: missing meta 0、orphan meta 0、未解決GUID 0、review duplicate 0、historical group 0、code debt file 0。
- ユーザー実機確認: TilePalette修復後のステージ1をプレイし、違和感なし。
- GameManager分割: Unity Compile成功4回。AssetImport直後の重複Compile要求1回は`guard_code: 35`でUnity接続前に停止し、規定クールダウン後の検証は成功。
- `Area Survivors/Validate/Token Runtime Service`: fresh success marker確認。キル閾値、30秒報酬、10トークン攻撃段階、獲得元内訳、重複レリック内訳、次回報酬時刻を検証。
- `Area Survivors/Validate/Legacy Resource Removal`: fresh success marker確認。
- `Area Survivors/Validate/Stage Transition Enemy Defeat`: success marker確認。
- `Area Survivors/Validate/HUD Layout Mutation Guard`: fresh success marker確認。
- 旧資源削除後のUnity Compile成功。初回は未Import guard、次は削除漏れ5件のC#エラーをログで確定して限定修正し、最終RunnerでImport→Compile→Menu検証まで成功。
- 最終Console Error表示0件。
- `command-tools-self-test.ps1`: 31 scripts parse/guard成功。Graphify空行・Source欠落の再発防止を含む。
- 責務分割後のProject Cleanliness: payload 957、meta 1050、missing meta 0、orphan meta 0、未解決GUID 0、review duplicate 0、historical group 0、code debt file 0。
- 旧資源画像削除後のProject Cleanliness: payload 954、meta 1047、missing meta 0、orphan meta 0、未解決GUID 0、review duplicate 0、historical group 0、code debt file 0。
- Asset Reference Report: `TokenReports/UnityReports/asset-references-20260726-220222.md`。
- Compact Project Snapshot: `TokenReports/UnityReports/compact-project-snapshot-20260726-220344.md`。
- Scene/Prefab Overview: `TokenReports/UnityReports/scene-prefab-overview-20260726-220433.md`。
- Play Modeは開始していない。
- 完全削除移行のUnity Compile 1回目は`BuildingPersistentState`改名前参照8件で停止。原因をScene移行順序と確定した。
- 2回目は一時互換型の静的API不足を`July25GameplayBugFixValidator`が検出して停止。現行参照を`BuildingRevivalState`へ移行した。
- ユーザー許可後の3回目CompileとScene移行Menuは成功。marker `TokenReports/UnityMarkers/legacy-construction-scene-migration.success`を確認した。
- 完全削除後の`RefreshAfterRemoval`は、追加C#が未Importのため`guard_code: 41`でCompile前停止した。これを受けてcleanup前のAssembly-current guardを追加した。
- 続く`RegisterAndRun -BatchRefresh`はAssetRefreshが60秒timeout。Editor.logでは39 asset import完了、Runtime Assemblyはstale。後続の明示AssetImportは`Server is busy executing 'unknown'`で停止したため、それ以降Unityコマンドは実行していない。
- `command-tools-self-test.ps1`: 31 scripts成功。cleanup preflight、BatchRefresh/C# Import境界、focused-search出力上限を含む。
- Unity再起動後のCompile Errorは、`StatIconCatalog`の削除済み`Work` fallback 1件と、`AreaSurvivorsBootstrap.PrefabSet.watchTower`のfield追加漏れ1件。限定修正後、Runtime Assembly current、Editor Assembly current（Bee artifact hash一致）。
- `Area Survivors/Validate/Legacy Feature Removal`: fresh success marker `2026-07-27T00:29:44.2425020Z`確認。
- `Area Survivors/Validate/July 25 Gameplay Bug Fixes`: fresh success marker確認。壁復活時のcollision grace／recoveryを検証。
- `Area Survivors/Validate/Token Runtime Service`: fresh success marker確認。
- `Area Survivors/Validate/Stage Transition Enemy Defeat`: fresh success marker確認。
- `Area Survivors/Validate/HUD Layout Mutation Guard`: fresh success marker確認。
- 最終Console Error表示0件。
- 旧型／旧設定／旧画像／旧serialized field検索は`LegacyFeatureRemovalValidator`内の不在検証文字列だけが残り、Runtime／Scene／Prefab／asset実体は0件。
- 最終Project Cleanliness: payload 948、meta 1041、missing meta 0、orphan meta 0、未解決GUID 0、review duplicate 0、historical group 0、code debt file 0。
- `git diff --check`はUnity保存の新規Component空scalar`m_Name: `／`m_EditorClassIdentifier: `だけを既知例外として検出。Scene以外のtask対象はwhitespace error 0件。
- ユーザー実機確認: スキル解放時の固定建造物自動配置と、ボス撃破時の破壊建造物復活に問題なし。旧資源・手動建造機能の完全削除作業は完了。
- ユーザー要望: 体感や手動報告に依存せず、高負荷状態の自動検知と該当処理の修正・再計測を行える仕組みを希望。
- ユーザー要望: 通常の手動プレイテスト中も軽量監視を常駐させ、負荷Spikeの前後、ゲーム状態、関連counterを自動保存し、プレイ終了後に原因分析・再現Scenario化できるようにする。
- 既存基盤: `RuntimePerformanceProbe`はavg／p95／max frame ms、33/50/100ms超過、GC回数、managed memory差分、敵・演出・範囲・弾数を記録する。`CombatPerformanceDiagnostics`は攻撃範囲query、candidate、damage、paint、Excalibur shape、popup、hit flash、death、XP orbを記録する。
- 既存Scenario: Excalibur sustained／kill burst、Frost sustained、enemy crowdがあり、popup、hit flash、damage feedback、enemy controller/contact/paint/animation/YSort/collision、occlusion、physics multithreadingのA/Bモードを持つ。
- 直近保存済みBaseline: enemies 128、avg 27.43ms、p95 38.70ms、max 1140.31ms、33ms超66 frame、GC各85回。単発結果上書きで、履歴・複数回中央値・基準比較・自動合否は未実装。
- `RuntimePerformanceSentinel`を05_GameのGame ManagerへScene-authored Componentとして配置した。Editor／Development Buildだけで動作し、Release Buildでは無効化する。
- 通常プレイ中は固定長rolling bufferへframe/main thread、GC alloc／collection、used memory、敵数、area/projectile query/candidate、damage feedback、popup、hit flash、death、XP orb counterを保存する。
- 10秒warmup後、100ms超frame、p95の絶対／baseline比悪化、GC pressureを1秒間隔で評価する。非focus、pause、focus復帰直後は検知しない。
- 検知時は前5秒＋後10秒、stage/time、character、武器level、upgrade／relic、player位置、敵・弾・範囲・演出・orb数を`TokenReports/PerformanceSessions/<session>/incident-###.json`へ保存する。
- session単位の`session.json`／`session-summary.md`、最新sessionポインタを出力し、監視自体の通常時avg/max µsとincident書込時間も自己計測する。
- `performance-session-report.ps1`を追加し、最新または指定sessionをp95/max順に要約できるようにした。
- Runtime Performance Sentinelの明示Import→Unity Compile→Scene Setupが1回で成功。Runtime／Editor Assembly current、専用ValidatorとHUD Layout Mutation Guardのfresh marker、Console Error表示0件を確認した。
- `command-tools-self-test.ps1`: 32 scripts成功。session reporterのparameter contractとfixture自己テストを含む。
- task対象C#／PowerShell／ctxの`git diff --check`成功。改行コード変換警告のみでwhitespace error 0件。
- 初回確認時にユーザー側RTK直下の存在しない`safe-read.ps1`とWindows PATHにない`ls`を指定して失敗した。根本原因はWrapper実在確認の不足で、以後はプロジェクト内`Tools/TokenUsage`と存在確認済み`powershell.exe`をRTK経由で使用した。
- ユーザーがドラゴン討伐まで約21分プレイし、`InvalidOperationException: Collection was modified`が発生した。完全スタックは`GameClearRoutine`→`EnemySpawner.StopAndClearEnemies`で、`EnemyController.ActiveEnemies`を直接`foreach`中に`Destroy`が`OnDisable`経由で同registryから要素を削除したことを示した。
- `StopAndClearEnemies`はactive-enemy registryのsnapshotを先に作り、snapshotを列挙して破棄するよう修正した。ドラゴン以外の同メソッド呼び出しでも同じ安全性を持つ。
- `StageTransitionEnemyDefeatValidator`へ、最終敵cleanupがlive registryを直接列挙せずsnapshotを使用する静的契約を追加した。
- 修正後の明示Import→Unity Compile成功。Runtime／Editor Assembly current、`Area Survivors/Validate/Stage Transition Enemy Defeat` fresh marker、Console Error 0件、task対象`git diff --check`成功。
- 初回自動計測session `20260727-013143-632-024e9a`: 21分、incident 20件、baseline p95 38.63ms、Sentinel通常時平均7.52µs／最大1.75ms、最大書込33.93ms。Stage 3の最大incidentはp95 109.47ms／max 205.60ms／敵396体。
- Stage 4のincident #17〜#20をフレーム単位で解析した。終盤#20はp95 69.98ms（約14fps）、max 143.39ms（約7fps）、188 enemies、781 DamagePopup、125 EnemyHitFlash、101 XP orbだった。
- Stage 4区間の重複frameを除いた120秒では、遅い上位10%は軽い側50%に対し、main thread 4.87倍、敵数5.25倍、Excalibur系projectile candidate 10.44倍、damage feedback／hit flash 66.99倍、popup spawn 50.08倍、GC alloc 4.64倍だった。
- #20の最遅143.39ms frameは191 enemies、185 damage feedback、186 popup spawn、185 hit flash request、約1.87MB GC alloc。main thread 142.99msで、GPU待ちではなくCPU main-thread側のstallだった。
- `EnemyController.OnDamaged`は全hitごとにSFX要求、HitFlash、`DamagePopup.Show`を実行する。`DamagePopup.Show`は毎回PrefabをInstantiateし、各Popupは独自Materialを持つ`RuntimeTextMeshOutline.LateUpdate`と`DamagePopup.Update`を持つため、大量hit時に生成、Material更新、描画、GCを同時増幅する。
- Excaliburは0.05秒ごとに広域`OverlapCircleNonAlloc`し、#20では302 query／45,899 collider candidate（平均約152 candidate/query）。各candidateへComponent取得、最大3回のClosestPoint／sector判定を行い、大量hit feedbackの入力源にもなる。
- Bananaは9本まで増えておりTrigger callback経由でhitを追加し得るが、SentinelのFrameSampleに`projectileTriggerCallbacks`とweapon別hit数が未収集のため、ExcaliburとBananaの寄与分離は未確定。
- 一部slow frameはdamage feedback 0でも発生しており、敵AI／Physics／YSort／Collision／描画の残留負荷が副次要因。既存A/B modeでのStage 4再現計測が必要。
- Sentinelのringは「5秒」ではなく最大1200 frameを保持するため、低fps時のincidentが28.9〜51.6秒へ伸び、隣接incidentが重複していた。集計ではsessionSecondsで重複除去したが、保存上限20件の早期到達にも影響している。
- XP orb／token orbの通常接近時とボス討伐時の吸い込みが固定6 units/sだったため、高速化したPlayerへ追従できなかった。
- `PickupAttractionMotion`を追加し、吸い込み速度を毎frame `max(既存最低速度, Player.CurrentMoveSpeed + 2)`で共通計算するよう変更した。低速時は従来の6を維持し、高速時だけPlayerより2 units/s速くなる。
- ボス討伐時のtimeout見積もりは、停止中の距離／固定速度ではなく、Playerが反対方向へ動き続ける最悪条件の相対追従速度で計算する。
- `StageTransitionEnemyDefeatValidator`へ、XP／tokenの通常・ボス両経路がPlayer相対速度を使うこと、低速時の最低速度維持、高速Player 12に対して吸い込み14、相対速度による所要時間を検証する契約を追加した。
- 新規Script Import直後の明示Compileは既知の非同期compile重複guard `35`でUnity接続前停止。24秒cooldown後の同一Compileは成功し、Runtime／Editor Assembly current、Stage Transition Enemy Defeat fresh marker、Console Error 0件を確認した。
- ユーザー実機で、高移動速度時の通常接近吸い込みとボス討伐時の全体吸い込みがPlayerへ追従して吸収されることを確認済み。
- `DamagePopup`はper-hit Instantiate／Destroyを廃止し、Prefab別最大96個の再利用poolと1frame最大32表示のrate limitへ変更した。生成、再利用、drop、active数をSentinelで記録する。
- `RuntimeTextMeshOutline`は個別Material生成と常時`LateUpdate`を廃止し、Prefab保存済み共有Material＋`MaterialPropertyBlock`を表示更新時だけ適用する。
- `EnemyHitFlash`は被弾時のComponent／GameObject／Material生成を廃止し、Enemy Prefabへ保存した非表示Meshと共有Materialを再利用する。同一frameの重複hitは1回のvisual同期へ集約する。
- `AdvancedWeaponProjectile`のExcalibur走査は固定20Hzから武器の`damageIntervalSeconds`へ統合した。標準0.25秒なら20回/秒から4回/秒へ80%削減し、swept sectorで走査間に通過した範囲は維持する。
- Excalibur候補処理へCollider→Enemy／Enemy→Health cache、damage cooldown先行判定、Boundsによるradial／angular早期rejectを追加し、不要な`GetComponentInParent`と`ClosestPoint`を削減した。
- Sentinelのprebufferは最大1200 frameだけでなく`sessionSeconds`で実5秒へtrimする。低fps時に30〜50秒分を保持してincidentが重複する問題を解消した。
- Sentinelへprojectile trigger、weapon別projectile hit（Excalibur／Banana／other）、popup request/create/reuse/drop/active peak、hit flash coalesced/active peakを追加し、`performance-session-report.ps1`から確認できるようにした。
- `CombatFeedbackPerformanceMigration`でEnemy／DamagePopup Prefabと共有Materialを保存し、Bootstrap再生成経路にも同じ構成を反映した。
- 性能対応の明示Import→Unity Compileは2回成功。Migration、Combat Performance Probe Validator、Runtime Performance Sentinel Validator、Combat Visual Rotation Guardはいずれもfresh marker確認済み。Play Modeは開始していない。
- `command-tools-self-test.ps1`は34 scripts成功、`performance-session-report.ps1 -SelfTest`成功、task対象`git diff --check`は改行変換warningのみでwhitespace error 0件。
- Play Modeは開始していない。
- ユーザーが性能修正後session `20260727-051655-045-832e4b`でStage 4まで実機プレイした。ドラゴンは未討伐。Stage 3終盤で強いカクつきを確認した。
- 最新sessionは13分54秒、incident 20件、baseline p95 37.51ms、Sentinel通常時平均8.61µs。Stage 3 worst incident #18は実15秒、p95 362.69ms、max 424.42ms、peak enemies 445。
- Stage 3 boss時間は360.28秒で停止したまま、incident #14〜#18のactive enemiesが156→220→268→361→436へ増加し、active XP orbも114→58→235→698→1056へ増加した。
- リッチは5秒cooldown＋0.5秒cast＋0.15秒recoverごとにSkeleton 10体＋SkeletonKnight 10体を召喚する。通常spawnは`MaxAliveEnemiesForDifficulty`を守るが、`SpawnSummonedEnemy`は上限確認を通らず、召喚敵に寿命／専用上限もない。
- Stage 3 #18ではProjectile Triggerが46,422回/秒、Banana hitが156回/秒。最遅frameは最大35,880 callback、別frameでは71,040 callbackかつdamage hit 0だった。Banana等の長寿命Triggerが`OnTriggerStay2D`で全接触を毎Physics step処理している。
- Enemy／BananaProjectile／ExperienceOrbはすべてLayer 0。Projectile callbackは敵だけでなくXP orb、token、建造物等の不要Colliderにも発生し、敵damage cooldown中も`OnTriggerStay2D`とComponent解決が走る。
- Fixed Timestep 0.02秒、Maximum Allowed Timestep 0.333秒のため、低fpsになるほど1表示frame内のPhysics catch-up stepが増え、Trigger Stay増加→さらに低fpsとなるspiralが起きる。
- #18にはTrigger 0、damage feedback 0でも336ms、enemy 445のframeがあり、根底の負荷は密集したDynamic Rigidbody2D敵同士のPhysics／各Enemy Update。Banana Triggerはその上へ重なる増幅要因。
- Stage 4移行直後incident #19はStage 3 prebufferのpeak enemies 550を含みmax 566msだが、cleanup後はactive enemies 5。Stage 4 #20はpeak enemies 94、active XP 62、p95 34.27msまで回復しており、Stage 3の無制限蓄積が主因であることを裏付ける。
- Popup poolはStage 3 #12〜#18で新規生成0、#18はpopup reuse 962／drop 2024。修正前Stage 3 worstに対しPopup表示64.3回/秒、GC 13.91MB/秒まで低下しており、今回の424ms stallの主因ではない。
- `performance-stage-detail-report.ps1`を追加し、最新sessionのstage指定incident、実時間rate、capture時object数、重複除去top frameを単一入口で出力するようにした。fixture自己テストと全35 scriptの`command-tools-self-test`成功。
- `AttractablePickup`と`PickupAttractionRegistry`を追加し、XPオーブ／トークンの値、通常回収、ステージ遷移予約、速度解決、移動処理を共通化した。
- 待機中Pickupは共通registryへ登録し、`PlayerController`が0.1秒間隔で前回位置から現在位置までの移動線分に近いPickupだけを吸引開始する。高速移動で吸引範囲を通過しても取りこぼさない。
- XPオーブ／トークン個別の`Update`と毎frame `Vector2.Distance`を削除した。吸引開始後はPlayerが保持する単一リストだけを毎frame移動し、待機中PickupにはUpdate dispatchが発生しない。
- Player管理側が到達時に直接回収するためPickupのPhysics triggerを廃止した。`ExperienceOrb.prefab`のCollider2DをMigrationで除去し、`TokenOrb.Spawn`もColliderを生成しない。大量PickupがProjectile／EnemyとのTrigger pairを作らない。
- ボス討伐後の全回収は`FindObjectsOfType<ExperienceOrb/TokenOrb>`を廃止し、共通registryのsnapshotから全Pickupを同じPlayer管理リストへ登録する。報酬予約、unscaled time演出、timeout後の強制完了、XP／tokenのまとめ付与は維持した。
- Sentinelへ`pickupProximityScans`、`pickupScanCandidates`、`pickupAttractionsStarted`、`pickupMovementTicks`を追加し、`performance-session-report.ps1`からincident単位で確認できるようにした。
- Edit Mode Validatorの`AddComponent`では通常MonoBehaviourのPlay時`OnEnable`登録が走らず、初回プローブが`registered=0`となった。Editor条件付きの明示登録／解除入口と`finally` cleanupを追加し、Runtimeと同じregistry走査を検証するよう修正した。
- Pickup吸引移行後のUnity Compileは上限5回すべて成功。最終Migration後も`Stage Transition Enemy Defeat`と`Runtime Performance Sentinel`のfresh marker、ExperienceOrb PrefabのCollider 0件、Console Error表示0件を確認した。
- `performance-session-report.ps1 -SelfTest`と全35 scriptの`command-tools-self-test`が成功した。Play Modeは開始していない。
- 読み取り時に`safe-read -PrintOutput`の80行ガードを複数回踏んだため、以後のコード読取を`safe-read-batch`へ統一した。Obsidian `Knowledge/safe-read-output-guard.md`と`Knowledge/area-survivors-execute-always-ui-state.md`へ原因・入口ルールを追記した。
- `EnemySpawner.RemainingAliveEnemyCapacity`を追加し、通常spawnとリッチ召喚が同じ`maxAliveEnemies × stage difficulty`上限を使用するよう統一した。`SpawnSummonedEnemy`自体も上限0なら生成しないため、呼び出し側を迂回しても無制限増加しない。
- リッチは召喚要求20体を残枠へ按分し、Skeleton／SkeletonKnightの比率を保ちながら残枠分だけ生成する。上限到達時は召喚Visualを維持しつつ敵生成を0件にする。
- Bananaは0.25秒間隔のNonAllocカプセル走査へ移行した。前回走査位置から現在位置までを覆うため、低fps／高速移動でも通過した敵を取りこぼさない。
- BananaのPrefab Collider形状から攻撃半径を取得した直後にColliderを無効化し、`OnTriggerEnter2D`／`OnTriggerStay2D`はBananaを明示除外した。Bananaは敵・建造物・他ProjectileとのPhysics trigger pairを作らない。
- Sentinelへ`bananaOverlapQueries`／`bananaColliderCandidates`／`summonedEnemySpawnAttempts`／`summonedEnemySpawns`／`summonedEnemyCapBlocked`を追加し、session reportとstage detail reportへ出力した。
- Unity Compileは3回成功。`Combat Performance Probe`、`Banana Evolution`、`Runtime Performance Sentinel`のfresh marker、Console Error表示0件を確認した。Play Modeは開始していない。
- Banana Validatorへfresh markerを追加した。現行進化条件は「武器Lv.10＋ゲームプレイ中300撃破」で、旧「武器Lv.10のみ」固定値を現行`WeaponCatalog`へ同期した。
- `performance-session-report.ps1 -SelfTest`、`performance-stage-detail-report.ps1 -SelfTest`、全35 scriptの`command-tools-self-test`が成功した。
- ユーザー実機session `20260727-063304-121-3713fe`（14分16秒、難易度5）を修正前 `20260727-051655-045-832e4b` と比較した。
- Stage 3の同等敵数比較（修正前peak 445／修正後peak 440）では、p95 362.69→159.09ms（56.13%改善）、max 424.42→178.93ms（57.84%改善）、Projectile Trigger 46,422回/秒→0、projectile candidate 1,131→872回/秒（22.94%削減）だった。Banana／PickupのPhysics pair除去は有効。
- 一方、修正後Stage 3は敵775体まで増え、最悪p95 379.09ms、max 502.31msとなった。修正前最悪値比でp95 4.52%悪化、max 18.35%悪化、敵peak 74.16%増加。
- 現在のalive上限は`GameConfig.maxAliveEnemies=160 × ProgressionStore.GetStageDifficulty`。実機saveのStage 3難易度は5のため上限800体となり、775体時点でも召喚attempt／spawnは同数、cap-blocked 0だった。召喚上限処理は動作しているが、性能上限として800体は高すぎる。
- 修正後Stage 3最遅frameは700体超、Projectile Trigger 0、damage 0～少数でも411～502msで、残る主因は大量Dynamic Rigidbody2D敵のPhysics／Enemy Update。Banana走査そのものではない。
- Stage 4通常区間はp95 34.27→35.41ms（3.34%悪化）、max 60.81→59.72ms（1.79%改善）で概ね同等。Projectile Triggerは18,105回/秒→0で、低～中敵数では機能回帰を伴う性能悪化は見られない。
- Sentinel通常監視は平均8.61→9.11µsで、引き続き通常プレイへの影響は極小。
- 前回の「Banana／PickupのPhysics pair除去が効いた」は両変更を含む合算結果で、Banana単独の寄与には分離できない。Pickupは個別Update、距離計算、Colliderを廃止しており、同敵数での改善にはPickup移行も寄与している。
- Enemy PrefabはLayer 0、Dynamic Rigidbody2D、非Trigger BoxCollider2Dで、全layerとのcallback送受信が有効。密集時は敵同士のPhysics contact／solver pairと`OnCollisionStay2D`が増える。callback内はenemy同士でもHealth取得後、Barrier／Ballista／WatchTower／Player／Towerの`GetComponentInParent`を行ってからreturnする。
- 敵1体ごとに少なくとも`EnemyController.Update`、`EnemyController.LateUpdate`、`EnemyBounceAnimation.LateUpdate`、`RuntimeSpriteOutline.LateUpdate`、`CharacterOcclusionReveal.LateUpdate`が毎render frame dispatchされ、`KnockbackReceiver.FixedUpdate`が毎physics step dispatchされる。775体なら最低3,875 managed frame callback／render frameに加え、固定step 0.02秒で38,750 FixedUpdate callback／秒となる。
- `EnemyController.Update`は全敵でtarget方向、Player aggro距離、Grid object contact、territory slow、weapon slow、Rigidbody velocity、animation schedule、territory paint scheduleを処理する。
- 通常敵のterritory paintは0.2秒／最低6 frame間隔で、radius 1の複数cellを更新する。`TileGrid.UpdatePaintTransitions`自体も毎frame全grid cellを走査し、敵数増加でdirty cellのVisual更新が増える。
- `EnemyController.LateUpdate`は全敵でYSort scheduleを確認し、通常敵は概ね4 frame間隔でRenderer列挙、Component確認、sortingOrder更新を行う。
- `EnemyBounceAnimation`は全敵で毎frame SmoothStep計算とVisual TransformのScale／Position書き換えを行う。
- `RuntimeSpriteOutline`は全敵で毎framesource Mesh／Material／Texture／Color／sorting stateを比較する。敵ごとに専用Materialと追加Outline Rendererを持ち、YSort／animation変更時は同期書き込みが走るため、CPU、Renderer、draw-callの全てが敵数比例する。
- `CharacterOcclusionReveal`は全敵で毎frameresource／timer／transform状態を確認する。通常敵の実Occlusion判定は24件／frameへ制限済みだが、0.18秒ごとに`FindObjectsOfType<Renderer>()`で全Rendererを再走査し、各敵がMaterial／CommandBufferを保持する。
- Pickup移行後はidle orbにUpdate／Colliderはない。Playerが0.1秒ごとに全idle PickupをHashSetからListへコピーして線分距離判定するためO(Pickup数)、吸引中はPlayerの1 Updateから全吸引対象をMoveTowardsするためO(吸引中数)。各orbのGameObject／Transform／Rendererは残る。
- 最新Stage 3 #18ではPickup scan 48,924候補／15.18秒、active XP 984。以前の1,000個別Update／Collider pairより軽いが、全件scanと約1,000 Rendererは次の集約候補。
- 攻撃範囲query、Popup、HitFlashは敵数・命中数に比例するが、最新最遅frameは700体超、Projectile Trigger 0、damage 0～少数でも411～502msだったため、現在の第一原因ではなくPhysics／Enemy Update群へ追加される増幅要因。
- `OnCollisionStay2D`冒頭で衝突相手を除外する早期returnは有効。現在は相手分類前に`contactTimer -= Time.deltaTime`を実行し、敵同士でもHealth取得後にBarrier／Ballista／WatchTower／Player／Towerを順次検索するため、敵判定を先頭へ移すだけでもmanaged component lookupを削減できる。
- 現在のTagManagerはcustom tag／layerが空で、Enemy PrefabはDefault Layer。短期対応は相手Collider直下の`EnemyController`を1回確認してreturn、根本対応はEnemy専用Layerとcontact-damage対象LayerMaskを追加し、Enemy×EnemyをPhysics2D Collision Matrixで無効化する。
- 冒頭returnだけではbroadphase／contact manifold／solver／Rigidbodyの押し合い／managed callback dispatchは残る。Enemy×EnemyのLayer衝突無効化がPhysics負荷削減の本体。
- `contactTimer`をcollision callbackごとに減算する現仕様は、無関係な敵同士の接触数でcooldownが早く進む。`nextContactDamageAt`絶対時刻へ変更するか、timer減算を1体1回のUpdateへ移し、許可対象との接触時だけ判定する必要がある。
- Enemy専用Layerの追加だけでは衝突挙動は変わらない。Collision MatrixのEnemy×EnemyをOFFにした場合だけ、敵同士は押し合わず重なれる。これはゲーム挙動変更なので、ユーザーの明示了承なしに実施しない。
- 敵同士の物理衝突を維持する場合は、Enemy×Enemy collisionをONのまま、Enemy Layerで`OnCollisionStay2D`を先頭returnする。可能ならCollider2Dのcallback対象LayerをPlayer／建造物だけへ絞り、enemy同士のsolver／押し合いは維持しながらmanaged collision callbackを抑止する。
- ユーザー判断: Enemy専用Layerへの分離は許可するが、敵同士の物理衝突／押し合いは維持する。
- `EnemyController`へEnemy Layer定数とcacheを追加し、`OnCollisionStay2D`は相手ColliderがEnemy LayerならHealth等のComponent検索前に即returnする。
- 建造物セル接触とPhysics接触で共有していた`contactTimer -= Time.deltaTime`を廃止し、`nextContactDamageAt = Time.time + 0.75f`の絶対時刻方式へ統一した。敵同士や複数接触数でcooldownが早く進まない。
- `EnemyCollisionLayerMigration`を追加し、TagManagerのuser layer 8へ`Enemy`を登録、Enemy Prefab全階層4 GameObjectをLayer 8へ移行した。
- Physics2D Layer Collision Matrixは全組み合わせ有効のまま。専用Validatorで`Physics2D.GetIgnoreLayerCollision(enemyLayer, enemyLayer)==false`、Dynamic Rigidbody2D、非Trigger Collider、Prefab全階層Layer一致を確認する。
- `CombatPerformanceProbeValidator`へEnemy Layer、Enemy×Enemy collision維持、absolute contact cooldown、早期return、旧contactTimer不在、Migration契約を追加した。
- 新規Editor ScriptのAssetImport直後Compileは非同期compile重複guard `35`でUnity接続前停止。20秒cooldown後の2回目Compileは成功し、Runtime／Editor Assembly current。
- `Area Survivors/Migrate/Enemy Collision Layer`はfresh `combat-performance-probe-validator.ok` markerを生成し成功。最終Console Error表示0件。Play Modeは開始していない。
- 最終`scoped-diff-check`は、Unity再保存Prefabの空欄フィールド`m_Name: `／`m_EditorClassIdentifier: `を末尾空白として検出して停止した。専用ValidatorでPrefab全階層Layer、Dynamic Rigidbody2D、非Trigger Collider、Enemy×Enemy collision有効を確認済みのため、Unity YAMLを手整形せず既知例外として扱う。
- ボス後の全Pickup吸引は、予約値を全Pickup終了後に合算付与する方式を廃止し、各XP Orb／Token Orbがプレイヤーへ到着した時点で予約値を即時付与する方式へ変更した。timeout時だけ残存Pickupをプレイヤー位置へ移して同じ到着処理を完了する。
- 通常XPで複数レベル上昇した場合は`pendingRunLevelUps`へ上昇回数を蓄積し、表示中パネルの選択完了後に残数ぶん次のパネルを順次表示する。例としてLv.18→22なら強化選択は4回行う。
- `StageTransitionEnemyDefeatValidator`へ、Pickup到着前付与、遷移後のXP／Token合算経路不在、複数レベル選択キュー、SceneのlevelUpPanel参照を追加した。
- 初回Compile検証は外部編集C#未Importの`guard_code: 41`でUnity接続前停止。3本を明示ImportしたCompileは成功した。初回ValidatorはAddExperienceの境界が後方Helperまで含んで誤検出したため、直後の`QueueRunLevelUps`宣言を終端へ修正し、再Import／Compile後にfresh marker成功。HUD Layout Mutation Guardもfresh marker成功、最終Console Error表示0件。Play Modeは開始していない。
- Enemy Layer早期return前セッション`20260727-063304-121-3713fe`と修正後`20260727-074507-915-1c2c70`を比較した。Stage 2の近似敵数では254体p95 57.87ms→253体55.40ms（4.3%改善）、321体82.04ms→308体65.04ms（20.7%改善）。Stage 3高密度帯では440体159.09ms→517体108.41ms（31.9%改善）、661体379.09ms→628体149.35ms（60.6%改善）、775体363.62ms／max 502.31ms→800体worst p95 248.39ms／max 282.96ms（31.7%／43.7%改善）。
- 修正後も800体ではp95 248.39ms、max 282.96msで実用上重い。最遅帯はProjectile Trigger 0、damage 0のframeでも249～260msのため、攻撃命中よりEnemy Physics solver／個別Update／Visual群が第一原因。active XP Orbは最大2,345、GCは最大29.6MB/sで二次的な増幅要因。
- 修正前後で武器Lv、Projectile候補数、Pickup数が完全一致しないため、上記をEnemy Layer早期return単独の厳密な効果率とは扱わない。監視overheadは平均9.11us→8.61us、最大incident write 36.16ms→35.14msでp95悪化の主因ではない。
- 固定負荷GameplayTestとして`Gameplay_Enemy_Load_200/400/800.asset`を追加した。全Scenarioはseed `20260727`、敵数の10%を同一点へ密集、残りを同一規則で配置し、1秒warmup＋6秒計測＋0.5秒遷移で12モードを自動実行する。Baselineを先頭・末尾で再計測し、途中はcontact check、move multiplier、paint、animation、YSort、occlusion、outline、Enemy同士collision、Physics multithreading、EnemyController全停止を個別A/Bする。
- `RuntimePerformanceProbeMatrix`は各モード前にEnemy Transform／Rigidbody位置・速度を初期状態へ戻し、`Library/AreaSafeUnity/combat-performance-matrix-last.txt`とtimestamp付きarchiveへ結果を保存する。Enemy collision OFFはO(N^2)のCollider pair列挙を廃止し、Enemy専用Layerのcollision設定を一時変更して終了時に元設定へ復元する。
- `combat-performance-probe.ps1`へ`PrepareEnemyLoad200Matrix`、`PrepareEnemyLoad400Matrix`、`PrepareEnemyLoad800Matrix`、`LastMatrixResult`を追加した。C#6本の明示Import／Compileは1回で成功し、Scenario生成marker、Combat Performance Probe Validator marker、Console Error表示0件を確認した。Play Modeによる実測は未実施。
- 200体Matrixを実測した。archiveは`Library/AreaSafeUnity/combat-performance-matrix-20260727-084751-Gameplay_Enemy_Load_200.txt`。Baseline p95は19.12ms→17.67ms（-7.6%、stable）。平均Baseline 18.40ms基準でEnemyController全停止9.38ms（49.0%改善）、Outline停止13.06ms（29.0%）、Enemy同士collision停止13.66ms（25.7%）が上位。その他の個別停止は9.6～17.7%だった。
- 800体Matrixを実測した。archiveは`Library/AreaSafeUnity/combat-performance-matrix-20260727-085212-Gameplay_Enemy_Load_800.txt`。先頭Baseline p95 109.72ms／max 316.17ms、末尾Baseline p95 173.86ms／max 244.70msで、p95 driftは+58.5%（high-drift）。Enemy同士collision停止はp95 46.13ms、EnemyController全停止51.26ms、Outline停止80.88msまで低下した。Physics multithreadingは109.61msで先頭Baselineと実質同じ。物理solverが最大、Outlineが第二の拡大要因と判断できるが、高driftのため他の数%～20%差は厳密比較に使わない。
- `performance-matrix-report.ps1`を追加し、最新archiveの先頭／末尾Baseline drift判定とA/B順位を自動出力するようにした。`command-tools-self-test.ps1`は36本parse成功。
- 800体準備後の選択状態確認で引用符付きinline Evalが既存`guard_code: 25`によりUnity接続前停止した。ガード指定どおり同じEvalを再試行せず、永続`GameplayTestSelectionReporter`を追加した。Reporter markerで`Gameplay_Enemy_Load_800.asset`と`90_GameplayTest.unity`を確認し、2回目Compileは成功した。
- 今回のPlay Modeは200体・800体の2回を使用して終了した。400体は上限に従い未実測。
- 追加許可後に400体Matrixを実測した。archiveは`Library/AreaSafeUnity/combat-performance-matrix-20260727-090049-Gameplay_Enemy_Load_400.txt`。Baseline p95は33.32ms→33.84ms（+1.6%、stable）。EnemyController全停止16.96ms（49.5%改善）、Enemy同士collision停止21.41ms（36.2%）、Outline停止22.38ms（33.4%）。400体は敵だけで約30fpsの境界となり、武器・Pickupを含む本番上限には不適切と確定した。
- 固定200／400／800の結果を基に、`EnemySpawner.PerformanceSafeAbsoluteMaxAliveEnemies = 200`を追加した。難易度1は従来どおり160体、難易度2～5は最大200体。通常spawn、リッチ召喚、時限エリートは共通capを通し、ボスだけは出現保証のため上限時に最大201体を許容する。敵同士の物理衝突と押し合いは変更していない。
- `RuntimePerformanceSentinel`のsession／incident JSONとMarkdownへ`stageDifficulty`と実際の`maxAliveEnemies`を追加した。次回実プレイでは推測せずcap適用状態をレポート単体で確認できる。
- alive cap実装はC#3本の明示Import／Compile成功後、時限spawn水平対応を含むC#2本の再Import／Compileも成功。Combat Performance Probe Validator fresh marker成功、Console Error表示0件。
- ユーザー実機session `20260727-090857-879-b13c45`（19分16秒、難易度5）で、体感がかなり改善したことを確認した。session metadataは`stageDifficulty=5`、`maxAliveEnemies=200`で、Stage 1～3のincident上の敵peakは最大200だった。
- 敵上限前session `20260727-074507-915-1c2c70`との最悪値比較では、Stage 1 p95 73.13→54.75ms（25.1%改善）／max 137.30→65.80ms（52.1%改善）、Stage 2 p95 74.50→66.79ms（10.3%改善）／max 167.90→93.13ms（44.5%改善）、Stage 3 p95 248.39→68.83ms（72.3%改善）／max 282.96→146.98ms（48.1%改善）だった。
- Stage 3のリッチ召喚では、記録されたincident内でspawn 114件に対してcap-blocked 106件となり、200体到達後の超過召喚が遮断された。従来の800体まで増える状態は解消した。
- 最新sessionはStage 3終了時点でincident上限20件に到達したため、Stage 4以降の定量記録はない。Stage 3の200体帯でも最悪p95 68.83msが残り、GC pressure incidentは16～23MB/s程度、Damage Feedback／HitFlash／Popupが重なる区間は次の改善候補。
- ユーザー実機で、Pickup到着時のXP獲得と、複数レベル上昇時に上昇回数ぶんレベルアップ選択が順番に表示されることを確認済み。
- エンドロールが毎回表示される原因は、`GameOverScreen`が`RunResult.allStagesDifficultyFiveCleared`だけを見ており、表示済み状態を永続化していなかったため。
- `SaveData.endingCreditsViewed`、`ProgressionStore.HasViewedEndingCredits`、`TryMarkEndingCreditsViewed()`を追加した。全ステージ難易度5・Stage 4クリア・未表示・Scene参照ありの場合だけ、再生開始前に表示済みを保存してエンドロールを開始する。
- 旧セーブはJSONに新fieldが存在するかを非永続markerで判定する。新fieldなしで既に全Stage難易度5クリア済みなら、従来版で表示済みだった履歴として自動移行し、更新後の再クリアで再表示しない。未達成の旧セーブと新規セーブは初回達成時だけ表示する。
- `ResetStageClearStateForTesting()`はエンドロール表示済み状態も解除し、テスト用の初回条件を再現できる。
- 初回Compile要求は外部編集C#未Importの`guard_code: 41`でUnity接続前停止。変更5本を`RegisterAndRun`で明示Import後、Compile成功、Ending Credits Validator fresh marker成功、HUD Layout Mutation Guard fresh marker成功、Console Error表示0件、対象5ファイルの`git diff --check`成功。Play Modeは開始していない。
- Graphify Pilotの実運用再集計後、`Affected`へ既定20件／推定500 tokenの会話表示上限を追加した。全件は`full_capture_path`へ保持し、上限超過はverification対象としてDepth 1再絞り込みまたはfallbackを提示する。
- Graphifyが提示する`focused-search`へ`GraphifyFallbackId`を渡し、実際にfallbackが完了した場合だけ`TokenReports/graphify-pilot-usage.jsonl`へ`action: Fallback`を追記する。これにより推奨回数と実利用回数を分離集計できる。
- 評価実行では`ProgressionStore` Affectedが126件／推定2,980 tokenに対し表示20件へ制限され、full capture保存、共通fallback IDによるAffected→Fallback相関を確認した。`command-tools-self-test.ps1`は36 script成功。Unity Compile／Play Modeは実行していない。
- `EnsureFresh`は実際にGraphifyを使う直前の1回だけとし、grep/readだけの作業や同じfresh graphを続けて読む間は再実行しないルールへ更新した。

## Closeout Snapshot (2026-07-27)

- ユーザー実機確認済み: 整理後のStage 1表示、Stage 4到達までの性能改善、XP到着時取得、複数レベルアップ選択、セーブ単位のエンドロール一度限り表示。
- 直近のUnity検証: Compile成功、Ending Credits Validator成功、HUD Layout Mutation Guard成功、Console Error 0件。
- プロジェクト清掃: Missing meta 0、Orphan meta 0、未解決GUID 0、レビュー対象重複0、旧版フォルダ0、TODO/FIXME/HACK 0。
- 性能計測: 難易度5、敵上限200。Stage 3 p95は248.39msから68.83msへ72.3%改善。Incident上限20件によりStage 4定量記録は未取得。
- 廃止済みWood/Stone資源、手動建造、自然木・岩、旧保存・UI・アセットは完全削除済み。現行建造物は`FixedBuildingLayoutService`、復活は`BuildingRevivalState`、トークンは`TokenRuntimeService`が担当する。
- 締め作業中、`safe-read.ps1`をユーザーディレクトリ配下と誤認した呼び出しが終了コード1で停止した。正式入口`Tools/TokenUsage/safe-read.ps1`と既定Windows PowerShellの固定経路を確認し、限定自己テスト成功後に再開した。既存のCommand Failure Playbookに同一原因と防止策が記録済み。
- Blockerなし。

## 2026-07-27 継続作業開始時のCommand Failure

- Stage 4性能Incident改善の詳細ルールを読む前に、未確認の`rtk wc -l Docs/AgentRules/core-files.md`を実行し、Windows PATH上に`wc`が存在しないため終了コード1で対象ファイル到達前に停止した。
- 原因境界はCLI実行ファイル解決であり、プロジェクト・Unity・対象Markdownへの状態変更は0件。
- AGENTS.mdとCommand Failure Playbookの既存規則どおり、別Shellや別Unixコマンドへ切り替えず、`safe-read-batch.ps1`の`param(...)`と実在を確認した。
- `rtk C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe ... -File Tools/TokenUsage/safe-read-batch.ps1 -Path Docs/AgentRules/core-files.md -Ranges "1-20" -PrintOutput`の限定自己テストが終了コード0で成功した。
- 外部記憶`Knowledge/rtk-codex-windows.md`へ、長さ不明ファイルを`wc`で測らず`safe-read-batch`へ十分な上限範囲を渡して読む規則を追記した。

## 2026-07-27 Graphify Pilot 再集計

- 前回production集計終端（20:41）以降は5記録。内訳は`EnsureFresh` 2、`Affected` 2、実行済み`Fallback` 1。
- 新規`Affected`は合計179推定token、中央値89.5 token、合計1.548秒、中央値774ms。`RuntimeSpriteOutline`は3結果でそのまま採用し、`RuntimePerformanceSentinel`は0結果をverification対象としてfallbackへ移行した。
- fallback推奨1件に対して実行1件で、共通`fallback_id`の一致を確認した。新規2件は小出力だったため表示制限発動0件。上限機能自体はevaluationの126結果で検証済み。
- 新規`EnsureFresh`はfresh 1回（689ms）、rebuild 1回（40.186秒）。新規Graphify関連経過45.520秒のうち89.8%がfreshness処理で、問い合わせ本体は3.4%、fallbackは6.8%。
- 累積productionはGraphify問い合わせ26件、推定7,356 token、中央値92 token、verification 8件（30.8%）。`EnsureFresh`は25回中20回rebuildで、累積経過時間の96.8%を占める。
- 新規問い合わせ2件だけでは削減率の再判定に不足する。production問い合わせ20件程度まで継続蓄積し、表示制限率・fallback実利用率・Action別tokenを再評価する。

## 2026-07-27 TokenReports 棚卸し

- 既存の全体集計入口は`Tools/TokenUsage/token-report-summary.ps1`。前回のGraphify再集計では専用`graphify-pilot-usage.jsonl`だけを直接集計しており、この全体Reporterは参照していなかった。
- 直近30日相当は`safe_command` 15,632件、capture全文ベース約9,973,399推定token。中央値258、p90 1,397、p95 2,019、p99 4,113。blocked 27、失敗245、timeout 7。
- capture tokenの85.3%（約851万）はfile read。次いでgit diff 6.7%、content search 4.5%、Unity/Editor 2.0%。最大の削減対象はread回数とread範囲。
- read上位は`GameManager.cs` 273回／約62.9万、`safe-read.ps1` 677回／約48.9万、`WeaponController.cs` 323回／約46.8万、`GameConfig.cs` 127回／約22.1万、`ctx/current.md` 158回／約16.5万。
- 現Reporterはcapture全文を集計する一方、`PrintOutput`／実表示tokenを記録しないため、約997万は実クレジット消費ではなくraw capture上限。tool metadata、会話、推論、画像等も自動集計外。
- `token_start_marker`は全期間5件、`token_coverage_snapshot` 0件、`manual_untracked_usage` 1件で、UI消費率とのcoverage計測は実運用されていない。
- 日次baselineは2026-06-20のまま。`Run-TokenDailyHealth.ps1`は固定5コマンドの旧ベンチで、現Reporterにない`-ExcludeBenchmark`呼び出しが残る。`TokenReports/Archive`も空。
- `token-report-summary.ps1`はGraphify JSONLをkind空欄66件として混在させ、旧ログparse error 14件も含む。全体scanは今回13.8秒で、長期継続にはschema分離・stream集計・retentionが必要。
- 対策優先度は、(1) capture tokenと実表示tokenの分離記録、task/session ID追加、(2) Reporter schema修正とGraphify統合、(3) `ctx/current.md`短縮とWrapper param preflightのcompact contract化、(4) GameManager等の反復readをGraphify/focused rangeへ置換、(5) Compile/Console/marker確認のbatch化、(6) 旧daily baselineとArchive運用の更新。

## 2026-07-27 締め作業トークン監査の高優先導入

- `Safe-Command.ps1`をschema v2化し、capture全文token、モデルへ実表示したcapture部分、非表示部分、`PrintOutput`要求、blocked、呼び出しWrapperを分離記録するようにした。既存`estimate`は互換維持。
- `safe-graphify-pilot.ps1`は表示制限後の実表示tokenを`displayed_estimated_tokens`へ記録し、`focused-search.ps1`のGraphify fallbackも実表示出力を同じusage JSONLへ記録する。
- `Get-TokenReportSummary.ps1`はSafe-Command schema v2とGraphify schemaを統合し、capture／displayed、legacy gap／current schema gap、measurement coverageを分離する。Graphify JSONLがkind空欄として混入する旧集計を解消した。
- `closeout-token-report.ps1`を追加。締め時の表示token、raw capture、計測coverage、legacy/current gap、失敗、family別消費、反復commandを集計し、`reduce-file-read`、`reduce-git-diff`、`deduplicate-command`、`reduce-command-failures`、`high-visible-output`等の対策を生成する。
- `Run-TokenDailyHealth.ps1`の旧`-ExcludeBenchmark`呼び出しを現Reporter契約へ修正した。
- `area-survivors-closeout`スキルへトークン監査を必須工程として追加。captureを課金tokenと呼ばず、high／measurement incomplete時は原因と対策を日本語で必ず提示し、現行logger欠落は締め続行前に修正する。
- 最終実ログ監査は`high`。表示済みcommand output 23,974推定token、file read 22,403で主因。schema導入前legacy gap 2,004件、current schema gap 0件、session coverage snapshot 0件。raw capture 1,153,603は課金量ではない。
- schema v2実記録77件、current gap 0件。表示ありはcapture=displayed、非表示はdisplayed=0／hidden=capture、caller script保存を確認した。
- `missing-session-coverage`を追加し、Wrapper外tool、tool metadata、会話、推論がcommand output集計外であることを締め時に必ず明記する。UI開始率・終了率・budgetが揃う場合だけ`session-coverage.ps1 -Save`で差分推定し、不明値は推測しない。
- `command-tools-self-test.ps1`は38 script成功。`closeout-token-report -SelfTest`はmeasured／legacy／current gap／Graphify fixture成功。更新後`area-survivors-closeout`は`quick_validate.py`成功。
- Skill metadata更新中、UTF-8 BOM先頭行の複数file patch不一致、skill-creator Python群のCP932依存、入れ子Python呼び出しregexのfalse positiveを確定した。Playbook、`Knowledge/rtk-codex-windows.md`、generator／quick validator／init scriptのUTF-8固定へ反映済み。

## TODO / Blocker

- TODO: 次の責務分割候補は`RunStageController`（stage timer、boss、round transition）。
- TODO: その後の候補は`FixedBuildingLayoutService`（固定建造物slot定義）と`LevelUpController`（XP、選択肢、reroll/skip、panel）。
- TODO: `GameHudController`内のPlayer/Tower panel分割は、独立保守が必要になった場合だけ行う。
- ユーザー実機でStage 4まで再プレイし、体感の大幅改善を確認済み。定量比較は敵上限前sessionとのStage 1～3比較まで完了。
- 敵絶対上限修正後の難易度5・Stage 3リッチ戦再計測は完了。enemy peak 200、cap-blocked 106、最悪p95 68.83ms／max 146.98msを確認した。
- TODO: 200体／400体／800体A/B matrixは実測済み。Bounceは独立モード未実装のため、Outline／collision対応後も原因が残る場合だけ追加する。
- TODO: ユーザー実機で通常敵同士が従来通り押し合うこと、Player／塔／壁への接触ダメージが0.75秒間隔で継続すること、ボス重量挙動、武器命中に違和感がないことを確認する。
- TODO: 次回Performance Sessionで同等敵数のp95／maxを比較する。Physics solver負荷は維持されるため、今回削減されるのは主にEnemy×Enemy `OnCollisionStay2D`内のmanaged Component検索と誤ったcooldown進行。
- TODO: 同一武器・同一敵数のGameplayTest A/BでEnemy Layer早期returnの単独効果を確定する。実プレイ比較では高密度帯31.7～60.6%改善したが、条件差が残る。
- TODO: Collider2D callback layer filteringは今回未適用。Layer早期returnの効果測定後、solver／押し合いを維持したままmanaged callback dispatch自体も抑止できるか限定検証する。
- TODO: 根本対応の第二候補は通常敵の個別Update／LateUpdate／FixedUpdateを`EnemySimulationSystem`へ集約し、近距離／画面内だけ高頻度、遠距離敵は低頻度で時分割更新すること。
- Outline第一段階は完了。敵だけchange-driven同期＋8/4/1フレームの安全確認へ変更し、輪郭shader参照を13回から9回へ削減した。YSortも描画順が変わった場合だけ書き込む。
- TODO: Outlineの共有Material／main shader統合は、今回の実機再計測後もOutline負荷が大きい場合だけ検討する。Bounce中央batch／shader化、OcclusionのPlayer／Boss優先中央manager化は未対応。
- TODO: Enemy territory paintは敵ごとではなく、中央managerがoccupied cellを重複排除して一定間隔で1回だけpaintする。
- TODO: Pickupは必要ならworld cell別spatial bucketと近接orbのvalue集約／pool化で、全idle scanと1,000 Rendererを減らす。
- TODO: ユーザー実機で召喚演出、Skeleton／SkeletonKnight生成、Banana命中感に違和感がないことを確認する。
- TODO: Tokenの通常吸引、高速移動で吸引範囲を通過した場合、Stage 1～3ボス後のToken全体吸引を明示確認する。XP獲得はユーザー確認済み。
- TODO: Tokenについても到着時点で値が増えることを明示確認する。XP到着時加算と複数レベルアップ選択キューはユーザー確認済み。
- TODO: Pickup移行後のcounterは取得済み。敵絶対上限後も1000個超のPickupが残る場合、scan candidatesとmovement ticksを再比較する。
- TODO: registry移行後も1000個超のPickup本体・Rendererが残る負荷が大きい場合だけ、value集約poolまたはactive上限を追加検討する。
- Stage別Incident予約後のsessionでStage 4を5件取得し、総上限による欠落0件を確認した。同種集約とStage予約は完了。
- TODO: object急増の検知は毎秒の全Object走査を避けるため未実装。実測で必要性が出た場合は対象型へ軽量registryを追加して差分検知する。
- TODO: Scenario matrixを各敵数で複数回実行し、固定baselineとの絶対budget／相対回帰率で合否判定する。現時点は1Play内のA/B自動実行までで、200→400→800のPlay Mode連続起動は未自動化。
- TODO: 800体は同一Play内でもBaseline p95が+58.5% driftした。厳密なA/B採用判定には、各modeをfresh Playで個別実行するか、敵再生成とPhysics contact state初期化を含む分離Runnerへ更新する。
- TODO: A/Bモードの効果量から原因候補をランキングし、関連counter／Profiler marker／コードsymbolを報告する。
- TODO: Codexの限定修正→Compile/Validator→同一Scenario再計測→改善時だけ採用、を単一Wrapperへ統合する。
- TODO: incidentからGameplayTest再現Scenarioを自動生成する機能とRNG seed記録は未実装。
- ユーザー実機で、全Stage難易度5クリア済みセーブのStage 4再クリア時にエンドロールが再表示されず、結果画面へ進むことを確認済み。セーブ単位の一度限り表示対応は完了。

## Next Action

- Runtime Performance Sentinelの総上限20件をStage 1〜4へ各5件ずつ予約し、同一Stage・同一原因カテゴリの詳細保存を2件までに集約した。Incident開始時のStage／難易度／敵上限／経過時間を固定し、遷移後の誤分類も防止した。
- `session.json`、`session-summary.md`、`performance-session-report.ps1`へStage別取得件数とquota抑制件数を追加した。
- `performance-session-report.ps1 -SelfTest`、`command-tools-self-test.ps1`、scoped diff check成功。
- Unity Compileは2回成功。Runtime Performance Sentinel ValidatorとCombat Performance Probe Validatorはfresh marker成功、Console Error表示0件。Play Modeは開始していない。
- ユーザー実機session `20260727-125845-155-9cd397`（難易度5、16分51秒）を取得した。Stage coverageはStage 1=`2`、Stage 2=`5`、Stage 3=`5`、Stage 4=`5`、session上限抑制=`0`で、Stage 4記録欠落を解消した。
- Stage 4最悪incidentは敵200体、p95 `87.20ms`、max `129.88ms`。敵198体の次incidentはp95 `74.37ms`、max `88.51ms`。
- 敵135体ではp95 `34.78ms`、敵200体では`87.20ms`となり、敵数48%増に対してp95は約2.5倍へ非線形増加した。
- XP Orb 1,077個のStage 3 incidentはp95 `66.65ms`、XP Orb 560個のStage 4 incidentは`87.20ms`のため、Pickup数は第一原因ではない。
- GCはStage 3/4とも概ね`15.75～20.96MB/s`で、p95差を単独では説明しない。ただし敵200体の重いフレームでは1フレーム約`2～3MB`のallocationがあり、二次増幅要因として残る。
- Stage 4の約90～100msフレームにはDamage Feedback 0件、Projectile Trigger 0件、Projectile Damage 0件のものが複数ある。大量ヒットだけでなく、Enemy Physics solver、個別Enemy Update/Visual、Outline等の基礎負荷が第一原因。
- Damage Feedback 216件、Popup Drop 184件、Hit Flash 177体が重なるフレームも約100msであり、大量命中Visualは第二原因。PopupはPool生成0・再利用のみのためInstantiate問題は解消済みだが、active上限96体の更新・描画負荷は残る。
- 次の改善優先度は、敵同士の押し合いを維持したまま、(1) Outlineのshader統合またはchange-driven同期、(2) Enemy個別Update/LateUpdateの中央時分割、(3) Hit Flash/Popupの描画・更新上限見直し、(4) GC allocation発生源の追加counter化。
- 2026-07-27 Outline第一段階を実装した。`RuntimeSpriteOutline`はSprite／色／表示／描画順の変更要求を同一LateUpdateへ集約し、通常敵8・Elite4・Boss1フレームの安全確認だけ残す。`PaperMeshVisual`はOutline参照を一度だけ解決し、`YSort`は同値のsortingOrder書き込みを省いた。
- 敵だけ`AREA_OUTLINE_CROWD_OPTIMIZED` shader variantを有効化し、center＋周囲12回だったtexture参照をcenter＋周囲8回へ削減した。建造物・UI等は従来の13回参照を維持する。
- Unity Compile 1回成功。Combat Performance Probe Validatorはfresh marker成功、Console Error表示0件。Play Modeは開始していない。
- ユーザー実機でOutline第一段階後の表示を確認し、通常プレイで違和感なし。黒Outlineの欠け、太さの揺れ、Sprite切替残像、YSortずれは報告されず、表示回帰確認は完了。
- 最新session `20260727-133427-667-16a13e`はStage 1までの3分34秒。敵200体incidentはp95 `62.30ms`、max `77.32ms`、100ms超0件。baseline p95は`15.68ms`。
- Outline第一段階後のStage 4到達session `20260727-134055-188-e98932`（難易度5、敵上限200、15分26秒）を測定。Stage 1=`3`、Stage 2=`4`、Stage 3=`4`、Stage 4=`3` incidentを取得し、session上限抑制0件。
- session baseline p95は前回`36.74ms`から`31.42ms`へ14.5%改善。Sentinel平均負荷は`8.69us`から`8.64us`で同等。
- Stage 3の高密度帯は新sessionの敵190～193体でp95 `51.69～69.46ms`、旧sessionの敵200体で`57.54～71.07ms`。高密度incident平均p95は`65.09ms`から`60.58ms`へ6.9%改善、最悪p95は2.3%改善。Stage全体maxは`111.10ms`から`102.48ms`へ7.8%改善。
- Stage 4最悪p95は`87.20ms`から`58.93ms`へ32.4%改善、最悪maxは`129.88ms`から`86.29ms`へ33.6%改善。100ms超フレームは旧sessionの4件から0件になった。
- ただしStage 4の敵peakは旧200体に対して新185体、武器は旧Slash/Frost/ArrowRain、新Slash/ThunderBall/Shield、XP Orb量も大きく異なる。Stage 4改善量の全てをOutline単独効果とは断定しない。
- Stage 1ではXP Orb 1,448個・敵200体のincidentがp95 `79.95ms`、ボス遷移を含む次incidentがp95 `88.10ms`／max `169.03ms`。Stage 3/4改善後も、極端なPickup残留とボス遷移は別の高負荷条件として残る。
- TODO: Outline単独の厳密な効果量は、同一GameplayTestシナリオのOutline有効／無効または旧／新shader variant A/Bで確定する。実プレイのエンドツーエンド改善と表示回帰なしは確認済み。
- 調査中の停止記録: `safe-read -PrintOutput`へ150行を渡してguard 39、未確定の旧`CombatPerformanceDiagnostics.cs`パスを渡してguard 33、停止中ObsidianへCLI接続して終了コード1となった。コード変更前に各失敗境界を確定し、`safe-read-batch`限定自己テスト、`safe-search -FilesOnly`での実在パス確定、Obsidian Vault設定からのローカル編集へ固定した。`command-tools-self-test.ps1`は全項目成功。
- 最終status確認時、外部記憶repoをworkdirにした同一ShellへAreaSurvivorsの`safe-status.ps1`を混在させ、先頭コマンドだけパス不在で停止した。repo境界の混在が原因で、AreaSurvivors rootからの限定再確認は成功した。以後はrepoごとにShellとworkdirを分離する。

## 2026-07-27 Pickup残留・ボス遷移の根本負荷対策

- 最新Stage 4 sessionの追加分析で、Stage 1にXP Orb 1,448個が残り、p95 `79.95ms`、ボス遷移時p95 `88.10ms`／max `169.03ms`となる独立した高負荷条件を確認した。
- 同一`TileGrid`セル内に待機中XP Orbがある場合、新規Instantiateを行わず既存Orbの`value`へXPを加算する空間集約を追加した。XP総量、プレイヤー接近時／ボス後の吸引、到着時の経験値加算と複数レベルアップキューは維持する。
- `PickupAttractionRegistry`へXPセルindexと遷移吸引中件数を追加した。吸引開始時はセルindexから外し、無効化／回収完了時は全registryから除去する。
- `GameManager.AttractRemainingStageRewards`は一覧を再利用し、毎フレーム全Pickupを走査する`HasActiveStageTransitionAttraction`を廃止した。完了待ちは`StageTransitionAttractionCount`のO(1)判定へ変更した。
- 性能counterへ`xpOrbMerges`を追加し、Runtime Performance Sentinelと`performance-stage-detail-report.ps1`へ伝播した。次回sessionでは物理生成数`xpOrbSpawns`と集約数`xpOrbMerges`を分離比較できる。
- `Stage Transition Enemy Defeat Validator`へ、同一セルのXP値保持、Active件数不変、遷移吸引件数の登録／解除、旧毎フレーム走査の不在を追加した。
- 検証中の最初のfixtureはEdit Modeで`AddComponent`後にRuntime `OnEnable`登録が走ると誤認し、`activeBefore=0`で失敗した。`RegisterForValidation`明示登録、セル中央座標、実測診断値へ修正し、外部記憶`Knowledge/rtk-codex-windows.md`へ再発防止を記録した。
- Unity Compileは3回成功。最終`Stage Transition Enemy Defeat Validator` fresh marker成功、Console Error表示0件、`performance-stage-detail-report -SelfTest`と旧session読込成功、対象差分のwhitespace検査成功。Play Modeは開始していない。
- TODO: ユーザー実機で通常時のXP Orb表示・接近吸引・ボス後吸引・経験値総量・複数レベルアップに違和感がないことを確認する。
- TODO: 次回Stage 4到達sessionで、旧session `20260727-134055-188-e98932`のStage 1 XP Orb 1,448個、p95 `79.95ms`、遷移max `169.03ms`に対する`activeExperienceOrbs`、`xpOrbSpawns`、`xpOrbMerges`、`pickupScanCandidates`、`pickupMovementTicks`、p95／maxの改善量を比較する。

## 2026-07-27 XP集約後の実機性能比較

- ユーザー実機session `20260727-141616-256-9ef5d2`（難易度5、敵上限200、15分55秒）を取得。Stage 1=`4`、Stage 2=`5`、Stage 3=`3`、Stage 4=`3` incident、session上限抑制0件。
- Stage 1の最大Active XP Orbは`1,448`から`471`へ67.5%減少。高密度帯のPickup scan候補平均は`1,457.7`から`418.6`へ71.3%、peakは`1,778`から`434`へ75.6%減少した。
- Stage 1の敵190体以上を抽出した重複除外フレームでは、p95 `86.57ms`→`61.94ms`（28.4%改善）、max `134.70ms`→`109.15ms`（19.0%改善）。
- Stage 1ボス後の敵0体・Pickup吸引中フレームでは、移動Pickup peak `1,980`→`567`（71.4%減）、平均フレーム`27.54ms`→`17.12ms`（37.8%改善）、p95 `53.04ms`→`26.71ms`（49.6%改善）、max `60.48ms`→`43.57ms`（28.0%改善）。
- 新sessionのIncident区間では物理XP Orb生成`803`回に対して同一セル集約`987`回を記録した。記録範囲内のXP報酬要求の55.1%で新規Orb生成を回避している。
- Stage 4は敵peakが旧`185`から新`200`へ増えた条件でも、最悪p95 `58.93ms`→`58.36ms`、最悪max `86.29ms`→`74.66ms`（13.5%改善）。Stage 4で100ms超フレームはない。
- session baseline p95は`31.42ms`→`35.34ms`と12.5%高いため、全体比較は新sessionに不利な環境差を含む。それでもPickup局所指標と遷移フレームは大幅改善しており、XP空間集約とO(1)遷移完了判定の効果を確認できた。
- 残課題はPickupと無関係な単発スパイク。Stage 3に`119.58ms`が1回あり、敵177体、XP生成1、集約0、Pickup走査0、移動0、Projectile Trigger 0、Damage Feedback 0、GC allocation約0.98MBだった。Enemy Physics／個別Enemy Visual・Update／GCの基礎負荷が次の調査対象。
- ユーザー実機プレイ完了。XP Orb表示、通常吸引、ボス後吸引、経験値総量、複数レベルアップについて不具合報告は現時点でなし。

## 2026-07-27 Graphify Pilot 再集計（22:20以降）

- `TokenReports/graphify-pilot-usage.jsonl`は75行すべてJSON解析成功。内訳は明示production 61、evaluation 4、旧schemaのcategory未設定10。
- 前回締切`2026-07-27T22:20:24.0003678+09:00`より後のproductionは9件。`EnsureFresh` 2件、`Affected` 4件、`Fallback` 3件。
- 新規Graphify query 4件は中央値586ms、capture上限529 token、verification 3件（75%）。推奨されたfallback 3件はすべて同じ`fallback_id`で実行・照合できた。`output_limited`は0件。
- 表示トークン計測導入後の最新5件は欠損0。Graphify本体（EnsureFresh＋Affected 2件）の表示は42 token、fallback 2件は2,146 tokenで、表示合計2,188 tokenの98.1%をfallbackが占めた。
- 最新の`RelicCatalog.TryPickRandom`と`TryPickRandom`は`Affected`が0件となり、fallbackへ865／1,281 tokenを使用した。定義・実装確認のmethod symbolは`focused-search`を先に使い、Graphifyは正確なgraph nodeを確認できた影響調査または2 symbol間の`Path`へ限定する余地が大きい。
- 新規9件の総経過76.995秒中、`EnsureFresh` 2回のrebuildが68.993秒（89.6%）。同じfresh graphを継続利用し、タスク単位でrebuildを1回へ抑える方針は引き続き最優先。
- production累計は61件。Graphify query 30件、中央値625ms、capture上限7,885 token、verification 11件（36.7%）。`EnsureFresh` 27件中22件がrebuild、fallbackは4件すべて推奨IDと照合済み。
- 表示トークンの正確な比較は最新5件から可能。締切後9件のうち先行4件は旧schemaで表示値がなく、capture値を実消費または課金tokenとして扱わない。
- TODO: 次の実作業では、method symbolの定義確認を`focused-search`へルーティングし、Graphifyを使う場合は1回の`EnsureFresh`後に複数queryをまとめる。表示計測済みproductionを20件以上蓄積した時点で、Graphify成功queryとfallbackの表示token中央値を再評価する。

## 2026-07-27 エクスカリバー間隔・全レリック取得後の重複変換

- `GameConfig.excaliburCooldownSeconds`と`Resources/Config/GameConfig.asset`を`5秒`から`3秒`へ変更した。`AdvancedWeaponRuntime`と`WeaponController`の既存参照経路は維持した。
- 未取得レリックが1つでも残る間は、取得済みを抽選候補から除外する現行仕様を維持した。最後の1つが残る場合もその未取得1種だけが候補になる。
- 全34種を取得済みの場合だけ、全34種を抽選poolへ戻す。レア度抽選比は従来どおりコモン50／アンコモン30／レア15／レジェンダリー5、同一レア度内は均等抽選。
- 重複時はコモン5／アンコモン10／レア30／レジェンダリー50トークンへ変換し、即時セーブする。
- フィールド`RelicChest`とStage 4再クリア等の`AcquireRelicRewardRoutine`を`RelicCatalog.TryAcquireReward`へ統一した。新規取得時だけPlayer statsを更新し、重複時は既存`RelicAcquisitionPanel`の宝箱OPEN演出と変換トークン表示を通す。
- `Relic Drop Eligibility Validator`へ、最後の未取得がある間の重複禁止、全取得後の全候補復帰、50/30/15/5抽選比、5/10/30/50変換値、2つの獲得経路、開封パネル継続を追加した。
- `Excalibur Cooldown Validator`を追加し、GameConfig assetが3秒であることを検証する。
- Unity Compile 1回成功。Relic Drop Eligibility ValidatorとExcalibur Cooldown Validatorはfresh marker成功、Console Error表示0件、対象差分のwhitespace検査成功。Play Modeは開始していない。
- ユーザー実機で、エクスカリバーが約3秒間隔で発射されること、全レリック取得済みセーブで宝箱OPEN演出後に重複レリックと変換トークンが表示されることを確認済み。対応完了。
- 調査中、`git show`を`Select-Object -First`へ直接パイプしてbroken pipe相当の終了コード1になった。本番変更前に停止し、Native command全般で全出力と終了コードを先に配列取得するルールへPlaybookと外部記憶を更新し、限定自己テストと`command-tools-self-test`成功後に再開した。

## 2026-07-28 Steam実績の解除・表示タイミング調査

- `SteamAchievementRuntime`はTitle Sceneへ1個配置する設計で、`Awake`時に`DontDestroyOnLoad`され、ゲーム中・結果画面・ロビーでも同じinstanceが動作し続ける。Title再読込時の重複instanceは無効化される。
- 実績はタイトル復帰専用処理ではない。Steam stats初期化完了直後と、`ProgressionStore.Save()`成功後の`Saved` eventごとに、セーブデータから全13実績を再評価する。撃破数だけは敵撃破時にも未保存分を含めて評価する。
- 条件成立時は`SteamUserStats.SetAchievement`を行い、成立した全実績をまとめて`SteamUserStats.StoreStats`する。ゲーム側の実績ポップアップUIはなく、`UserAchievementStored_t` callbackではConsole logだけを出す。
- Stage clearはボス撃破routine冒頭の`ProgressionStore.MarkStageCleared`でSaveされるため、その時点でSteam送信が始まる。Steam client側の保存callback／Overlay通知は非同期なので、通知描画が結果画面やTitle/Lobbyへの遷移後に見える可能性がある。
- 起動時にSteam未同期の解除条件を既存セーブが満たしている場合も、Title SceneでSteam初期化が完了した直後の再評価により解除・表示される。これはオフライン時の取りこぼし防止を意図した遡及同期。
- TODO: 表示タイミングを厳密に実測・変更する場合は、実績API名・条件成立時刻・`StoreStats`呼出時刻・callback時刻・active sceneを記録する限定diagnosticを追加してSteam client上で確認する。現時点では挙動変更は未実施。

## 2026-07-28 Steam実績の再テスト方法

- Steam公式の開発用手段として、Steam client consoleの`achievement_clear 4980380 <API名>`で個別実績、`reset_all_stats 4980380`で当該アカウントの全統計・実績をリセットできる。APIでは`ClearAchievement`＋`StoreStats`、または`ResetAllStats(true)`が相当する。
- AreaSurvivorsは起動時に既存セーブを遡及判定するため、Steam側だけ未解除へ戻してもセーブが条件を満たしていれば起動直後に再解除される。現在のセーブを保持したまま条件達成タイミングを再検証するには、別テストアカウント、バックアップした新規セーブ、または製品版に残らない開発専用テスト経路が必要。
- ユーザーがゲーム内設定からプレイデータを初期化した後、Unity Play Mode停止・AreaSurvivors実行プロセス不在を確認してSteam Consoleで`reset_all_stats 4980380`を実行した。
- Steam Console表示と`C:/Program Files (x86)/Steam/logs/console_log.txt`の両方で、`2026-07-28 09:33:38 reset_all_stats success for appid 4980380`を確認した。Steam側の全実績・統計リセットは成功。
- TODO: 次回起動後、Steamライブラリで全13実績が未解除表示であることと、条件達成時のOverlay通知タイミングをユーザー実機で確認する。

## 2026-07-28 Steam実績時系列診断ログ

- `SteamAchievementRuntime`へイベント駆動のJSONL診断ログを追加した。実績判定・Steam送信順序・既存セーブ遡及判定は変更していない。
- 保存先は`Application.persistentDataPath/SteamAchievementReports/steam-achievement-session-<UTC>-<suffix>.jsonl`。起動時にUnity Consoleへ絶対Pathを1回出力する。
- 記録対象はRuntime/Steam初期化、stats ready/waiting、Scene遷移、`SetAchievement`結果、`StoreStats`受付結果、`UserStatsStored` callback、`UserAchievementStored` callback、解除件数、trigger、Scene、UTC、frame、realtime、累計撃破数、プレイ回数、最高クリアStage。
- 毎Kill・毎Saveの未解除0件評価はファイルへ記録せず、実績解除やSteam callback等のイベント時だけ追記する。診断書込失敗時はゲーム処理を止めず、警告1回で診断だけを無効化する。
- 最初のCompile検証は変更C#をAssetImportする前に実行したため、コードエラーではなくstale検出`guard_code: 41`で停止した。原因と正式順序を確認し、`command-tools-self-test`全項目成功後、Runtime C#を明示AssetImportして再開した。
- 外部記憶`vault/Knowledge/area-survivors-token-usage-tools.md`へ、既存C#でもAssetImport→Compileの順を省略しないことと、Obsidian Vaultの実在ノートPathをFilesOnlyで確定することを追記した。
- 最終CompileはRuntime／Editor Assemblyともcurrent。Steam Achievements Validator fresh marker成功、Console Error表示0件、対象C#のscoped diff check成功。Play Modeは開始していない。
- TODO: ユーザー通しプレイ後、最新JSONL、Unity Player log、Steam `logs/stats_log.txt`を時系列比較し、Overlay通知が表示された画面の申告と照合する。
