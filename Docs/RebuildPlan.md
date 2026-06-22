# AreaSurvivors 現行改修 引き継ぎ計画

## 背景

新規リブートプロジェクト `AreaSurvivorsReboot` で基盤を作り直していたが、Unity の Scene / Prefab / Builder / Validator をゼロから積むコストが大きく、完了までの見通しが長くなった。

そのため、方針を「新規リブート」から「現行 `AreaSurvivors` の段階的改修」へ戻す。

現行プロジェクトにはすでに以下が存在するため、ゼロから再構築するより改修の方が速い。

- 敵出現
- 経験値オーブ
- レベルアップ UI
- 武器レベル定義
- ボス
- スキルツリー / トークン基盤
- HUD
- セーブ
- 既存素材と Prefab
- ラウンド2用の Goblin / Ogre / GoblinLord 系素材

## 直近の重要状態

この引き継ぎ作成時点で、建造物サイズ / アップグレード画像表示まわりの未コミット変更があり、ユーザー承認によりコミット対象とする。

対象の主な変更:

- `Assets/AreaSurvivors/Editor/BuildingPrefabLayoutBuilder.cs`
- `Assets/AreaSurvivors/Prefabs/BallistaTower.prefab`
- `Assets/AreaSurvivors/Prefabs/WatchTower.prefab`
- `Assets/AreaSurvivors/Prefabs/WoodenGate.prefab`
- `Assets/AreaSurvivors/Prefabs/WoodenWall.prefab`
- `Assets/AreaSurvivors/Scripts/Game/BallistaTower.cs`
- `Assets/AreaSurvivors/Scripts/Game/WatchTower.cs`
- `Assets/AreaSurvivors/Scripts/Game/WoodenBarrier.cs`

内容の概要:

- 建造物の通常画像がアップグレード後より小さく見える問題への対処。
- `BuildingPrefabLayoutBuilder` に通常画像用の高さ補正倍率を追加。
- `BallistaTower` / `WatchTower` / `WoodenBarrier` で、アップグレード済み時に通常画像を隠す処理を追加。
- アップグレード済みSpriteがある場合は対象Spriteを反映する処理を追加。

注意:

- この対応はリブート前に調査していた建造物サイズ問題の途中成果。
- Scale / Rotation を今後禁止したい方針とは一部ぶつかる可能性があるため、現行改修ではまず「壊さず収束」させる。
- 後続作業では、画像加工 / Sprite import / Prefab layout のどこを正とするか再確認する。

## 現行改修の基本方針

残すもの:

- 敵出現システム
- 敵画像とアニメーション
- 経験値オーブ
- レベルアップ UI
- 武器レベル定義
- ボス討伐処理
- スキルツリー / トークン基盤
- 中心塔 / 建造物 Prefab
- HUD の基本構造

削る、または無効化するもの:

- 建造メニュー
- 木 / 石リソース表示
- 木 / 石自然物スポーン
- 大工小屋 / 作業小屋
- プレイヤーキャラ選択
- 制限時間クリア
- 任意配置型の建造

置き換えるもの:

- 建造物配置を固定スロット方式へ変更
- 武器強化をプレイヤーステータス強化中心から武器強化中心へ変更
- ボス後のラウンド進行
- 塗り状況ゲージ
- セル衝突 / 占有の扱い
- 画像 Scale 問題のルール化

## 推奨フェーズ

### Phase 0: 棚卸し

目的:

- 現行差分とリブート方針の衝突を整理する。
- まず既存ゲームが壊れていない状態を確認する。

作業:

- `git status` と直近コミットを確認。
- 建造物サイズ調整の現在地を確認。
- `05_Game.unity` と `90_GameplayTest.unity` のどちらを主検証にするか確認。
- Console Error と Compile を確認。

主な対象:

- `Assets/AreaSurvivors/Scenes/05_Game.unity`
- `Assets/AreaSurvivors/Scenes/90_GameplayTest.unity`
- `Assets/AreaSurvivors/Scripts/Game/GameManager.cs`
- `Assets/AreaSurvivors/Scripts/Game/BuildPlacementController.cs`

### Phase 1: 不要機能の停止

目的:

- リブート仕様で不要になった機能を、削除ではなくまず非表示 / 無効化する。
- 大きな破壊を避ける。

作業:

- ナイト以外のキャラ選択を無効化。
- 建造メニューを非表示。
- 木 / 石 HUD を非表示。
- 木 / 石自然物スポーンを停止。
- 大工小屋 / 作業小屋を未使用化。
- 制限時間クリアを停止し、中心塔破壊 / ボス討伐へ寄せる。

