# Asset Cleanup 2026-07-09

## Scope

- 対象: `Assets/AreaSurvivors`
- 目的: 未使用Assetの削除、今後のフォルダ整理候補の洗い出し
- 注意: 既存のScene/Prefab/HUDレイアウトはユーザー調整済みのため、配置・サイズ・参照を勝手に戻さない

## Reports

- Unity参照レポート: `TokenReports/UnityReports/asset-references-20260709-124733.md`
- 削除候補フィルタ: `TokenReports/UnityReports/asset-cleanup-candidates-20260709.md`
- External PNG候補一覧: `TokenReports/UnityReports/external-zero-guidref-candidates-20260709.txt`
- External削除リスト: `TokenReports/UnityReports/external-delete-list-20260709.txt`
- 削除後Unity参照レポート: `TokenReports/UnityReports/asset-references-20260709-130130.md`
- Prefab整理後Unity参照レポート: `TokenReports/UnityReports/asset-references-20260709-131332.md`
- Generated Sprite整理後Unity参照レポート: `TokenReports/UnityReports/asset-references-20260709-132958.md`

## Completed

### Deleted

`Sprites/Generated/CharacterSheets` は以下4枚のみで構成され、いずれもGeneratedSpriteCatalog外、Scene/Prefab/Asset GUID参照なし、コード名参照なしだったため削除した。

- `Assets/AreaSurvivors/Sprites/Generated/CharacterSheets/Archer.png`
- `Assets/AreaSurvivors/Sprites/Generated/CharacterSheets/EnemyBoar.png`
- `Assets/AreaSurvivors/Sprites/Generated/CharacterSheets/Knight.png`
- `Assets/AreaSurvivors/Sprites/Generated/CharacterSheets/Mage.png`
- 上記 `.meta`
- 空になった `Assets/AreaSurvivors/Sprites/Generated/CharacterSheets` フォルダと `.meta`

検証:

- `unicli exec Compile`: 成功

### Deleted External Source Assets

`Assets/AreaSurvivors/Sprites/External` 配下のPNG 190件と対応する `.png.meta` 190件を削除した。

削除前に以下を確認した。

- External PNG 190件すべてがScene/Prefab/AssetからのGUID参照ゼロ
- ゲーム実行時参照なし
- 古いEditorセットアップ側のExternal固定パス依存は削除済み

削除後に空フォルダとフォルダ `.meta` も整理した。

削除効果:

- 約171.6MB削減
- PNG 190件削除
- PNG meta 190件削除

検証:

- `unicli exec Compile`: 成功
- 削除後Asset Reference Reporter: `review-candidate` 0件

### Reorganized Root Prefabs

`Assets/AreaSurvivors/Prefabs` 直下に散らばっていたPrefabをカテゴリ別フォルダへ移動した。

- `Prefabs/Buildings`: `BallistaTower`, `CenterTower`, `RelicChest`, `WatchTower`, `WoodenWall`
- `Prefabs/Characters`: `Enemy`, `Player`
- `Prefabs/Effects`: `ProjectileExplosionHitbox`, `ProjectileImpact`
- `Prefabs/Pickups`: `ExperienceOrb`
- `Prefabs/UI`: `DamagePopup`, `TokenGainPopup`
- `Prefabs/Weapons`: `Arrow`, `BallistaArrow`, `BossDarkOrb`, `BossDragonBreath`, `BossShockwave`, `Fireball`, `LichSummonCircle`, `PlayerArrow`, `Shield`, `Slash`, `TowerCannonball`

移動に合わせてEditor/Setup/Reporter/Test内の固定Prefabパスを新パスへ更新した。

検証:

- 旧ルートPrefabパス検索: 0件
- `Assets/AreaSurvivors/Prefabs` 直下のPrefab: 0件
- `unicli exec Compile`: 成功
- Prefab整理後Asset Reference Reporter: `review-candidate` 2件

補足:

- `TowerCannonball.prefab` は `CenterTower.prefab` と `05_Game.unity` からGUID参照あり
- `BallistaArrow.prefab` は `BallistaTower.prefab` からGUID参照あり
- 上記2件はレポート側の参照検出漏れとして保持

### Reorganized Generated Sprites

`Assets/AreaSurvivors/Sprites/Generated` 直下のPNG 114件をカテゴリ別フォルダへ移動した。

- `Buildings`: 17件
- `Characters`: 4件
- `Effects`: 2件
- `Environment`: 6件
- `Pickups`: 4件
- `Relics`: 37件
- `UI`: 既存分を含め12件
- `Walk`: 既存分とは別に旧ルートの歩行シート4件を移動
- `Weapons`: 36件

