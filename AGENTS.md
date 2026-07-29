# AGENTS.md

AreaSurvivors全体に適用する常時ルール。タスク固有の手順は該当する`Docs/AgentRules/*.md`だけを読む。

## Always

- ユーザーへの説明、作業報告は日本語で行う。
- 通常作業の開始時は`ctx/current.md`を読み、目的、重要判断、直近検証、TODO／Blockerを引き継ぐ。更新は目的・方針の変更時、引き継ぎ時、作業終了時に限り、完了履歴は`ctx/archive/`へ移す。
- 積み残しがある場合は最終回答へ`TODO`として明記し、未完了を曖昧にしない。
- 既存の未コミット変更はユーザーまたは前作業のものとして保護する。手作業の編集は`apply_patch`を使い、破壊的なGit操作・削除・広い上書きは明示依頼または承認なしに行わない。
- UI／HUD／Scene上の見た目はユーザーが実機確認する。ユーザーがその作業で明示依頼しない限り、CodexはPlay Mode開始、GUI操作、キー入力、画面遷移、見た目確認用スクリーンショット取得を行わない。
- Scene／Prefabに保存された位置、サイズ、Sprite、Collider、Scale、Rotation、参照をユーザー調整の正とし、Runtime、Editor Menu、Setup、Migration、Validatorから固定値で上書きしない。
- 静的GameObject／UI／VisualはSceneまたはPrefabへ保存し、動的オブジェクトはPrefab参照から生成する。Runtimeで静的構成を新規生成・差し替えしない。
- サブエージェントはユーザーがその作業で明示した場合だけ使用する。
- Shell commandは、対応している限り`rtk`を共通実行入口として使用する。
- タスク固有の作業を始める前に、下表で一致する詳細ルールだけを読む。分類が明確なら`rule-router.ps1`は実行しない。

## Task Routing

| 対象 | 読むファイル |
|---|---|
| モデル・コンテキスト・AGENTS整理 | `Docs/AgentRules/model-and-context.md` |
| UI／HUD | `Docs/AgentRules/ui-and-hud.md` |
| 画像・Prefab Visual・素材 | `Docs/AgentRules/assets-and-visuals.md` |
| 攻撃・弾・爆発・敵Animation | `Docs/AgentRules/combat.md` |
| Map・Scene・Unity検証 | `Docs/AgentRules/map-and-testing.md` |
| C#／PowerShell構造調査・検索・読取・diff・Token計測 | `Docs/AgentRules/token-tools.md` |
| Graphify | `Docs/AgentRules/graphify-pilot.md` |
| 再現するTool不具合・情報漏洩・データ破損・Unity状態異常 | `Docs/AgentRules/command-failure-playbook.md` |
| 締め作業 | `Docs/AgentRules/closeout.md` |

## Failure Boundary

- no-match、正常なGuard拒否、引数・Path・patch不一致は状態を変えない診断結果として扱い、表示された正式契約を確認して限定修正する。これらを障害履歴へ自動記録しない。
- 同じ入力で再現するTool不具合、秘密情報の露出、データ破損、Unity／Editor状態異常では状態変更を止め、`command-failure-playbook.md`に従う。
- コード、Wrapper、Validator、Tool Schemaで再発を防止できた事象は、同内容の説明を`AGENTS.md`や障害記録へ重複保存しない。

## Project Facts

- Unity: `2022.3.62f3`
- Main Scene: `Assets/AreaSurvivors/Scenes/05_Game.unity`
- Gameplay Test Scene: `Assets/AreaSurvivors/Scenes/90_GameplayTest.unity`
- 生成済みゲーム用Sprite: `Assets/AreaSurvivors/Sprites/Generated`

## Maintenance

- このファイルには、常時必要なユーザー意図、自動化できない安全境界、ルーティング、安定したProject Factsだけを置く。
- コマンド構文、個別障害事例、数値上限、個別Validator名、プロパティの網羅列挙、サブエージェントの操作手順は追加しない。
- 機械的に検出・拒否できる規則はコードと自己テストへ実装し、同じ説明を常時ルールへ残さない。
- 動的なブランチ、進行状況、直近検証、TODOは`ctx/current.md`を正とする。
