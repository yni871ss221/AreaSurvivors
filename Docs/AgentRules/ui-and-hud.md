# UI And HUD Rules

- HUD、ロビー、建造メニュー、ステージ表示、撃破数表示、資源表示、ステータス表示、アイコンは原則Scene上へ直接配置する。
- HUD、スキルツリー、ロビー、建造メニューなど、静的に存在するUI/アイコン/画像は必ずScene上に配置し、`Source Image` もScene上の serialized reference を正とする。
- 実行時コードではScene/Prefab参照済みUIの値更新、表示/非表示、色変更だけを行う。Sprite差し替え、RectTransform補正、UIオブジェクト生成、アイコン生成をしてはいけない。
- `GameManager` / `GameHudController` / `UpgradeScreen` などで `CreatePanel`、`CreateText`、`Ensure*`、`new GameObject`、`AddComponent` を使って新規HUD/UIを生成してはいけない。
- ゲーム実行中のオブジェクト配置は禁止。静的オブジェクトはSceneへ直接配置し、動的オブジェクトは必ずPrefab化してPrefab参照から生成する。
- SceneやPrefab上の `RectTransform`、Sprite、Collider、Scale、RotationをRuntimeで固定値へ戻さない。
- HUD画像、アイコン、`Source Image` はScene上で設定する。GameManagerなどの実行時処理で作成・差し替え・サイズ補正しない。
- HUDの位置調整はユーザーがEditorで行う前提。正規化ツールや固定座標上書きで位置を戻さない。
- HUD変更時は `05_Game.unity` の既存兄弟要素の `RectTransform` を確認し、現在の配置を基準にする。
- 新規UIのフォールバック生成は禁止。必要なUIはSceneに追加してから参照をつなぐ。
- UI配置変更は、座標表やグリッド定義をまとめて決めて一括反映し、その後Validatorと最終スクリーンショットで確認する。