移動に合わせて、Generated SpriteのEditor側名前解決を `GeneratedSpriteAssetUtility` に集約した。これにより、Editorセットアップ系は `Generated` 直下固定ではなく、サブフォルダを含めてSprite名から探索できる。

Runtime側の `GeneratedSpriteLoader` もEditor実行時はサブフォルダ検索へ対応した。ビルド時は引き続き `GeneratedSpriteCatalog` 参照。

`GeneratedSpriteCatalog` は再構築済み。従来名（例: `Arrow`）と相対パス名（例: `Weapons/Arrow`）の両方を保持するため、既存コード互換と今後のカテゴリ指定の両方に対応できる。

検証:

- `Assets/AreaSurvivors/Sprites/Generated` 直下のPNG: 0件
- `unicli exec Compile`: 成功
- Generated Sprite整理後Asset Reference Reporter: `review-candidate` 0件
- 新規カテゴリフォルダ `.meta`: すべて追加済み

### Removed Retired Editor Utilities

現在の「Scene/Prefabを正とする」運用では誤実行リスクが高い、または空実装になっていた古いEditorメニューを削除した。

- `Assets/AreaSurvivors/Editor/GameHudRelicPanelSetup.cs`
  - 中身が空の残骸だったため削除
- `Assets/AreaSurvivors/Editor/LegacyHudCleanup.cs`
  - 過去HUDオブジェクトを破壊的に削除する移行メニューだったため削除
- `Assets/AreaSurvivors/Editor/RetiredUpgradeSceneCleanup.cs`
  - 過去スキル/ロビー要素を破壊的に削除する移行メニューだったため削除
- `Assets/AreaSurvivors/Editor/LobbyReferenceLayoutSceneSetup.cs`
  - ユーザー調整済みロビー配置を旧レイアウトへ戻す復元メニューだったため削除

検証:

- 削除対象名の参照検索: 0件
- `unicli exec Compile`: 成功
- `Area Survivors/Validate/HUD Layout Mutation Guard`: 実行成功

補足:

- Editorスクリプトの `Reports` / `Setup` / `Utilities` / `Validation` フォルダ分けも試行したが、Unity Editorのコンパイル対象キャッシュが旧パスを保持し続けたため今回は見送り、元配置へ戻した。
- Editorスクリプトのフォルダ分けを行う場合は、Unity Editor上の `AssetDatabase.MoveAsset` ベースで実施する方が安全。

### Disabled High-Risk Editor MenuItems

Scene/Prefab/静的UIをEditorメニューから再生成・上書きできる古い `Apply` / `Rebuild` / `Setup` 系メニューは、現在の運用では誤実行リスクが高いため `[MenuItem]` 属性だけを外した。

メソッド本体は削除せず残しているため、将来どうしても必要な場合はCodex作業内で明示的に呼び出せる。ただし通常のUnityメニューからは実行できない。

主な対象:

- タイトル、オプション、ポーズ、武器図鑑、HUD属性アイコンなどの静的UI再生成メニュー
- ロビー難易度UI、ロビーテストボタンなどのロビー配置変更メニュー
- スキルツリー、スキルリンク、スキルツールチップ、スキルアイコン再設定メニュー
- レリック機能一括適用、レリックHUD/図鑑スロット同期メニュー
- 建造物、プレイヤー、武器、ボス攻撃、ポップアップなどのPrefab再生成メニュー
- HUD重なりグループ再構成メニュー

残したメニュー:

- `Reports` 系
- `Validate` 系
- Generated Sprite Catalog再構築
- 明示的なConfig/Project補助メニュー

追加で外したメニュー:

- Gameplay Test Scene生成、起動、Scenario選択/実行系メニュー
- Gameplay Test Launcher Scene更新系メニュー
- Mapの保存済みGround Tile削除、Ground Preview再構築、Map Perimeter再構築メニュー

補足:

- Gameplay Test関連のメソッド本体、およびScenario Inspector上の小さな選択ボタンは残している。通常のUnityメニューからは実行できない。
- Map Clear/Rebuild系もメソッド本体は残している。必要な場合はCodex作業内で明示的に呼び出す。

検証:

- `unicli exec Compile`: 成功
- `Area Survivors/Validate/HUD Layout Mutation Guard`: 実行成功
- 通常Scene/HUD/スキルツリー/Prefabを上書きしそうな `Apply` 系メニュー検索: 該当なし
- Gameplay Test系、Map Clear/Rebuild系メニュー検索: 該当なし

### Removed Unreferenced Retired Setup Scripts

MenuItemを外した旧Setup/再生成系Editorスクリプトについて、C#参照を確認したうえで外部参照ゼロ、または削除対象同士の参照だけだったものを削除した。

削除した主なカテゴリ:

