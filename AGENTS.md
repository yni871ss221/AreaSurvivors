# AGENTS.md

AreaSurvivors リポジトリ全体に適用する低トークン運用の入口ルール。詳細は必要なカテゴリだけ `Docs/AgentRules/*.md` を読む。

## Must

- ユーザーへの説明、作業報告、Obsidian記録は日本語で行う。
- 通常作業の開始時は `ctx/current.md` を読み、現在の目的、重要判断、直近の検証結果、TODO/Blockerを引き継ぐ。作業中にこれらが変わった場合は `ctx/current.md` を更新し、作業終了時は未完了事項を明記する。`ctx/archive/` の詳細履歴は必要な場合だけ読む。
- 作業に積み残しがある場合（確認後に不要な機能を削除、確認後に水平展開、後続検証、未対応の派生修正など）は、最終回答に `TODO` として必ず明記する。積み残しを曖昧にしたまま次作業へ進まない。
- UI/HUD/Scene上の見た目を変更した後の実機表示確認はユーザーが行う。Codexは、Scene/Prefab反映、静的Reporter/Validator、必要なCompileまでで停止し、最終報告でユーザーへUI確認を明示的に依頼する。ユーザーがその作業でPlay Mode検証を明示依頼していない限り、CodexからPlay Modeを開始せず、GUIクリック・キー入力・リロール・画面遷移・スクリーンショット取得による見た目確認も行わない。
- UI確認のためだけにPlay Mode開始や画面操作を追加してはならない。実機確認後にユーザーから差分や不具合が報告された場合は、その報告と添付画像を正として限定修正し、再度ユーザーへ確認を依頼する。
- コマンド、ツール、Unity、Editor、ビルド、テストでエラー、タイムアウト、無応答、異常な長時間待機、想定外の戻り値・状態が発生した時点で、現在の実装・状態変更・手打ち再試行を停止する。別コマンド、別Shell、Eval、手動Editor操作、推測修正、フォールバックへ切り替えて回避することを禁止し、`Docs/AgentRules/command-failure-playbook.md` に従って元の方式の失敗境界と根本原因を最優先で確定する。
- 想定外挙動では、実行コマンドと引数、終了コード、経過時間、timeout有無、capture path、ログ、権限境界、Unity/Editor状態を証拠として保存する。原因確定後は、再発を入口で防ぐValidator/Wrapper/Reporter、`AGENTS.md`または詳細ルール、再発し得る知見のObsidian `Knowledge/`、ユーザー訂正を伴う場合の `Knowledge/mistakes.md` を更新し、限定自己テストが通るまで元の機能実装を再開してはいけない。原因を確定できない場合は推測で進めず、最終回答へ根拠付きの `TODO` またはblockerとして明記する。
- 既存の未コミット変更はユーザーまたは前作業のものとして扱い、勝手に戻さない。
- 手作業のコード編集は `apply_patch` を使う。破壊的なGit操作や削除は、明示依頼または承認なしに行わない。
- 通常作業開始時のObsidian外部記憶読み込みは行わない。履歴確認・記録・締め作業を明示された時だけ使う。ただし、上記の想定外挙動で確定した原因・再発防止策の追記は、このMustを恒常的な明示指示として実施する。
- Scene/Prefabとゲーム処理を疎結合にし、Editor調整したいものはSceneまたはPrefabを正とする。Runtimeで既存の位置、サイズ、Sprite、Collider、Scale、Rotationを固定値へ戻さない。
- HUD/静的UIの `RectTransform`、Transform位置、Scale、Rotation、Source Image、Collider、Sprite、サイズは、RuntimeだけでなくEditor Menu、Setup、Rebuild、Restore、Normalize、Validator、Importer、Migrationスクリプトからも既存Scene/Prefabへ上書きしない。既存HUDレイアウトはユーザーがEditorで調整したScene/Prefabを唯一の正とし、コードで戻す処理は絶対禁止。
- HUD/静的UIに関わるEditorコードを追加・変更した場合は、`Area Survivors/Validate/HUD Layout Mutation Guard` を手動実行し、危険なHUDレイアウト書き換えが検出されないことを確認する。検出された場合は機能追加より先に必ず除去する。
- 攻撃範囲、塗り範囲、当たり判定を示すArea/Range Visualは、Transform Rotation X/Yやカメラ回転、`PaperBillboard.faceCamera=true` で疑似パース補正しない。見た目、当たり判定、セル塗りは同じ半径・縦横比を基準にする。
- AnimatorでSpriteを切り替える戦闘Visual、落下物、着弾演出も、Transform Rotation X/Yや`PaperBillboard.faceCamera=true`を使用しない。PrefabのRotation X/Yを0とし、方向表現が必要な場合はZ回転だけを使う。Animator Controller、AnimationClip、表示サイズ、SpriteRenderer参照はPrefabへ直接保存し、Runtimeは再生開始だけを行う。
- 複数フレームのSprite切り替え、落下・着弾・爆発などEditor上で時系列とフレームを調整すべき戦闘Visual、プレイヤーの方向別歩行アニメーションは、Unity標準のAnimationClipとAnimator ControllerをPrefabへ直接設定する。Runtimeコードによるフレーム配列の時間切り替え、Sprite差し替え、Transform時系列アニメーションを新規実装しない。
- Animator移行の対象外は、画像を切り替えない単純な建造物バウンス、単発着弾Spriteの拡大フェード、敵被弾時の白Overlay、軌道・ホーミング・周回位置・ランダム配置、Area/Range形状、当たり判定・ダメージ時刻などゲーム処理に属するものとする。これらをAnimatorへ移す場合は、Editor調整価値とRuntime負荷の根拠を先に示し、ユーザーの明示了承を得る。
- Animator化したVisualは、Controller、Clip、SpriteRendererまたはAnimator適用対応済みの`PaperMeshVisual`、再生対象TransformをSceneまたはPrefabのserialized referenceとして保持し、Runtimeは状態Parameter設定または再生開始だけを行う。旧Runtimeフレーム切り替えを無効化したまま残さず、移行完了後に削除し、Animator/Prefab Validatorで旧Component・旧Sprite切り替え経路の不在を確認する。
- Area/Range VisualまたはAnimator戦闘Visualを追加・変更した場合は、全武器Prefabを対象とする`Area Survivors/Validate/Combat Visual Rotation Guard`を実行し、Rotate X/Yと`PaperBillboard.faceCamera`の違反が0件であることを確認する。
- ゲーム実行中にGameObject/UI/静的Visualを新規配置・生成・差し替えしない。静的オブジェクトはSceneへ直接配置し、動的オブジェクトはPrefab化してScene/Prefab参照から生成することを絶対ルールとする。
- スキルツリー、HUD、建造メニューなどのアイコンや `Source Image` はScene/Prefab上の参照を正とし、RuntimeコードでSpriteを差し替えない。
- HUD全体、Scene全体、Gameplay Test Scene全体を安易に再生成しない。必要な対象だけ変更する。

