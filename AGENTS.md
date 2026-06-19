# AGENTS.md

AreaSurvivors リポジトリで作業するエージェント向けの運用ルールです。このファイルはリポジトリ全体に適用します。

## Communication

- ユーザーへの説明、作業報告、Obsidian記録は日本語で行う。
- 作業中は短く状況を共有し、長引く調査や判断分岐が出たら先に報告する。
- 仕様判断が必要な新機能は、実装前に認識を合わせる。
- ファイル参照やコマンド結果は、重要な点だけを簡潔に伝える。

## Project Basics

- Unity: `2022.3.62f3`
- 主要Scene: `Assets/AreaSurvivors/Scenes/05_Game.unity`
- Gameplay Test Scene: `Assets/AreaSurvivors/Scenes/90_GameplayTest.unity`
- 開発ブランチ: `feature/01_GameSystemInit`
- 生成済みゲーム用Spriteは `Assets/AreaSurvivors/Sprites/Generated` に統一する。`Assets/AreaSurvivors/Resources/Generated` を新規追加しない。

## Obsidian Memory

- 作業開始時やユーザーが履歴・ルール・記憶の読み込みを依頼した場合は、Obsidianの外部記憶を読む。
- 基本の読み取り順は次の通り。
  1. `Knowledge/mistakes.md`
  2. `Projects/area-survivors-current.md`
  3. `Knowledge/area-survivors-memory-rules.md`
  4. Unity検証や再生成に触れる場合は `Knowledge/area-survivors-unity-workflow.md`
  5. 表示、画像、アート調整では関連するPreferences/Knowledgeも読む
- Obsidianを読んだ、または書いた場合は、どのノートを扱ったかユーザーへ明示する。
- ユーザーが「締め作業」「作業終了」「今日の作業終了」「Obsidianへ記録」「コミット＆プッシュ」と依頼したら、`area-survivors-closeout` skill を使い、AreaSurvivors本体と `codex-external-memory` の両方を対象にする。
- 締め作業ではObsidianへ作業履歴を記録するだけでなく、その日の作業で判明した注意点、再発し得るミスの防止策、禁止事項、ユーザーのこだわりポイント、今後の判断基準を確認する。
- 上記の内容が今後のエージェント全体に効くルールなら、Obsidianだけでなくこの `AGENTS.md` にも追記・更新する。単発の履歴や一時的な状況はObsidianへ残し、恒久的な作業ルールだけを `AGENTS.md` に入れる。

## Core Architecture Rules

- Scene/Prefabとゲーム処理を疎結合にする。
- ユーザーがEditor上で調整したいものは、SceneまたはPrefabを正とする。
- Runtime側は、既存オブジェクトへのバインド、値更新、ボタン接続、表示/非表示の切り替えに留める。
- 既にSceneやPrefab上にある `RectTransform`、Sprite、Collider、Scale、Rotationを、実行時コードで固定値へ戻さない。
- HUD全体、Scene全体、Gameplay Test Scene全体を安易に再生成しない。必要な対象だけを変更する。

## HUD And UI

- HUD、ロビー、建造メニュー、ステージ表示、撃破数表示、資源表示、ステータス表示、アイコンは原則Scene上へ直接配置する。
- HUDの画像、アイコン、`Source Image` はScene上で設定する。GameManagerなどの実行時処理で作成・差し替え・サイズ補正しない。
- HUDの位置調整はユーザーがEditorで行う前提。正規化ツールや固定座標上書きで位置を戻さない。
- HUDを変更するときは、`05_Game.unity` の既存兄弟要素の `RectTransform` を確認し、現在の配置を基準にする。
- 新規UIをどうしてもフォールバック生成する場合も、Sceneに存在する要素の位置・サイズは上書きしない。

## Prefabs, Sprites, And Visuals

