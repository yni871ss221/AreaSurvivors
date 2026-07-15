# UI And HUD Rules

## 進化武器表示

- レベルアップ候補、通常HUD、一時停止詳細、武器図鑑の進化アイコンはScene/Prefabへ通常用と進化用を両方配置し、Runtimeではactiveと色だけを切り替える。進化獲得時にSpriteをロードして差し替えない。
- 進化候補のバウンスは既存Button/PanelのRectTransformを動かさず、Sceneへ追加した専用VisualのScene-authored基準Scaleだけをアニメーションする。
- 通常HUDの進化強調色は各slotがScene参照したアイコン背景と情報背景へ適用し、通常武器へ戻す時はSceneから取得した既定色へ戻す。

- 最重要: HUD/静的UIのレイアウトはScene/Prefabが唯一の正。既存オブジェクトに対して、Runtime、Editor Menu、Setup、Rebuild、Restore、Normalize、Validator、Importer、Migrationのどれであっても `RectTransform.anchorMin`、`anchorMax`、`pivot`、`anchoredPosition`、`sizeDelta`、`offsetMin`、`offsetMax`、`Transform.localPosition`、`localScale`、`localRotation`、`rotation` を書き換えてはいけない。
- `Area Survivors/Rebuild/*`、`AreaSurvivors/Setup/*` などのEditorメニューで既存HUDを並べ直す処理は禁止。HUDに関わるEditorメニューは、実行前にコードパスを読み、既存HUDの位置、サイズ、Scale、Rotation、Spriteを変更しないことを確認できない限り実行しない。
- `Create*Hud`、`Restore*Hud`、`Rebuild*Hud`、`Ensure*Panel`、`SetRect`、`SetAnchored`、`Stretch`、`Normalize*` という名前の処理で既存HUDを触らない。既存HUDに対して許可されるのは、数値表示、表示/非表示、色、透明化用コンポーネント、参照フィールドなど、レイアウトを変えない変更だけ。
- HUDの新規作成や初期配置が本当に必要な場合は、事前に「どのScene/Prefabに何を追加するか」「既存RectTransformを一切触らないこと」「実行前後のレイアウト差分確認方法」をユーザーへ説明し、明示承認を得た一回限りの移行として行う。通常作業のついでに自動生成、復元、再配置しない。
- 既存HUDの見た目がおかしい場合でも、コードで座標・サイズを戻して解決しない。まずScene上の現在値をReporterで確認し、ユーザーがEditorで直した値を尊重する。
- HUD/静的UIに関わるEditorコードを追加・変更したら、Unityメニュー `Area Survivors/Validate/HUD Layout Mutation Guard` を手動実行する。検出された `anchoredPosition`、`sizeDelta`、`localScale`、`localRotation`、`SetRect`、`SetAnchored` などは、既存HUDに対する書き換え入口として扱い、削除またはScene/Prefab参照へ移す。
- HUD、ロビー、建造メニュー、ステージ表示、撃破数表示、資源表示、ステータス表示、アイコンは原則Scene上へ直接配置する。
- HUD、スキルツリー、ロビー、建造メニューなど、静的に存在するUI/アイコン/画像は必ずScene上に配置し、`Source Image` もScene上の serialized reference を正とする。
- 武器、レリック、建造物など種別ごとに異なるアイコン付きUIを複製追加するMigrationでは、名前やラベルだけでなく種別と期待Spriteの対応表から各`Image.sprite`を明示設定する。完了判定はオブジェクトの存在確認だけにせず、専用Validatorで全種別の期待Sprite一致まで検証する。
- 実行時コードではScene/Prefab参照済みUIの値更新、表示/非表示、色変更だけを行う。Sprite差し替え、RectTransform補正、UIオブジェクト生成、アイコン生成をしてはいけない。
- `GameManager` / `GameHudController` / `UpgradeScreen` などで `CreatePanel`、`CreateText`、`Ensure*`、`new GameObject`、`AddComponent` を使って新規HUD/UIを生成してはいけない。
- ゲーム実行中のオブジェクト配置は禁止。静的オブジェクトはSceneへ直接配置し、動的オブジェクトは必ずPrefab化してPrefab参照から生成する。
- SceneやPrefab上の `RectTransform`、Sprite、Collider、Scale、RotationをRuntimeで固定値へ戻さない。
- HUD画像、アイコン、`Source Image` はScene上で設定する。GameManagerなどの実行時処理で作成・差し替え・サイズ補正しない。
- HUDの位置調整はユーザーがEditorで行う前提。正規化ツールや固定座標上書きで位置を戻さない。
- HUD変更時は `05_Game.unity` の既存兄弟要素の `RectTransform` を確認し、現在の配置を基準にする。
- 新規UIのフォールバック生成は禁止。必要なUIはSceneに追加してから参照をつなぐ。
- UI配置変更は、座標表やグリッド定義をまとめて決めて一括反映し、その後Validatorまで実行してユーザーへ実機UI確認を依頼する。ユーザーがその作業で明示依頼していないPlay Mode、GUI入力、画面遷移、スクリーンショット確認をCodex側で追加しない。