## Low Token First

- 単純なUI追加、検索、差分確認、小修正は軽量モデル・低推論で始める。設計判断、原因不明バグ、Scene/Prefab移行、戦闘仕様変更だけ高推論に上げる。
- ユーザーがその作業で「時間をかけてもよい」「徹底検証してよい」等を明示していない限り、作業規模を問わず Unity Compile は最大5回とする。Play Mode開始は上記Mustに従ってユーザーがその作業で明示依頼した場合に限り、最大2回を上限とする。失敗した実行、再試行、確認目的の再実行も回数へ含める。
- Unity Compileの6回目またはPlay Modeの3回目が必要になった時点で実装・修正を止める。直前までの結果、想定外の事象、未確定の仮説を整理し、コードを変更せず原因調査を優先する。原因を根拠付きで確定できず、ユーザーの追加許可もない状態でUnity Compileの6回目またはPlay Modeの3回目を実行してはいけない。
- 想定外の結果が出た後に、仮説だけでコード修正→Compile→Play Modeを反復することを禁止する。先にEditor設定、Play/Compile状態、Active Scene、Scene/Prefab参照、ライフサイクル、Console、テスト経路が本番経路と同一かを読み取り確認する。
- 同種のコマンド失敗、引数ミス、エスケープ不良、検証不能が2回発生した場合、それ以上の手打ち再試行を禁止する。`Tools/TokenUsage` のWrapper、限定Editor Runner、Reporter、Validator、または再利用可能な検証コマンドとして部品化し、以後はその入口だけを使う。
- そのタスクで初めて使うWrapperは、実行前に対象`*.ps1`先頭の`param(...)`または`Docs/AgentRules/token-tools.md`の正式用例を確認する。`safe-search`の`-Path`、`safe-unity-search`の`-Query`など、別Wrapperの引数名・glob・Modeを転用または推測してはならない。サブエージェントへの委譲文にもこのPreflightを含める。
- Windows環境でコマンドまたはPowerShell WrapperをRTK配下から呼ぶ場合は、このリポジトリで存在確認済みの実行ファイルだけを使う。`pwsh`、`head`、`tail`など別環境の実行ファイル名を推測で指定せず、出力制限や抽出は`safe-read.ps1`、`safe-search.ps1`等の既存Wrapperへ任せる。新しい実行ファイルが必要なら先に存在確認と正式用例の確認を行う。引用符を含む複雑な検証式を`powershell.exe -Command`へ埋め込まず、既存の`-File` Wrapperまたは限定Validatorへ分離する。
- 文言、色、整列、数値、数px単位の位置など、直前に確認済み機能への軽微な追調整はFast Pathで扱う。対象特定 → 同種変更の一括適用 → Unity Compile 1回 → Console Error確認1回 → ユーザーへUI確認依頼を上限目安とし、Codex側でPlay Mode、リロール、スクリーンショット、Scene往復を追加実行しない。
- 同じScene/UIの複数要素や複数プロパティを変更する場合、`unicli` を1要素・1プロパティごとに反復しない。最初に対象と調整値を表にまとめ、明示依頼された対象だけをSceneへ一括反映して保存は1回にする。
- Runtime Componentのserialized field、型、旧Animation Componentを削除する前に、対象Runtimeフォルダだけでなく`Assets/AreaSurvivors/Editor`のBootstrap、Setup、Migrator、Validatorからの直接参照を限定検索する。削除対象を設定・生成しているEditor経路は同じCompileバッチで新構成へ更新し、Missing memberをCompile後に発見しない。
- 旧MonoBehaviourをPrefabから除去する移行では、原則として型と`.meta`が解決可能な状態でPrefab移行を先に完了してから旧Scriptを削除する。既にMissing Script化している場合は、Prefab全階層で欠損Componentを除去し、残数0、`SaveAsPrefabAsset`の非null戻り値、専用Validator成功markerを確認するまで移行完了と扱わない。
- `Menu.Execute`やEditor Menu Wrapperの終了コード0はMenu受付成功にすぎない。Migration/Validatorは処理末尾だけで成功markerを作成し、呼び出し側はmarkerの新規生成を必須確認する。終了コード、`executed: true`、Console成功文のいずれか単独で成功判定しない。
- Menu経由Validatorは `Tools/TokenUsage/invoke-menu-validator.ps1 -MenuPath <menu> -SuccessMarkerPath <project-relative-marker>` を使い、要求受付ではなく当該実行後のmarker新規生成で成功判定する。`safe-unity.ps1 -Action Menu` を単独で実行した場合は、同一のmarker確認なしに完了扱いにしてはならない。
- 権限確認やUnity連携がタイムアウトした場合は、モデル推論や実装不良と混同しない。Unity状態を確認して同じ操作を最大1回だけ再試行し、成功後に検証範囲を追加で広げない。
- `functions.exec`が`Script running with cell ID ...`を返した場合、同じコマンドの継続取得は必ず`functions.wait(cell_id)`を使う。`collaboration.wait_agent`はエージェントmailbox待ち専用であり、command cellの完了待ちには絶対に使わない。cell結果を回収する前に同じコマンドを再発行しない。
- `functions.exec`内で`tools.exec_command`を呼ぶ時は、戻り値の`exit_code`だけでなく`session_id`も必ず出力または保存する。`exit_code`が未定義で`session_id`がある場合は同じsessionを`tools.write_stdin`で回収し、session_idを失った場合は同じコマンドを再発行せずTokenReportsの`capture_path`・`timed_out`・終了コードから失敗段階を確定する。
- 長時間のEditor Runnerは`invoke-unity-editor-runner.ps1 -Concise`を使い、会話へ成功payloadを展開しない。`functions.wait`は外側の専用ツールであり、`functions.exec`内の`tools.wait`として呼ばない。cell結果がcontext超過またはcell消失で回収不能なら同じRunnerを再発行せず、TokenReportsとSafe-Command captureから終了状態を確定する。
- Unityプロジェクト直下の`Temp/`はEditorによって実行中に削除され得るため、ダウンロードした外部Runtime、長時間処理の中間物、動画プレビュー、最終成果物を保存しない。外部RuntimeはUnity管理外の明示キャッシュ、最終成果物は`Docs/`などの安定した保存先を使う。
- Play Modeの開始、終了、状態確認は `Tools/TokenUsage/safe-unity.ps1 -Action PlayEnter|PlayExit|PlayStatus` だけを使い、`unicli exec PlayMode.*` や終了直後の `Editor.Status` を直接実行しない。`PlayExit` 成功後はその検証シーケンスのUnity操作を終了し、追加確認が不可欠な場合だけクールダウン後に `PlayStatus` を使う。
- 作業種別が曖昧な場合は `Tools/TokenUsage/rule-router.ps1 -Task "<依頼内容>"` で読む詳細ルールと中核ファイルを絞る。
- C#またはPowerShellの複数ファイルをまたぐ完全一致シンボルでは、実際にGraphifyを使う直前に限り`EnsureFresh`を1回使い、2シンボル間の経路は`Path`、影響候補は`Affected`を標準の第一手にする。grep/readだけで完結する作業や、同じfresh graphを続けて読む間は`EnsureFresh`を再実行しない。`Affected`の既定表示上限（20件または推定500 token）を解除せず、全件は`full_capture_path`で必要箇所だけ読む。fresh時は再構築しないため`Update`を毎回実行しない。`Explain`はcaller/callee等の直接近傍が必要な場合だけ使い、定義場所・実装内容だけなら`focused-search`／`safe-read`を先に使う。`graphify_verification_required: true`が出た場合は提示されたfallbackを実行して確認し、`GraphifyFallbackId`を削除しない。自然文BFS Query、正確な文字列・数値検索、Unity YAML/serialized reference確認には使わず、`Docs/AgentRules/graphify-pilot.md`のroutingに従う。
- 初手で `Assets/AreaSurvivors` 全体へ広域 `rg` をかけない。`safe-search.ps1 -FilesOnly`、`-HitSummary`、`focused-search.ps1` を先に使う。
- 複数の明示Pathを生の `rg` / `rtk rg` へ同時に渡さない。既知の実在Pathは1回につき1つだけ検索し、候補または未知のPathは先に `safe-search.ps1 -FilesOnly` で実在確認する。既知Pathと推測Pathを同じコマンドへ混在させることを禁止する。
- 読み取りは `safe-read.ps1 -Pattern <語> -Context <行数>` または `-StartLine` / `-EndLine` を優先する。
- RTK経由の同一PowerShell読み取りWrapperを並列起動しない。`safe-read-batch.ps1`は複数範囲を1回へまとめ、複数ファイルは直列実行して`-File`のWrapper境界と`-Path`対象を固定する。
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
- `safe-unity-search.ps1`はread-onlyの`rg`代替ではなく、Unity接続とEditor Menu Reporter実行を伴う。Unity/Menu実行禁止で委譲されたサブタスクでは使用せず、許可済みの通常ファイル検索と既存コード読み取りだけで調査する。
- Git worktreeは独立したC#、ドキュメント、読み取り調査へ使い、Scene/Prefab/asset/meta編集やUnity Editor最終検証は統合側の単一作業ツリーで行う。
- サブエージェントへは、目的、完了条件、編集可能ファイル、編集禁止ファイル、依存タスク、検証方法、返却形式を必ず渡す。範囲が曖昧なまま編集を開始させない。
- Leadが現行の実在パスを把握している場合は、サブエージェントの依頼文へその絶対またはworkspace相対パスを明記する。サブエージェントはSkillや過去知識の旧パス、一般的なフォルダ名から検索起点を組み立てず、未知のパスはLead指定の既存親ディレクトリから`safe-search.ps1 -FilesOnly`で確定する。
- サブエージェントの報告はLeadが根拠と差分を確認してから統合する。報告だけを根拠に未確認の修正を確定しない。
- 作業担当サブエージェントは、Leadの修正・許可・依存成果を待つために`wait_agent`を反復しない。待機が必要なら証拠と再開条件をfinalで返してidle/completedになり、Leadが条件解消後に`followup_task`で明示的に再開する。メッセージ待ちのrunning状態を維持して、長時間処理中に見える状態を作らない。
- `collaboration.wait_agent`を使う場合、`timeout_ms`はTool定義の許容範囲`10000`〜`3600000`だけを指定する。短いpoll目的で最小値未満を渡さず、即時状態確認には`list_agents`を使う。
- サブエージェントが`functions.exec`のcommand cellを持つ場合も、`functions.wait`でそのcellだけを回収する。`collaboration.wait_agent`へ置き換えたり、cell未回収のままfinal終了・依存コマンド再発行を行わない。
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
- コード構造探索Graphify Pilot: `Docs/AgentRules/graphify-pilot.md`
- コマンド失敗、タイムアウト、引数破損の原因調査: `Docs/AgentRules/command-failure-playbook.md`
- 締め作業、Obsidian、記憶運用: `Docs/AgentRules/closeout.md`