- 建造物、アップグレード後表示、建造中表示、HUD画像、建造メニュー画像などの静的VisualはPrefab/Sceneに参照を持たせる。
- 静的Visualのために `GeneratedSpriteLoader.Load` でSpriteを実行時に当てはめない。
- `GeneratedSpriteLoader` は、歩行アニメ、弾、動的UI、マップ外画像、地面バリアントなど、実行時に選択が必要なものへ限定する。
- 画像差し替え時はPNGだけでなく、Prefab参照、Scene参照、TilePalette、Editor生成ツール、`GeneratedSpriteCatalog.asset`、古いSprite/Source/Prefab/Tile/Metaを確認する。
- 建造物画像は、背景除去、可視範囲トリミング、占有セル横幅 `セル数 * 64px` に合わせたアスペクト比維持リサイズを行う。高さはセルに無理に収めず、Prefabで下端と横幅を合わせる。
- Sprite比率や下端ずれを、RuntimeのScale/Rotation/Y補正で直そうとしない。
- `PaperMeshVisual.OnValidate` ではMesh/Renderer変更を直接実行せず、必要ならEditorの遅延実行で反映する。Awake/OnValidate中のSendMessage警告を出さない。

## Collision And Combat

- 見た目と当たり判定が一致すべき攻撃は、調整可能なColliderを優先する。
- Knightの斬撃など、画面上の範囲が重要な攻撃で隠れた `OverlapBoxAll` / `OverlapCircleAll` 判定を残さない。
- 武器の範囲が広がる場合は、当たり判定だけでなく見た目のサイズも追従させる。
- 火球や爆発のVisual Scaleを爆発半径に直結させない。見た目の大きさとダメージ範囲は必要に応じて別管理にする。
- 着弾時のPixelBurst系バーストは通常攻撃では不要。負荷や視覚ノイズを増やさない。

## Map And Testing Scenes

- `05_Game.unity` のGround TilemapはSceneに全セル保存せず、`TileGrid.Build()` の実行時生成を正とする。
- `90_GameplayTest.unity` は `05_Game.unity` のコピーにしない。空に近いBootstrap Sceneとして維持する。
- GameplayTestはScenario AssetとBootstrapで再現する。通常プレイでランダム発生を待つ検証は避ける。
- Scenario切り替えでScene差分を出さない。Scenario選択は `EditorPrefs` を使う。

## Assets And Skills

- AreaSurvivorsの画像素材追加・差し替えでは `area-survivors-asset-import` skill を使う。
- 攻撃、弾、爆発、戦闘演出では `area-survivors-attack-animation` skill を使う。
- 敵アニメーション取り込みでは `area-survivors-enemy-animation-import` skill を使う。
- 取得画像をそのままゲームに使わない。原本は `Assets/AreaSurvivors/Sprites/External/*Source.png` に残し、背景透過、トリミング、解像度調整、既存資産とのサイズ比較、Unity Importer設定を行った処理済みPNGを使う。
- HUD画像や建造メニュー画像を追加する場合は、現在のScene上の既存パネル/スロットを基準に配置する。

## Validation

- 通常の標準検証は、コード確認、Unity Compile 1回、関連GameplayTest 1件を目安にする。
- 大規模変更、再発バグ、見た目確認が必要な変更、ユーザー指定がある場合だけ完全検証を行う。
- よく使う確認:
  - `unicli exec Compile`
  - `unicli exec Console.GetLog --logType Error --maxCount 30`
  - `git diff --check`
- UniCLIやUnity検証が止まったように見える場合は、同じ呼び出しを繰り返す前に、Unityの状態、プロジェクトロック、ログ、実行中コマンドを確認する。

## Recurring Mistakes To Avoid

- HUD項目やHUDアイコンをPlay中に動的生成しない。
- Scene上にあるHUDの位置やサイズをRuntimeで正規化・固定値上書きしない。
- ロビーやメニューを、Editorで調整したいUIなのにRuntime生成中心にしない。
- ユーザーがPrefabで調整するColliderをRuntimeで再設定しない。
- 建造物やアップグレード画像の比率ずれをRuntime Scale/Rotateで補正しない。
- `Resources/Generated` と `Sprites/Generated` を二重管理しない。
- 外部メモリの最新履歴・ルール確認を依頼されたとき、AreaSurvivors本体だけ見て `codex-external-memory` を読まない状態で進めない。
- 巨大なUnityコマンド一覧、Scene全文、広すぎる検索結果を不用意に読み込まない。対象ファイル、検索語、行数を絞る。

