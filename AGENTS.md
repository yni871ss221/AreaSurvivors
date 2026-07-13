# AGENTS.md

AreaSurvivors リポジトリ全体に適用する低トークン運用の入口ルール。詳細は必要なカテゴリだけ `Docs/AgentRules/*.md` を読む。

## Must

- ユーザーへの説明、作業報告、Obsidian記録は日本語で行う。
- 作業に積み残しがある場合（確認後に不要な機能を削除、確認後に水平展開、後続検証、未対応の派生修正など）は、最終回答に `TODO` として必ず明記する。積み残しを曖昧にしたまま次作業へ進まない。
- 既存の未コミット変更はユーザーまたは前作業のものとして扱い、勝手に戻さない。
- 手作業のコード編集は `apply_patch` を使う。破壊的なGit操作や削除は、明示依頼または承認なしに行わない。
- 通常作業開始時のObsidian外部記憶読み込みは行わない。履歴確認・記録・締め作業を明示された時だけ使う。
- Scene/Prefabとゲーム処理を疎結合にし、Editor調整したいものはSceneまたはPrefabを正とする。Runtimeで既存の位置、サイズ、Sprite、Collider、Scale、Rotationを固定値へ戻さない。
- HUD/静的UIの `RectTransform`、Transform位置、Scale、Rotation、Source Image、Collider、Sprite、サイズは、RuntimeだけでなくEditor Menu、Setup、Rebuild、Restore、Normalize、Validator、Importer、Migrationスクリプトからも既存Scene/Prefabへ上書きしない。既存HUDレイアウトはユーザーがEditorで調整したScene/Prefabを唯一の正とし、コードで戻す処理は絶対禁止。
- HUD/静的UIに関わるEditorコードを追加・変更した場合は、`Area Survivors/Validate/HUD Layout Mutation Guard` を手動実行し、危険なHUDレイアウト書き換えが検出されないことを確認する。検出された場合は機能追加より先に必ず除去する。
- 攻撃範囲、塗り範囲、当たり判定を示すArea/Range Visualは、Transform Rotation X/Yやカメラ回転、`PaperBillboard.faceCamera=true` で疑似パース補正しない。見た目、当たり判定、セル塗りは同じ半径・縦横比を基準にする。
- ゲーム実行中にGameObject/UI/静的Visualを新規配置・生成・差し替えしない。静的オブジェクトはSceneへ直接配置し、動的オブジェクトはPrefab化してScene/Prefab参照から生成することを絶対ルールとする。
- スキルツリー、HUD、建造メニューなどのアイコンや `Source Image` はScene/Prefab上の参照を正とし、RuntimeコードでSpriteを差し替えない。
- HUD全体、Scene全体、Gameplay Test Scene全体を安易に再生成しない。必要な対象だけ変更する。

## Low Token First