主な対象:

- `GameManager.cs`
- `BuildPlacementController.cs`
- `GameHudController` 系
- `GameConfig.asset`

### Phase 2: 固定建造物スロット化

目的:

- プレイヤーが任意配置する建造ではなく、スキルツリーで決まった場所に出る方式へ変える。

作業:

- `FixedBuildingSlot` 相当を現行側へ追加。
- バリスタ1個目は左上、2個目は右上、など固定配置。
- 建造物 Prefab は既存を流用。
- スキル取得時に該当スロットを Active 化。
- 配置済みセルを通行不可にする。

主な対象:

- `BuildPlacementController.cs`
- `BuildingUpgradeController.cs`
- 既存建造物 Prefab
- `TileGrid` / 通行判定まわり

### Phase 3: HUD整理

目的:

- 新仕様に必要な HUD だけに絞る。

作業:

- 武器パネルを削除または非表示。
- 建造メニューを削除または非表示。
- 木 / 石所持パネルを削除または非表示。
- 塗り状況ゲージを追加。
- 既存 XP / レベル表示は流用。

注意:

- HUD 全体を再生成しない。
- 必要な対象 UI だけを差し替える。

### Phase 4: 塗り / セル占有整理

目的:

- リブートで整理した「セル側に集約する」方針を現行へ移植する。

作業:

- 青 / 赤 / 無色セルの集計。
- 塗りゲージの差分更新。
- 赤床でプレイヤー速度 50% 低下。
- 青床で敵速度 50% 低下。
- 武器による青塗り。
- 敵による赤塗り。
- 建造物配置セルの通行不可化。

注意:

- 既存の塗り処理を全面削除しない。
- まず既存処理の責務を確認してから、必要部分だけ移す。

### Phase 5: 武器仕様の新仕様化

目的:

- ナイト1人 + 武器追加 / 強化方式へ寄せる。

作業:

- 初期武器はスラッシュのみ。
- レベルアップ選択肢をスラッシュ / 弓 / 火の玉強化へ変更。
- スラッシュは近距離、高威力、強ノックバック、押した敵の足元を青塗り。
- 弓は自動遠距離攻撃、着弾敵の足元を青塗り。
- 火の玉は通過セルと爆発範囲を青塗り。

流用候補:

- 既存の `WeaponLevelDefinition`
- 既存のレベルアップ UI
- 既存の攻撃 / Projectile 系

### Phase 6: 敵出現 / ラウンド仕様調整

目的:

- 制限時間制をやめ、ボス討伐で進む方式にする。

作業:

- 10秒ごとの出現方向変更は維持。
- 0:30 エリートイノシシ。
- 1:00 オークへ切り替え。
- 1:30 エリートオーク。
- 2:00 オークキング。
- ボス中はタイマー停止、赤表示。
- 初回ボス討伐は一旦クリア。
- 2回目以降はラウンド2へ連続進行。
- ラウンド2は Goblin / Ogre / GoblinLord。

主な対象:

- `GameManager.cs`
- `GameConfig.cs`
- `EnemySpawner` 系
- ボス討伐 / 結果処理

### Phase 7: 画像 Scale 問題の収束

目的:

- 現行で問題になっていた建造物の通常画像 / アップグレード画像サイズ差を安定させる。

作業:

- Prefab の Scale / Rotation 方針を確定。
- 実行時補正を残すか、画像加工へ寄せるか判断。
- `BuildingPrefabLayoutBuilder` の責務を整理。
- Prefab Validator 追加または拡張。

注意:

- ユーザーの希望は最終的に Scale `1` / Rotation `0`。
- 現行改修では、すぐに全撤廃せず、まず見た目崩れを止める。

## 最初にやるべきこと

別チャットで開始する場合、最初の依頼文は以下を推奨。

```text
AreaSurvivors現行改修に戻します。
Docs/RebuildPlan.md を読んで、まず Phase 0 の棚卸しから進めてください。
未コミット変更は前チャットでコミット済みの前提です。
まず建造物サイズ/アップグレード画像まわりの現在地と、05_Game / 90_GameplayTest の検証状態を確認してください。
```

## 作業時の注意

- 既存未コミット変更は勝手に戻さない。
- Scene / Prefab 全体を安易に再生成しない。
- `GameManager.cs` は大きいため、広域改修前に責務を絞る。
- `.unity` / `.prefab` は全文読みではなく Reporter / Validator / targeted search を優先する。
- Obsidian 外部記憶は、ユーザーが履歴確認・記録・締め作業を明示した時だけ使う。
- 日本語で報告する。