## Git And File Editing

- 既存の未コミット変更はユーザーまたは前作業のものとして扱い、勝手に戻さない。
- 手作業のコード編集は `apply_patch` を使う。
- 破壊的なGit操作や削除は、明示依頼または承認なしに行わない。
- 継続的に使うブランチ名、クラス名、プロジェクト固有識別子に `Codex` を含めない。

## Token And Verification Efficiency

- UIやSceneの見た目調整では、まず設計・座標表・グリッド・Validatorなどの機械検証で崩れを潰してから、スクリーンショット確認を行う。
- スクリーンショット確認は「初回」「最終」「機械検証で判断できない時」に絞る。毎回の微調整ごとに高解像度スクリーンショットを読まない。
- スクリーンショットが必要な場合も、可能なら低解像度または必要範囲だけで確認し、2560x1440など高解像度画像の反復確認を避ける。
- Unity Scene YAMLや巨大Prefabに対して不用意に `git diff --check` をかけない。Unity生成の空フィールドで大量出力になりやすい。基本はコード対象に限定し、SceneはUnity検証・専用Validator・最終スクリーンショットで確認する。
- `git diff`、`rg`、`Get-Content`、UniCLI出力は対象ファイル、検索語、行数を絞る。巨大なScene全文、広すぎる検索結果、長大な警告一覧を読み込まない。
- RTKが使える環境では、広い `git status`、`git diff`、`git log`、`rg`/`grep`、長いテスト/ログ確認は `rtk git status`、`rtk git diff`、`rtk grep`、`rtk test` のようにRTK経由を優先する。
- RTK出力は要約されるため、Scene/Prefab/YAMLの精査や正確な差分確認が必要な場合は、対象ファイルを絞って通常の `git diff -- <path>` や専用Validatorで確認する。
- 広い `git diff` / `git status` / `rg` / `Get-Content`、Scene/Prefab/YAML全文、Unityログ、Obsidian長文、スクリーンショット反復などでトークン消費が大きくなりそうな場合は、実行前に軽量ルートを自動で選ぶか、必要なら短く提案してから進める。
- 高トークン化を検知した場合の優先順は、`Compact Project Snapshot`、RTK、対象パス指定、専用Validator/Reporter、必要範囲だけのログ/差分確認、最後に限定的な全文確認とする。
- UI配置変更は、個別に動かして都度スクリーンショットを見るのではなく、座標表やグリッド定義をまとめて決めて一括反映し、その後Validatorと最終スクリーンショットで確認する。
- スキルツリーは `SkillTreeLayoutValidator` のようなEditor検証を先に使い、ノード重なり・リンク角度・重複ID・前提不整合を検出してから目視確認する。
- UniCLI `Eval` に複雑なC#コードや引用符を多く含む処理を直接渡さない。Scene操作、Validator実行、移行処理などは、最初から短い一時Editor Runner/Migratorを作成し、`AreaSurvivors.SomeRunner.Run();` のような単純なEvalで呼び出す。作業後はRunner/Migratorと `.meta` を削除する。
- `git status` は必要時のみ実行し、可能なら `git status --short -- <対象パス>` のように対象を絞る。広い作業ツリーで全体statusを読むと、過去作業の大量変更でトークンを消費しやすい。
- `git diff` は原則として対象ファイルを指定し、必要な場合も `--name-only`、`--stat`、`Select-Object -First` などで出力を絞る。広いScene差分や過去作業を含むdiffを不用意に読まない。
- Unity SceneやPrefabを含む作業では、差分全文よりも専用Validator、Compile、Console Error確認、対象オブジェクト検索を優先する。Scene YAML全文確認は最終手段にする。
- 長いスレッドでトークン消費が大きくなった場合は、新規チャットへ移ることを提案し、`AGENTS.md`、Obsidian要点、現在の未完了タスク、直近の検証結果だけを読み込んで続行する。
- 大規模作業は「調査」「Scene/Prefab反映」「コード削除/実装」「検証」の段階ごとに出力を絞る。各段階で広い検索・広いdiff・スクリーンショット確認を重ねない。