- 単純なUI追加、検索、差分確認、小修正は軽量モデル・低推論で始める。設計判断、原因不明バグ、Scene/Prefab移行、戦闘仕様変更だけ高推論に上げる。
- ユーザーがその作業で「時間をかけてもよい」「徹底検証してよい」等を明示していない限り、作業規模を問わず Unity Compile は最大2回、Play Mode開始は最大2回を標準上限とする。失敗した実行、再試行、確認目的の再実行も回数へ含める。
- CompileまたはPlay Modeの3回目が必要になった時点で実装・修正を止める。直前までの結果、想定外の事象、未確定の仮説を整理し、コードを変更せず原因調査を優先する。原因を根拠付きで確定できず、ユーザーの追加許可もない状態で3回目を実行してはいけない。
- 想定外の結果が出た後に、仮説だけでコード修正→Compile→Play Modeを反復することを禁止する。先にEditor設定、Play/Compile状態、Active Scene、Scene/Prefab参照、ライフサイクル、Console、テスト経路が本番経路と同一かを読み取り確認する。
- 同種のコマンド失敗、引数ミス、エスケープ不良、検証不能が2回発生した場合、それ以上の手打ち再試行を禁止する。`Tools/TokenUsage` のWrapper、限定Editor Runner、Reporter、Validator、または再利用可能な検証コマンドとして部品化し、以後はその入口だけを使う。
- 文言、色、整列、数値、数px単位の位置など、直前に確認済み機能への軽微な追調整はFast Pathで扱う。対象特定 → 同種変更の一括適用 → Unity Compile 1回 → 必要な代表表示1件 → Console Error確認1回を上限目安とし、確認済み経路のPlay Mode、リロール、スクリーンショット、Scene往復を重複実行しない。
- 同じScene/UIの複数要素や複数プロパティを変更する場合、`unicli` を1要素・1プロパティごとに反復しない。最初に対象と調整値を表にまとめ、明示依頼された対象だけをSceneへ一括反映して保存は1回にする。
- 権限確認やUnity連携がタイムアウトした場合は、モデル推論や実装不良と混同しない。Unity状態を確認して同じ操作を最大1回だけ再試行し、成功後に検証範囲を追加で広げない。
- 作業種別が曖昧な場合は `Tools/TokenUsage/rule-router.ps1 -Task "<依頼内容>"` で読む詳細ルールと中核ファイルを絞る。
- 初手で `Assets/AreaSurvivors` 全体へ広域 `rg` をかけない。`safe-search.ps1 -FilesOnly`、`-HitSummary`、`focused-search.ps1` を先に使う。
- 読み取りは `safe-read.ps1 -Pattern <語> -Context <行数>` または `-StartLine` / `-EndLine` を優先する。
- `.unity` / `.prefab` / `.asset` は本文diffや全文読みを避け、`safe-diff -SummaryOnly`、`safe-unity-search.ps1`、Reporter/Validatorを先に使う。
- プロジェクト肥大化や未参照候補を見る時は `project-weight-report.ps1`、GameManager分割候補は `game-manager-responsibility-report.ps1`、検証選択は `validation-preset.ps1` を使う。
- 長いスレッドで固定コンテキストが重くなったら、新規チャットへ移ることを提案し、`AGENTS.md`、未完了タスク、直近検証結果だけを引き継ぐ。

## Subagent Operation

- サブエージェントは、ユーザーがその作業で明示的に使用を指定した場合にのみ使う。規模や並列化効果にかかわらず、指定がない作業ではLeadが単独で対応する。
- メインスレッドをLead兼Integratorとし、要件、完了条件、ファイル所有権、統合、最終報告を管理する。
- ユーザーが使用を指定した場合は、原因不明バグ、独立した複数調査、ログ解析、実装後レビューを優先的な委譲対象とする。
- 同時に本番ファイルを編集するエージェントは最大2体とし、同じファイルや同じ機能領域を割り当てない。
- `explorer` は調査専用、`verifier` は独立検証専用とし、原則として本番ファイルを編集させない。
- `.unity`、`.prefab`、`.asset`、`.meta`、画像、音声の各対象は単一エージェントが排他的に所有する。同じScene/Prefabや参照関係の強いアセットを並列編集しない。
- `unity_ui_owner` だけがLeadから明示されたScene/Prefab/HUDを編集する。ユーザーがEditorで調整した配置や参照を他エージェントが変更しない。
- Git worktreeは独立したC#、ドキュメント、読み取り調査へ使い、Scene/Prefab/asset/meta編集やUnity Editor最終検証は統合側の単一作業ツリーで行う。
- サブエージェントへは、目的、完了条件、編集可能ファイル、編集禁止ファイル、依存タスク、検証方法、返却形式を必ず渡す。範囲が曖昧なまま編集を開始させない。
- サブエージェントの報告はLeadが根拠と差分を確認してから統合する。報告だけを根拠に未確認の修正を確定しない。
- 子エージェントによる再帰的な委譲は行わない。`.codex/config.toml` の `max_depth = 1` を維持する。

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
