# UI And HUD Rules

- HUD、ロビー、建造メニュー、ステージ表示、撃破数表示、資源表示、ステータス表示、アイコンは原則Scene上へ直接配置する。
- SceneやPrefab上の `RectTransform`、Sprite、Collider、Scale、RotationをRuntimeで固定値へ戻さない。
- HUD画像、アイコン、`Source Image` はScene上で設定する。GameManagerなどの実行時処理で作成・差し替え・サイズ補正しない。
- HUDの位置調整はユーザーがEditorで行う前提。正規化ツールや固定座標上書きで位置を戻さない。
- HUD変更時は `05_Game.unity` の既存兄弟要素の `RectTransform` を確認し、現在の配置を基準にする。
- 新規UIをどうしてもフォールバック生成する場合も、Sceneに存在する要素の位置・サイズは上書きしない。
- UI配置変更は、座標表やグリッド定義をまとめて決めて一括反映し、その後Validatorと最終スクリーンショットで確認する。