- 武器、ボス攻撃、シールド、監視塔、トークンポップアップなどの旧Prefab/攻撃セットアップ
- ロビー、タイトル、図鑑、オプション、ポーズなどの旧Scene/UI再生成補助
- スキルツリー、スキルリンク、ツールチップ、スキルアイコン旧バインダー
- レリック機能一括適用、建造メニュー、建造物Prefabレイアウト旧セットアップ
- Gameplay Test Launcher、Ground Tilemap Clear/Rebuild旧メニュー実装

削除対象の一部はUnityのコンパイル対象キャッシュが残りやすかったため、一度退役プレースホルダーでコンパイルを復旧し、AssetDatabase経由で正式削除した。

検証:

- 削除対象クラス名のC#残存検索: 0件
- 退役プレースホルダー残存検索: 0件
- `unicli exec Compile`: 成功
- `Area Survivors/Validate/HUD Layout Mutation Guard`: 実行成功

### Organized Game Scripts

`Assets/AreaSurvivors/Scripts/Game` 直下に77件のC#が集中していたため、Unityの `AssetDatabase.MoveAsset` ベースでGUIDを維持しながらカテゴリ別サブフォルダへ移動した。

移動後:

- `Buildings`: 12件
- `Characters`: 12件
- `Map`: 10件
- `Pickups`: 3件
- `Runtime`: 3件
- `Visuals`: 15件
- `Weapons`: 22件

補足:

- asmdefは存在しないため、フォルダ移動によるアセンブリ分割の影響はなし。
- 一時的に `GameScriptFolderOrganizer.cs` を作成して移動後、AssetDatabase経由で削除済み。
- Unity側の自動命名で一時的に `BuildingS` / `WeaponS` になったため、フォルダと `.meta` を一時名経由で `Buildings` / `Weapons` へ修正した。

検証:

- `Assets/AreaSurvivors/Scripts/Game` 直下のC#: 0件
- `unicli exec Compile`: 成功
- `Area Survivors/Validate/HUD Layout Mutation Guard`: 実行成功

### Reviewed Resources Folder

`Assets/AreaSurvivors/Resources` 配下を確認した。

現在の構成:

- `Audio/BGM`: 4件
- `Audio/SFX`: 29件
- `Config/GameConfig.asset`
- `GeneratedSpriteCatalog.asset`

Audioは `AudioCatalog` の定義と実ファイルを照合した。

検証:

- `AudioCatalog` 定義: 33件
- Resources配下Audio実ファイル: 33件
- Catalog定義に対する欠損: 0件
- Catalog未定義のAudio: 0件

判断:

- `GameConfig.asset` はRuntime/UIから `Resources.Load<GameConfig>("Config/GameConfig")` で参照されるため保持。
- `GeneratedSpriteCatalog.asset` はビルド時のGenerated Sprite解決に使うため保持。
- Resources配下の削除対象は現時点なし。

### Final Asset Reference Report

整理後に `Area Survivors/Reports/Asset References` を再実行した。

最新レポート:

- `TokenReports/UnityReports/asset-references-20260709-150157.md`

結果:

- Candidate assets scanned: 50件
- `referenced-by-guid`: 45件
- `referenced-by-code-name`: 5件
- `review-candidate`: 0件

判断:

- Reporter上の追加削除候補はなし。
- 大きな未参照候補は今回の整理範囲では残っていない。

## Findings

### External Source Assets

`Assets/AreaSurvivors/Sprites/External` にはPNGが190件あり、合計約171.6MBだった。

静的GUID照合では190件すべてがScene/Prefab/AssetからのGUID参照ゼロ。

当初、Editorスクリプト側に固定パス参照が残っていた。

- `Assets/AreaSurvivors/Editor/BuildingPrefabLayoutBuilder.cs`
- `Assets/AreaSurvivors/Editor/RelicFeatureSetup.cs`
- `Assets/AreaSurvivors/Editor/AssetReferenceReporter.cs`

対応として、古いセットアップ処理からExternalへのコピー/正規化を削除した。

`AssetReferenceReporter.cs` はExternalを候補ルートとして見るだけで、フォルダが無い場合はスキップするため保持。

### Generated Sprite Catalog

`Assets/AreaSurvivors/Sprites/Generated` のPNGは422件。

- GeneratedSpriteCatalog登録済み: 408件
- カタログ外: 14件

カタログ外のうち、今回削除した `CharacterSheets` 4件以外はProjectSettings、Scene、またはゲーム内配置から参照があるため保持。

## Pending Decisions

### Folder Reorganization

未使用削除の第一弾は完了。

次は残る整理候補を検討する。

- Editor生成/復旧系スクリプトを現在の運用に合わせてさらに削るか

移動はGUID維持が必要なため、UnityのAssetDatabase経由で行う。
