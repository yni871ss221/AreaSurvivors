# AGENTS.md

AreaSurvivors リポジトリ全体に適用する低トークン運用の入口ルール。詳細は必要なカテゴリだけ `Docs/AgentRules/*.md` を読む。

## Must

- ユーザーへの説明、作業報告、Obsidian記録は日本語で行う。
- 作業に積み残しがある場合（確認後に不要な機能を削除、確認後に水平展開、後続検証、未対応の派生修正など）は、最終回答に `TODO` として必ず明記する。積み残しを曖昧にしたまま次作業へ進まない。
- 既存の未コミット変更はユーザーまたは前作業のものとして扱い、勝手に戻さない。
- 手作業のコード編集は `apply_patch` を使う。破壊的なGit操作や削除は、明示依頼または承認なしに行わない。
- 通常作業開始時のObsidian外部記憶読み込みは行わない。履歴確認・記録・締め作業を明示された時だけ使う。
- Scene/Prefabとゲーム処理を疎結合にし、Editor調整したいものはSceneまたはPrefabを正とする。Runtimeで既存の位置、サイズ、Sprite、Collider、Scale、Rotationを固定値へ戻さない。
- 攻撃範囲、塗り範囲、当たり判定を示すArea/Range Visualは、Transform Rotation X/Yやカメラ回転、`PaperBillboard.faceCamera=true` で疑似パース補正しない。見た目、当たり判定、セル塗りは同じ半径・縦横比を基準にする。
- ゲーム実行中にGameObject/UI/静的Visualを新規配置・生成・差し替えしない。静的オブジェクトはSceneへ直接配置し、動的オブジェクトはPrefab化してScene/Prefab参照から生成することを絶対ルールとする。
- スキルツリー、HUD、建造メニューなどのアイコンや `Source Image` はScene/Prefab上の参照を正とし、RuntimeコードでSpriteを差し替えない。
- HUD全体、Scene全体、Gameplay Test Scene全体を安易に再生成しない。必要な対象だけ変更する。

## Low Token First

- 単純なUI追加、検索、差分確認、小修正は軽量モデル・低推論で始める。設計判断、原因不明バグ、Scene/Prefab移行、戦闘仕様変更だけ高推論に上げる。
- 作業種別が曖昧な場合は `Tools/TokenUsage/rule-router.ps1 -Task "<依頼内容>"` で読む詳細ルールと中核ファイルを絞る。
- 初手で `Assets/AreaSurvivors` 全体へ広域 `rg` をかけない。`safe-search.ps1 -FilesOnly`、`-HitSummary`、`focused-search.ps1` を先に使う。
- 読み取りは `safe-read.ps1 -Pattern <語> -Context <行数>` または `-StartLine` / `-EndLine` を優先する。
- `.unity` / `.prefab` / `.asset` は本文diffや全文読みを避け、`safe-diff -SummaryOnly`、`safe-unity-search.ps1`、Reporter/Validatorを先に使う。
- プロジェクト肥大化や未参照候補を見る時は `project-weight-report.ps1`、GameManager分割候補は `game-manager-responsibility-report.ps1`、検証選択は `validation-preset.ps1` を使う。
- 長いスレッドで固定コンテキストが重くなったら、新規チャットへ移ることを提案し、`AGENTS.md`、未完了タスク、直近検証結果だけを引き継ぐ。

## Project Facts

- Unity: `2022.3.62f3`
- 主要Scene: `Assets/AreaSurvivors/Scenes/05_Game.unity`
- Gameplay Test Scene: `Assets/AreaSurvivors/Scenes/90_GameplayTest.unity`
- 開発ブランチ: `feature/01_GameSystemInit`
- 生成済みゲーム用Spriteは `Assets/AreaSurvivors/Sprites/Generated` に統一し、`Assets/AreaSurvivors/Resources/Generated` を新規追加しない。

## Detail Rule Files

- 中核ファイル表: `Docs/AgentRules/core-files.md`
- モデル、推論、長スレッド運用: `Docs/AgentRules/model-and-context.md`
- UI/HUD調整: `Docs/AgentRules/ui-and-hud.md`
- 画像、Prefab Visual、素材差し替え: `Docs/AgentRules/assets-and-visuals.md`
- 攻撃、弾、爆発、敵アニメーション: `Docs/AgentRules/combat.md`
- Map、Scene、GameplayTest、Unity検証: `Docs/AgentRules/map-and-testing.md`
- トークン削減ツール、検索、diff、ログ確認: `Docs/AgentRules/token-tools.md`
- 締め作業、Obsidian、記憶運用: `Docs/AgentRules/closeout.md`
