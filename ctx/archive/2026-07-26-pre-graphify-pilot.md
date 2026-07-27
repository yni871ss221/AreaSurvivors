# Archived Context (2026-07-26 before Graphify Pilot)

## Goal

GraphifyをAreaSurvivorsのコード探索へ導入した場合に、grep/readとCodexコンテキスト消費を削減できるか評価する。

## Findings

- 対象はC# 380ファイルで、複数ファイルをまたぐ呼び出し・依存・影響範囲探索にはGraphifyのASTグラフが有効と見込む。
- `.unity`、`.prefab`、`.asset`、`.shader`はGraphifyの主要なコード抽出対象外で、Unity serialized referenceは既存Reporter/Validatorを正とする必要がある。
- 正確な文字列・単一シンボル検索は既存`safe-search`/`focused-search`、構造探索はGraphify、最終確認は対象ソースreadという併用が適切。
- 初期導入はコードのみ・ローカルAST・出力上限付きCLI Wrapperとし、Docs/PDF/画像のLLM抽出、MCP、Codex自動hook、git hookは採用しない。
- Graphifyの公開削減率は他コーパスの値であり、AreaSurvivorsのクレジット削減はA/B計測しない限り確定できない。
- 2026-07-26時点でWindowsのnode identity/cacheに関する未解決issueがあるため、本採用前に隔離Pilotが必要。
- 既存`ctx/current.md`自体が868行・読み込み約1万トークンまで肥大化しており、Graphifyより先に現行セクションの読取範囲制限と履歴archive化が必要。

## Decision

- 条件付きでPilot導入を推奨する。grep/readの全面置換やGraphify自動installerによる`AGENTS.md`/hook変更は行わない。

## TODO / Blocker

- TODO: ユーザー承認後、コード限定Graphify Pilot、専用Wrapper、10問程度のA/B測定を実施する。
- TODO: `ctx/current.md`の旧履歴を`ctx/archive/`へ移し、通常読取を短い現行セクションへ限定する。
- Blockerなし。

## Next Action

- 評価結果を報告し、Pilot導入へ進むかユーザー判断を受ける。

---

# Archived Context

以下は過去作業の記録。明示的に必要な場合だけ読む。

# Current Task

## Goal

既存のリッチ大量召喚時負荷対策を基準に、計測条件を固定したうえで追加最適化を段階導入し、効果がない変更を撤回しながら性能・命中・演出回帰を検証する。

## Latest Completed (2026-07-26 Additional Performance Pass)

- `EnemySpawner.BeginStage()`は既存Coroutineを必ず停止し、無効ComponentではSpawnLoopを開始しない。`OnDisable()`とGameplay TestのSystem設定でも`StopSpawning()`を呼び、性能Scenario中の意図しない5体追加を根本停止した。
- 固定敵Scenarioだけ2秒ウォームアップ後に計測し、武器Scenarioは初撃を計測外へ追い出さないよう0秒にした。3回の固定敵計測はすべて`128→128`で、平均23.52 / 22.58 / 22.80ms、3回平均22.97msへ安定した。
- ウォームアップ後に`CombatPerformanceDiagnostics.BeginRecording()`がNoFeedbackフラグを初期化する回帰を検出し、`ApplyModeOverrides()`を計測開始時に再適用するよう修正した。フロストNoFeedbackでPopup/HitFlash生成0、命中40を確認した。
- `EnemyController`へActive Enemy Registryを追加し、実ゲーム側の`FindObjectsOfType<EnemyController>()`を武器、Projectile、バリスタ、中央塔砲、ステージ遷移、Spawnerから除去した。128体の同一走査は旧全Object探索2033.81µsに対しRegistry 23.65µsで約86倍高速。最終再測定でも1545.86µs対22.71µs（約68倍）を再現し、固定敵Baselineは22.97msから20.99ms、最終20.77msへ約9.6%改善した。
- フロストNoFeedbackは`128→128 / avg 24.79ms / p95 35.97ms / areaHits 40 / Popup・HitFlash生成0`。旧最終`32.32ms / 55.07ms / hits 39`から平均約23.3%、p95約34.7%改善した。
- エクスカリバーは射程・表示率が変わらないフレームのSector Mesh再構築だけを停止した。10秒でShapeは360回前後から最終7回、推定managed payloadは751,680 bytes前後から14,616 bytesへ約98%削減した。セル塗りは敵の塗り返しに対する占有挙動を変えないよう従来どおり毎フレーム維持する。
- エクスカリバー最終持続計測は`128→128 / avg 27.43ms / p95 38.70ms / projectileQueries 6 / hits 50 / excaliburPaint 364`。旧最終`31.02ms / 43.52ms`から平均約11.6%、p95約11.1%改善し、20Hz Sectorダメージ検索と継続セル塗りを維持した。
- KillBurst Scenarioを敵HP1へ修正し、30体死亡・XP Orb 30個を再現した。Baseline `24.54ms / p95 35.24ms`、NoFeedback `23.72ms / 35.42ms`で、Popup/HitFlash差は平均0.82ms、p95差なし。死亡・XP生成は現在の追加主因ではない。
- 通常敵Outline Material共有を試したが、共有前3回平均22.97msに対して22.99 / 23.63msで改善せず撤回した。DamagePopup 64個事前Poolも`popupSpawns=0`にはなったが27.79～29.94msで改善せず撤回した。

## Completed

- 既存`RuntimePerformanceProbe`を拡張し、10秒間の平均/p95/最大frame ms、33/50/100ms超過数、GC、managed memory差分、敵・Popup・HitFlash・Area・Projectile数を記録するようにした。
- `CombatPerformanceDiagnostics`を追加し、範囲検索回数/候補数/命中試行/成功、全敵探索、Projectile Trigger、セル塗り、Excalibur形状再構築と推定managed payload、Popup、HitFlash、死亡、経験値生成を集計するようにした。
- 実機A/B比較用にBaseline、Popup無効、HitFlash無効、全ダメージ演出無効の4モードと、最終結果読取コマンドを追加した。
- エクスカリバー固有の最大候補は、毎フレームのSector Mesh配列生成・Mesh再設定・PolygonCollider2D再構築・Sector塗り。32分割では配列本体だけで1回約2,616 bytes、60fpsで約157KB/秒に加えてNative Mesh/Physics更新が発生する。
- 全範囲武器共通では、`AdvancedWeaponArea`が`damageInterval`判定より前に毎フレーム`Physics2D.OverlapCircleAll`を実行するため、敵数増加時に検索・配列確保・Component確認が増える。
- 進化範囲攻撃では、対象収集ごとに`FindObjectsOfType<EnemyController>()`と新規Listを作り、フロストストームは5個の攻撃Areaを生成する。サンダー系Projectileも各弾が毎フレーム全敵探索とOverlapを実行する。
- 全武器共通の命中後処理では、成功命中ごとのDamagePopup生成、初回命中時のEnemyHitFlash Component・子Object・Material生成、大量同時死亡の0.48秒Coroutine、経験値Orb/Token生成が命中数・撃破数に比例する。
- リッチは5秒ごとにSkeleton 10体とSkeletonKnight 10体を半径4へ密集召喚するため、上記の検索・Trigger・演出・死亡生成を同時に増幅する。
- 128体のSkeleton/SkeletonKnightを固定配置する持続命中用・同時撃破用Gameplay Test Scenarioの準備コマンドを追加した。持続命中用は敵HP5000、通常速度、計測10秒を基準とする。
- 実機計測を2回開始したが、どちらも`excaliburShape=0`、`excaliburPaint=0`、`projectiles=0`で、エクスカリバーが起動していないことを検出したため本番武器の性能値としては採用しなかった。
- 原因は、Editor側の準備Menuが`RunState.SetNextWeaponTest(Excalibur)`をPlay開始前に呼んでおり、Play開始時のDomain Reloadでstatic値が初期化されること。`GameplayTestBootstrap.Start()`がDomain Reload後にScenarioを読み込んでから本編SceneをAdditive Loadするため、ここで武器指定を渡す必要がある。
- 参考値として、攻撃が届いていない128～129体の敵集団だけでも、安定区間のNoFeedback計測は`frames=363 / avgMs=27.64 / p95Ms=43.94 / maxMs=76.64 / over33=106 / over50=7`だった。敵AI・Collider密集だけで約36FPS相当まで低下しており、範囲攻撃以外の背景負荷も大きい。
- `GameplayTestScenario`へStarting Weapon overrideを追加し、Domain Reload後の`GameplayTestBootstrap.Start()`から本編Scene Load前に`RunState.SetNextWeaponTest()`を呼ぶようにした。
- 再現Scenarioを、Playerを中央へ移動、前方同一点へSkeleton/SkeletonKnightを計128体配置、敵接触ダメージ0、持続試験HP5000、90秒手動終了へ固定した。
- エクスカリバー大量命中Baselineは`avgMs=35.59 / p95Ms=48.56 / triggers=629 / attempts=70 / hits=70 / popupSpawns=74 / flashComponents=23`。
- 同条件NoFeedbackは`avgMs=35.12 / p95Ms=48.88 / triggers=552 / attempts=61 / hits=61 / popupSpawns=0 / flashComponents=0`。演出全停止による平均改善は0.47ms（約1.3%）だけで、Popup/HitFlashは今回の主因ではない。
- エクスカリバー形状更新のみ・命中なしは`avgMs=27.74 / p95Ms=45.39 / excaliburShape=361 / shapeBytes=944,376`。敵集団だけの27.64msとの差は0.10msで、毎フレームMesh/Collider再構築は無駄だが今回の最大CPU要因ではない。
- 大量接触・演出なしは敵集団だけより平均7.48ms悪化した。`OnTriggerStay`、Collider接触、damage interval判定前のcallback、ノックバックと密集Rigidbody物理がエクスカリバー固有の主な追加負荷。
- 水平確認した通常フロストのNoFeedbackは`avgMs=40.20 / p95Ms=55.57 / areaQueries=247 / areaCandidates=8,400 / attempts=40 / hits=40`。敵集団だけより平均12.56ms悪化し、`AdvancedWeaponArea`がdamage intervalに関係なく毎フレーム`OverlapCircleAll`と全候補走査を行う共通経路が最大の追加負荷と判定した。
- 追加A/B用に敵処理の個別無効化、敵同士Collider無効化、Physics2Dマルチスレッド有効化、および攻撃が届かない凍結128体Scenarioを追加した。連続計測のPlayExitクールダウンは`combat-performance-probe.ps1`がmarkerを読み、次Menu前に自動待機する。
- エクスカリバー大量命中条件での再計測はBaseline `35.61ms`、EnemyController全停止`32.92ms`、建造物接触判定なし`35.01ms`、移動倍率なし`35.77ms`、敵塗りなし`35.52ms`、敵Animationなし`36.03ms`、敵YSortなし`35.06ms`。敵Controller全体でも改善は2.69msで、個別C#処理は主因ではない。
- 同エクスカリバー条件で敵同士Colliderだけを無効化すると`32.30ms`だったが、敵がプレイヤー中心へ重なってSector内縁より内側へ入り、命中数が68前後から0へ変わったため、物理負荷と命中負荷が混ざる参考値に留めた。
- 敵速度0、Playerを20セル離し、攻撃が届かない凍結128体Scenarioで再計測した。Baseline `33.94ms`に対し、敵同士Collider無効`32.12ms`（-1.82ms）、EnemyController全停止`31.36ms`（-2.58ms）、Physics2Dマルチスレッド有効`33.41ms`（-0.53ms）。Physics接触とAI更新は実在するが第一要因ではない。
- 同じ凍結128体で`CharacterOcclusionReveal`無効は`25.38ms`（-8.56ms）、`RuntimeSpriteOutline`無効は`24.00ms`（-9.94ms）、両方無効は`18.47ms`（-15.47ms、約45.6%改善）。敵集団だけで発生する最大の基礎負荷は遮蔽表示とRuntime Outlineと確定した。
- `CharacterOcclusionReveal`は通常敵の遮蔽物集合更新自体は0.5秒間隔だが、CommandBufferがattach済みの間は各敵が毎LateUpdateで`RebuildCommands()`を行い、ScreenRectの8点`WorldToScreenPoint`、前後判定、Material/CommandBuffer再設定を反復する。
- `RuntimeSpriteOutline`は各敵が毎LateUpdateで、変化の有無にかかわらずMesh cache参照、Material texture/color/vector/float、Outline Transform、sharedMaterial、sorting、enabledを再設定する。さらに敵1体につき追加Renderer/個別Materialを持つため描画側コストも増える。
- ユーザー実機で占有率連動範囲とXP倍率2.5倍を確認済みとし、この2件の確認TODOを完了した。
- `RuntimeSpriteOutline`はMesh/texture/color/thickness/enabled/sortingと、Outline Renderer自身のMaterial/sorting/enabled状態が変わった時だけ同期する。共有Material+MaterialPropertyBlock化は撤回し、オブジェクト別Materialへ戻したうえで変更検知を維持した。
- `CharacterOcclusionReveal`は通常敵の遮蔽物集合再判定をattach中でも最大10Hzに制限する一方、キャラクターTransformが変わったフレームはCommandBufferを再構築してシルエットを滑らかに追従させる。Material propertyは署名変更時だけ同期する。
- `AdvancedWeaponArea`はdamage-onlyならdamage interval、Slow付きなら最大0.2秒周期だけ`OverlapCircleNonAlloc`を実行し、再利用bufferと敵単位dedupeで配列確保・多重Collider判定を削減した。
- ThunderBall/ThunderStormの範囲判定をdamage interval単位の`OverlapCircleNonAlloc`へ変更し、追尾の全敵探索を0.1秒周期へ制限した。
- Excaliburは毎フレームPolygonCollider2Dを戦闘判定へ使わず、20HzのNonAlloc outer-circle検索と前回検索からの通過Sector解析へ変更した。ダメージ間隔は従来どおり敵ごとの`stats.damageIntervalSeconds`で制限する。
- Excaliburの旧Baseline `avgMs=35.61 / triggers約603 / hits=68`に対し、最終実装は`avgMs=29.61 / p95Ms=41.62 / triggers=0 / queries=5 / candidates=804 / hits=63`。平均は6.00ms（約16.8%）改善し、総ヒット数差は約7.4%に収まった。
- ユーザー実機画像で、大部分の壁・建造物が真っ黒になり、プレイヤーのシルエット位置が段階更新になる回帰を確認した。ガクつきは`DrawMesh`へ記録した`localToWorldMatrix`を0.08～0.1秒ごとにしか更新しなかったことが原因で、Transform変更時の再構築によりユーザー実機で解消確認済み。
- 黒化について、共有Outline Materialと遮蔽Stencil Maskを原因とした2回の判定はいずれも誤りだった。ユーザー指摘どおり前面画像が消えたのではなく、アップグレード／復活時の`BuildingUpgradeController.RefreshYSortRenderers()`が後生成の`Runtime Outline`を`YSort`へ再登録し、Sourceと同じsorting orderに上書きして黒いOutline全面が正面を覆っていた。
- `YSort.Apply()`は`Runtime Outline` Rendererを除外し、`RuntimeSpriteOutline`側もOutline自身のsorting orderが`Source-1`から崩れた場合に再同期する二重対策を追加した。
- `OcclusionStencilMask.shader`へ追加した`ColorMask 0`と`Blend Zero One`はStencil-only Passの防御として維持するが、今回の黒化の直接原因ではない。
- ユーザー実機で建造物の前面画像・黒Outlineの描画順が正常化し、負荷も改善して見えることを確認した。
- 最終状態の凍結敵Baselineは`avgMs=29.00 / p95Ms=41.14 / enemies=128→133`。旧`33.94ms`から4.94ms、約14.6%改善した。
- 最終状態のフロストNoFeedbackは`avgMs=32.32 / p95Ms=55.07 / areaQueries=3 / areaCandidates=105 / hits=39`。旧`40.20ms / 55.57ms / 247 / 8,400 / 40`に対し、平均約19.6%改善、検索回数・候補走査を約98.8%削減し、ヒット数はほぼ同等。
- 最終状態のエクスカリバーBaselineは`avgMs=31.02 / p95Ms=43.52 / triggers=0 / projectileQueries=5 / candidates=780 / hits=61`。旧`35.61ms / 約48.56ms / triggers約603 / hits=68`に対し、平均約12.9%、p95約10.4%改善し、Physics Trigger callbackを廃止した。

## Important Decisions

- 調査で確定した上位3経路（Outline/遮蔽、Area検索、Excalibur Trigger）を本タスクで実装対象とする。
- 計測コードは`UNITY_EDITOR`または`DEVELOPMENT_BUILD`だけで記録し、プローブ停止中の集計負荷を避ける。
- まず同じリッチ集団・同じ武器状態でBaselineとNoFeedbackを比較し、差が大きければNoPopups/NoHitFlashで演出コストを分解する。
- ユーザーから追加Compile/Playの許可を受け、無効データを原因判定から除外したうえで固定Scenarioによる有効なA/B計測を取得した。
- 最適化優先度を実測に基づき更新する。1) `CharacterOcclusionReveal`と`RuntimeSpriteOutline`の毎敵・毎フレーム処理廃止、2) `AdvancedWeaponArea`の検索周期制御とNonAlloc化、3) ExcaliburのTrigger/Collider依存をdamage tick単位の解析的Sector検索へ置換、4) 敵同士Physics Layerの採否検証、5) Excalibur形状配列再利用、6) Popup/HitFlash Pool化。
- 遮蔽表示は削除せず、遮蔽物集合更新は低頻度、移動Transformは毎フレーム追従させる。Runtime OutlineはSourceと自身の描画状態を監視し、常に`Source-1`を維持する。YSortなど子Renderer一括処理では補助Rendererを明示除外する。
- `AdvancedWeaponArea`はdamage-onlyならdamage interval時だけ検索し、Slow付きなら効果時間0.25秒を維持できる最大0.2秒周期で検索する。Reusable NonAlloc bufferと敵単位の重複除去を使い、buffer満杯時だけ拡張再検索する。
- Excaliburは20Hzで外半径NonAlloc検索し、方向・角度・前回からの通過内外半径でSector内判定する。各敵のダメージ/ノックバックは従来のdamage intervalを維持し、`OnTriggerStay`と毎フレームPolygonCollider2D再構築を攻撃判定から外す。
- Physics2Dマルチスレッドは改善0.53msのため優先しない。敵同士Collision Layer無効化は1.82ms改善するが敵の重なり方を変えるため、描画補助と武器検索の対策後にゲーム性を含めて判断する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Testing/CombatPerformanceDiagnostics.cs`
- `Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceProbe.cs`
- `Assets/AreaSurvivors/Scripts/Testing/GameplayTestScenario.cs`
- `Assets/AreaSurvivors/Scripts/Testing/GameplayTestBootstrap.cs`
- `Assets/AreaSurvivors/Editor/CombatPerformanceProbeCommands.cs`
- `Assets/AreaSurvivors/Editor/CombatPerformanceProbeValidator.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponArea.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponProjectile.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponRuntime.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/ExcaliburSectorVisual.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/FrostStormSpikeImpact.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/ProjectileExplosionHitbox.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/SlashView.cs`
- `Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs`
- `Assets/AreaSurvivors/Scripts/Game/Characters/EnemyHitFlash.cs`
- `Assets/AreaSurvivors/Scripts/Game/Characters/CharacterOcclusionReveal.cs`
- `Assets/AreaSurvivors/Scripts/Game/Visuals/DamagePopup.cs`
- `Assets/AreaSurvivors/Scripts/Game/Visuals/RuntimeSpriteOutline.cs`
- `Tools/TokenUsage/combat-performance-probe.ps1`
- `Tools/TokenUsage/command-tools-self-test.ps1`
- `Tools/TokenUsage/invoke-unity-editor-runner.ps1`
- `Docs/AgentRules/combat.md`
- `Docs/AgentRules/command-failure-playbook.md`
- `Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Excalibur_Sustained.asset`
- `Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Excalibur_Sustained.asset.meta`
- `Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Enemy_Crowd.asset`
- `Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Enemy_Crowd.asset.meta`
- `ctx/current.md`
- 外部記憶 `Knowledge/unity-menuitem-validation-attribute.md`
- 外部記憶 `Knowledge/unity-memory-stream-corruption-after-domain-reload.md`

## Verification

- Unity Compileを4回実行し、すべて成功。最後の実行は出力切断後、TokenReportsからImport、Compile、Menu登録、Menu実行がすべてexit 0・timeoutなしであることを回収した。
- 固定シナリオ準備コマンド追加後の5回目Unity Compileも成功した。
- `Area Survivors/Validate/Combat Performance Probe`がfresh success markerを生成して成功した。
- `command-tools-self-test.ps1`成功、26スクリプトをParseし全Guard通過。
- Unity Console Error取得は`logs: []`、`displayedCount: 0`。
- 対象18ファイルの`scoped-diff-check.ps1`が終了コード0で成功した。改行コード変換予告のみでdiff errorはない。
- 初回Runnerで同一メソッドへ複数のvalidation `MenuItem`属性を積み`Multiple custom attributes of the same type found.`となった。各Menu用の個別メソッドへ分離し、self-test、`combat.md`、外部Knowledgeへ再発防止を追加した。
- `scoped-diff-check.ps1`のPreflightで`safe-read`へ100行を指定し、80行上限の`guard_code: 39`で停止した。状態変更はなく、既定の`safe-read-batch`へ切り替えて正式用例を確認した。
- 今回の追加調査でも`safe-read`へ86行・100行を誤指定して同じ`guard_code: 39`で停止した。状態変更はなく、残りの読取を`safe-read-batch`へ固定した。
- Play Mode 1回目は接触ダメージによりGame Overとなり、Probe完了後もscaled timeのTest Runnerが停止して自動終了しなかった。Console Errorは0件で、手動`PlayExit`により終了した。
- Play Mode 2回目は敵接触ダメージ0・HP5000へ変更し、Probeを完走した。ただしDomain ReloadでExcalibur指定が消えたため攻撃計測としては無効。手動`PlayExit`で終了した。
- ユーザーの追加許可後、Compileは合計7回、Play Modeは合計7回実行した。最後の有効計測はエクスカリバーBaseline/NoFeedbackとフロストNoFeedback。
- 6回目Compile自体は成功したが、Inspectorで選択中の`GameplayTestScenario`へserialized fieldを追加した状態で依存Scriptを逐次Importし、Domain Reloadが`Read 260 bytes but expected 296 bytes`、`MemoryStream is corrupted`でFatal停止した。Unity再起動で復旧した。
- 再発防止としてEditor Runnerへ`-BatchRefresh`を追加し、逐次Importせず1回のRefresh→Compileへ固定した。7回目Compile、Menu登録、Validator実行はこの新入口で成功した。
- `Knowledge/unity-memory-stream-corruption-after-domain-reload.md`と`command-failure-playbook.md`へ既知トリガー、証拠、禁止事項、再起動手順を追記した。
- 最終`Area Survivors/Validate/Combat Performance Probe`成功、`command-tools-self-test.ps1`全項目成功、Unity Console Error 0件。
- 最終Scenario AssetがBaseline、Excalibur、Player中央移動、128体前方密集、敵接触ダメージ0、HP5000、90秒手動終了へ保存されていることを限定読取で確認した。
- 対象24ファイルの`scoped-diff-check.ps1`が終了コード0で成功した。改行コード変換予告のみでdiff errorはない。
- 追加調査ではUnity Compileを5回、Play Modeを15回実行した。ユーザーが時間をかけた追加計測を明示許可済みで、各Playは`safe-unity PlayEnter/PlayExit`から実行した。
- 追加診断モードと凍結敵Scenarioの最終Compile成功。`Area Survivors/Validate/Combat Performance Probe`がfresh markerを生成し、`command-tools-self-test.ps1`は26スクリプト・全Guard成功、Unity Console Errorは`logs: [] / displayedCount: 0`。
- 最終保存状態はExcalibur持続Scenarioと凍結敵ScenarioをともにBaselineへ戻した。
- 最初の限定検索は、過去要約の旧パス`Scripts/Weapons`を実在確認せず指定して終了コード1となった。現行の正は`Scripts/Game/Weapons`で、未知Pathは既存親`Scripts`から`safe-search -FilesOnly`で確定する手順へ戻した。対象変更はない。
- Enemy Prefabの`safe-read -Pattern`で`-MaxMatches 1`を付けずguard 39となった。複数一致の既定見積りが原因で、同じWrapperを1件限定して成功した。対象変更はない。
- 1回目の連続計測でPlayExit直後に次Menuを呼びguard 23となった。`combat-performance-probe.ps1`へ`last-playmode-exit.utc`基準の自動待機を追加し、以降の全連続計測と自己テストが成功した。再発防止を`command-failure-playbook.md`へ追記した。
- 負荷対策実装後のUnity Compileを3回実行し、すべて成功。最後はExcaliburの20Hz検索と通過帯判定を含む。
- 最終`Area Survivors/Validate/Combat Performance Probe`と`Area Survivors/Validate/Combat Visual Rotation Guard`がfresh markerを生成して成功した。
- 最終Unity Console Errorは`logs: [] / displayedCount: 0`、`command-tools-self-test.ps1`は26スクリプト・全Guard成功、今回8ファイルの`scoped-diff-check.ps1`も終了コード0。
- Excalibur実機計測1回目はglobal 0.2秒検索により`avgMs=29.62 / hits=27`となり、移動帯がtick間に通過した敵を取りこぼすことを検出した。通過帯判定後は`avgMs=34.26 / hits=41`、敵ごとのヒット開始時刻を保つ20Hz検索後は`avgMs=29.61 / hits=63`まで回復した。
- 読取調査で入れ子PowerShellの`$_`が外側展開される失敗、`safe-read -Pattern`のguard 39、`safe-read-batch`へカンマ区切りを渡したguard 38が発生した。状態変更はなく、長い`-Command`を禁止し、行番号検索+セミコロン区切り`safe-read-batch`へ固定した。
- 最初の通過帯patchはフィールド順の想定違いで適用前検証に失敗した。対象行を限定読取し、実在する前後文へ分割patchして成功した。
- PlayExitクールダウン残り1秒で`command-tools-self-test`を呼びguard 23となった。状態変更はなく、エラー指定どおり経過後の同一テスト1回だけを再実行して成功した。
- 回帰修正後、変更3スクリプトを一括ImportしてUnity Compile 1回成功。`Area Survivors/Validate/Combat Performance Probe`と`Area Survivors/Validate/Combat Visual Rotation Guard`がfresh markerを生成して成功し、Unity Console Errorは`logs: [] / displayedCount: 0`。
- Stencil Pass修正後、ShaderとValidatorをBatchRefreshしてUnity Compile 1回成功。`Area Survivors/Validate/Combat Performance Probe`と`Area Survivors/Validate/Combat Visual Rotation Guard`がfresh markerを生成して成功し、Unity Console Errorは`logs: [] / displayedCount: 0`。
- YSort/Outline描画順修正後、変更3スクリプトをBatchRefreshしてUnity Compile 1回成功。`Area Survivors/Validate/Combat Performance Probe`と`Area Survivors/Validate/Combat Visual Rotation Guard`がfresh markerを生成して成功し、Unity Console Errorは`logs: [] / displayedCount: 0`。
- 最終修正後の効果計測としてPlay Modeを3回実行し、凍結敵Baseline、フロストNoFeedback、エクスカリバーBaselineを同じ10秒Probeで取得した。各回は`safe-unity PlayEnter/PlayExit`を使い、連続計測間はWrapperの20秒cooldownを通した。
- シェーダー検索で引用符を含む検索語が`safe-search`のguard 45に拒否された。状態変更はなく、引用符を除いた固有名による同一Wrapperの1回再実行で対象シェーダーを確定した。
- 3ファイル一括patchは`DetachCommandBuffer`本文の想定違いで適用前検証に失敗した。変更未適用を確認後、実在範囲を限定読取し、ファイル単位patchへ分割して成功した。

## Latest Verification (2026-07-26 Additional Performance Pass)

- 追加実装はBatchRefresh経路で複数回Compileし、すべて成功。最終コードを含むCompileも成功した。
- `Area Survivors/Validate/Combat Performance Probe`と`Area Survivors/Validate/Combat Visual Rotation Guard`がfresh success markerを生成した。
- Unity Console Errorは`logs: [] / displayedCount: 0`。
- `command-tools-self-test.ps1`は26スクリプトをParseし、全Guard成功。
- 今回対象20ファイルの`scoped-diff-check.ps1`は終了コード0。改行コード変換予告だけでdiff errorなし。
- Play Modeは固定敵4回、Outline A/B 3回、フロスト3回、Excalibur系6回、実KillBurst 2回を`safe-unity PlayEnter/PlayExit`経由で実行した。ユーザーが長時間の性能検証を明示許可済み。
- `safe-read`の80行出力見積りを3回誤り`guard_code: 39`となった。対象・Unityは未変更。同一タスクで1回発生後は`safe-read -Pattern`を禁止して`safe-read-batch`へ固定する規則を`command-failure-playbook.md`へ追加した。
- 最終Compile前の1回だけPlayExitクールダウン残り4秒でRunnerが`guard_code: 23`停止。AssetRefresh未到達を確認し、指定時間後の同一Runner 1回だけで成功した。

## TODO / Blocker

- 完了: 性能Scenario中の追加5体は無効`EnemySpawner`に対する`GameManager.BeginStage()`と継続Coroutineが原因。Spawner停止契約を修正し、全計測で`128→128`を確認した。
- TODO: Phase 4として専用Enemy Layerのself-collision無効化を試験し、敵重なり・Player接触・Boss押し出し・壁攻撃への影響を実機確認して採否を決める。
- 完了: Excalibur Sector Meshは形状変化中だけ更新する。セル塗りはゲーム性維持のため毎フレーム継続する。配列再利用は残り7回分だけで効果が小さいため未実装。
- 見送り: Outline Material共有とDamagePopup Poolは実測改善なしのため撤回。1 Renderer統合Outlineは遮蔽Stencil・Animator・HitFlashを同時に変えるため、専用Shader/Validatorを設計する別タスクとする。
- TODO: エクスカリバーの成長表示・セル塗りが従来どおり見えるかはユーザー実機で確認する。Codex側のVisual Rotation Guardと命中計測は成功済み。
- Blockerなし。

## Next Action

- 追加最適化とA/B結果をユーザーへ報告し、エクスカリバー表示の実機確認を依頼する。次の大きな候補は通常敵Outlineの1 Renderer統合だが、Visual回帰リスクが高いため専用タスクで行う。

# Previous Task (2026-07-25 Fire Missile Side Flip Correction)

## Goal

発射中のファイアミサイルについて、弾頭と後端を入れ替えず、進行方向に対する左右だけを0.2秒ごとに反転させる。

## Completed

- ユーザーの実機画像から、旧`PaperMeshVisual.flipHorizontal`が進行軸のローカルXを反転し、弾頭と後端を入れ替えていることを確認した。
- `FireMissileFlip.anim`から旧`PaperMeshVisual.flipHorizontal`カーブを除去した。
- 描画子`Paper Visual`のLocal Scaleを`X=1固定 / Y=1→-1→1 / Z=1固定`として、`0.0秒 / 0.2秒 / 0.4秒`で進行方向に対する左右だけを切り替えるループClipへ修正した。
- 全キーをConstant Tangentにし、補間で潰れず0.2秒ごとに瞬時反転するようにした。
- `FireMissileFlip.controller`を作成し、単一のDefault StateからClipを自動再生する構成にした。
- `FireMissile.prefab`の描画子`Paper Visual`だけへAnimatorを追加し、Controllerを直接保存した。
- Projectile RootのScale、Rotation、Collider、移動、追尾処理は変更していない。
- 再実行可能な`FireMissileFlipAnimationMigration`と、Clipキー、Loop、Constant Tangent、Controller、Prefab参照、Visual Transformを確認する専用Validatorを追加した。
- 全進化Validatorからも専用Validatorを呼び、将来の回帰を検出するようにした。

## Important Decisions

- Runtimeの`Update`やCoroutineで反転せず、Unity標準のAnimationClipとAnimator ControllerをPrefabへ保存する。
- ミサイルの進行方向はローカルX軸なので、X反転を禁止し、進行方向へ直交する描画子のローカルYだけを反転する。
- Root Transformを反転するとColliderやLaunch時Scaleへ影響するため、Animatorは描画子`Paper Visual`だけへ適用する。
- 左右反転以外のSprite、色、基準Scale、Rotation、Collider、追尾軌道は維持する。

## Files Changed

- `Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.anim`
- `Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.anim.meta`
- `Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.controller`
- `Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.controller.meta`
- `Assets/AreaSurvivors/Prefabs/Weapons/FireMissile.prefab`
- `Assets/AreaSurvivors/Editor/FireMissileFlipAnimationMigration.cs`
- `Assets/AreaSurvivors/Editor/FireMissileFlipAnimationMigration.cs.meta`
- `Assets/AreaSurvivors/Editor/FireMissileFlipAnimationValidator.cs`
- `Assets/AreaSurvivors/Editor/FireMissileFlipAnimationValidator.cs.meta`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchValidator.cs`
- `ctx/current.md`

## Verification

- 修正したEditorコードと依存Validatorを固定Runnerで明示Importし、Unity Compile 1回成功。
- `Area Survivors/Migrations/Apply Fire Missile Flip Animation`を実行し、Local Scale X/Y/Zの新ClipとPrefab参照を保存した。
- `Area Survivors/Validate/Fire Missile Flip Animation`がfresh success markerを生成し、旧`flipHorizontal`カーブ不在、X/Z固定、Y=`1/-1/1`、Constant Tangent、Controller/Prefab参照を確認して成功した。
- `Area Survivors/Validate/Weapon Evolution Batch`がfresh success markerを生成して成功した。
- `Area Survivors/Validate/Combat Visual Rotation Guard`がfresh success markerを生成して成功した。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- 生の`rtk git diff --check`はGit hook未初期化警告を終了コード1で返したため、既定の`scoped-diff-check.ps1`へ固定した。正式WrapperはAnimatorが生成した空フィールド`m_WarningMessage: `の末尾空白1行を検出し、該当行だけを`m_WarningMessage:`へ正規化した。
- Prefab再Import後、専用Validator、Rotation Guard、同じscoped diffを再確認して成功した。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で、弾頭が進行方向を向いたまま0.2秒ごとに左右だけが反転することを確認する。
- TODO: ユーザーがXP倍率2.5倍で全Stageをプレイし、Lv40前後へ自然に到達するか確認する。
- Blockerなし。

## Next Action

- ユーザーの実機確認結果に応じ、必要なら反転間隔または見た目だけを限定調整する。

# Previous Task (2026-07-25 Fire Missile Speed 75%)

## Goal

ファイアミサイルの弾速が遅すぎるため、進化前の50%から75%へ引き上げる。他の発射・追尾・攻撃間隔・射程仕様は維持する。

## Completed

- `GameConfig.fireMissileProjectileSpeedMultiplier`を`0.5f`から`0.75f`へ変更した。
- Fire Missile Motion Migrationを75%設定へ更新した。
- Fire Missile Homing Validatorと全進化Validatorの期待値を75%へ更新した。
- GameConfig Assetへ`fireMissileProjectileSpeedMultiplier: 0.75`を保存した。

## Important Decisions

- 弾速だけを変更し、前方180度ランダム発射、敵0体時の発射、発射後の追尾、旋回速度180度/秒、初期攻撃間隔0.5秒、飛距離は変更しない。
- 飛距離を維持するため、50%時より飛行時間は短くなるが、進化前と同じ距離を飛ぶ。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchMigration.cs`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchValidator.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- `ctx/current.md`

## Verification

- 変更C# 3件を個別Importし、Unity Compile 1回成功。
- `Area Survivors/Migrations/Apply Fire Missile Motion`がfresh success markerを生成し、弾速倍率0.75をAssetへ保存した。
- `Area Survivors/Validate/Fire Missile Homing`がfresh success markerを生成して成功した。
- `Area Survivors/Validate/Weapon Evolution Batch`がfresh success markerを生成して成功した。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で弾速75%の追尾軌道と見やすさを確認する。
- TODO: ユーザーがXP倍率2.5倍で全Stageをプレイし、Lv40前後へ自然に到達するか確認する。
- Blockerなし。

## Next Action

- 実機確認結果に応じ、弾速倍率のみを限定微調整する。

# Previous Task (2026-07-25 Fire Missile Motion)

## Goal

進化武器ファイアミサイルを、プレイヤー進行方向の前方180度へランダム発射し、敵がいなくても発射、敵がいれば発射後に追尾する仕様へ変更する。進化時の初期攻撃間隔を0.5秒、弾速を進化前の50%にする。

## Completed

- ファイアミサイルの発射方向を、`PlayerController.Facing`を中心とする±90度のランダム角度へ変更した。
- 射程内の敵が0体でも発射し、対象なしのまま前方へ飛行するようにした。
- 射程内に敵がいる場合は最寄りの敵を保持し、ランダム角度で発射した後、既存の180度/秒の方向転換で追尾する。
- 対象なしで発射した後も、既存の0.1秒間隔の再探索により、射程内へ敵が入れば追尾を開始する。
- `FireMissileLaunchDecision`を追加し、Prefabあり・敵0体でも発射する契約を純粋な判定として固定した。
- `GameConfig`へ`fireMissileBaseCooldownSeconds=0.5`、`fireMissileProjectileSpeedMultiplier=0.5`、`fireMissileLaunchArcDegrees=180`を追加した。
- 進化時に攻撃間隔0.5秒を基礎値として適用し、弾速を進化前の50%へ変更した。飛距離は変えず、低速化に応じて飛行時間が伸びる。
- GameConfig設定を専用Migrationで保存し、旧`fireMissileBaseCooldownMultiplier`をAssetとコードから除去した。
- 進化説明を日英とも、前方180度ランダム発射と条件付き追尾が分かる文言へ更新した。
- 専用Validatorへ±90度境界、角度Clamp、敵0体発射、対象あり追尾、Prefabなし非発射、弾速50%、攻撃間隔0.5秒、日英説明の検証を追加した。

## Important Decisions

- 通常のファイアボールは従来どおり進行方向へ直進し、今回のランダム角度・追尾・0.5秒・弾速50%は進化後のファイアミサイルだけへ適用する。
- 「進行方向」はオーラソードと同じく`player.Facing`を使用し、方向が取れない場合は下向きを既定値とする。
- 弾速を半分にしても射程は半減させず、同じ距離をゆっくり飛ぶことで追尾軌道を見やすくする。
- 既存の追尾対象死亡時の再捕捉と、徐々に方向転換する処理は維持する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Scripts/Core/WeaponCatalog.cs`
- `Assets/AreaSurvivors/Scripts/Core/Localization/LocalizationTextCatalog.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/WeaponController.cs`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchMigration.cs`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchValidator.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- `ctx/current.md`
- 外部記憶 `Knowledge/powershell-quoted-executable-invocation.md`

## Verification

- 変更C#を個別Importし、Unity Compileを2回実行して成功した。
- `Area Survivors/Migrations/Apply Fire Missile Motion`がfresh success markerを生成し、GameConfig Assetへ0.5秒、弾速50%、前方180度、旋回180度/秒を保存した。
- `Area Survivors/Validate/Fire Missile Cooldown`がfresh success markerを生成して成功した。
- `Area Survivors/Validate/Fire Missile Homing`がfresh success markerを生成して成功した。
- `Area Survivors/Validate/Weapon Evolution Batch`がfresh success markerを生成して成功した。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で、前方180度の発射分布、敵0体時の発射、発射後の追尾軌道、0.5秒間隔、弾速50%の見やすさを確認する。
- TODO: ユーザーがXP倍率2.5倍で全Stageをプレイし、Lv40前後へ自然に到達するか確認する。
- Blockerなし。

## Next Action

- 実機確認結果に応じ、発射角、弾速、旋回速度、攻撃間隔を限定調整する。

# Previous Task (2026-07-25 Fireball Specification Investigation)

## Goal

ファイアボールと進化後ファイアミサイルの現行仕様を、実装・GameConfig・Prefabから整理する。今回は調査のみで、ゲーム実装は変更しない。

## Completed

- 魔法使いの初期武器はファイアボール。通常時はプレイヤーの向いている方向へ1発発射し、敵を自動追尾しない。
- 基礎値は攻撃力6、攻撃間隔1.45秒、弾速11.5、飛距離7.0ワールド（標準10セル）、爆発半径1.1ワールド（約1.57セル）。
- 火球は敵へ初接触した時、または飛距離相当の寿命終了時に爆発する。壁や建造物は着弾判定の対象ではない。
- 爆発は円内の全EnemyControllerへ1回ずつ同じ攻撃力を与える。距離減衰、持続ヒット、基礎ノックバックはない。
- 飛行中は0.06秒間隔・半径1セルでプレイヤー領地を塗り、爆発時は爆発半径と同じ楕円範囲を塗る。
- 通常ランの個別強化は攻撃力+2、攻撃間隔×0.92、爆発半径+0.375、飛距離+0.75の4候補。選択ごとに表示Lvが1上がる。
- エリア占有率50%以上では爆発半径だけが2倍になる。
- 表示Lv10かつボス出現中にファイアミサイルへ進化できる。進化後は基礎攻撃力16、基礎攻撃間隔0.725秒相当、飛距離+7.0、最寄りの射程内敵を追尾、旋回速度180度/秒となる。爆発半径と弾速は進化前から引き継ぐ。
- 進化後は射程内に敵がいないと発射せず、敵がいる場合はランダム方向へ出した後に追尾する。
- 通常ランの個別強化と、`GameConfig.asset`の内部Lv1～10テーブルは別経路。内部テーブルではLv1→10で攻撃力6→15、攻撃間隔1.45→0.667、弾速11.5→13.75、飛距離7.0→13.3、爆発半径1.1→7.85となる。

## Important Decisions

- 通常ランの表示Lvは個別強化の選択回数であり、`fireballWeaponLevels`を自動的に1段ずつ進める仕組みではないため、両方を混同せず報告する。
- 爆発Visualは半径に完全追従せず、着弾表示Scaleが0.55～1.2へClampされる。大きな爆発半径では見た目と当たり判定が一致しない可能性がある。
- 今回は仕様確認のみで、数値・挙動・Prefabを変更しない。

## Files Changed

- `ctx/current.md`のみ。ゲームコード・Prefab・設定Assetは変更していない。
- 外部記憶 `Knowledge/safe-read-output-guard.md`へ、safe-read出力量Guard再発防止を追記した。

## Verification

- `GameConfig.cs`、`GameConfig.asset`、`WeaponController.cs`、`Projectile.cs`、`ProjectileExplosionHitbox.cs`、`WeaponCatalog.cs`、`Fireball.prefab`、`Player.prefab`を限定読み取りした。
- 現行値、発射経路、着弾条件、範囲ダメージ、領地塗り、特殊効果、進化条件と追尾経路を静的に照合した。
- Play Mode、Unity Compile、Asset変更は行っていない。

## TODO / Blocker

- TODO: ユーザーのイメージするファイアボール仕様を確認し、変更範囲を決定する。
- TODO: 通常ランの個別強化と内部Lv1～10テーブルを今後統一するか判断する。
- TODO: ユーザーがXP倍率2.5倍で全Stageをプレイし、Lv40前後へ自然に到達するか確認する。
- Blockerなし。

## Next Action

- 現行ファイアボール仕様を報告し、ユーザーの希望する発射・着弾・爆発・進化仕様を受けて限定修正する。

# Previous Task (2026-07-25 XP Curve Implementation)

## Goal

Lv1からLv40まで体感が急変しない滑らかな必要XP曲線を実装し、XP倍率2.5倍の全StageプレイでLv40前後へ到達できるようにする。

## Completed

- 必要XPの成長率を、到達Lv2の1.35から到達Lv39の1.10まで線形補間する計算へ変更した。
- 固定加算+3とLv1→2の必要XP 5は維持した。
- 通常の経験値取得と開始レベルボーナスの両方を`CalculateNextXpRequirement`へ統一した。
- 敵が落とすXPとXP倍率の処理は変更していない。
- 曲線の累計必要XPはLv18で4,065、Lv30で51,460、Lv40で237,824となる。
- XP倍率2.5倍時、Lv40までに必要な敵基礎XPは約95,130で、変更前のLv30到達目安94,730とほぼ同じ。

## Important Decisions

- Lv18などの明示的な切替点は設けず、Lv1からLv40まで必要XPを連続的に調整する。
- 成長率、補間開始／終了レベル、固定加算は`GameConfig`のserialized fieldとして調整可能にする。
- 敵XPはHP14あたり1の関係を維持する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- `ctx/current.md`

## Verification

- 変更したC# 2件と設定アセット1件を個別AssetImportした。
- Import直後のCompile要求は非同期コンパイルとの重複防止ガードで停止したため、指定された待機後に同じCompileを1回だけ再実行して成功した（Compile試行2回）。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- 対象4ファイルの`scoped-diff-check`成功。
- 現行の丸め規則でLv5=55、Lv10=419、Lv15=1,869、Lv18=4,065、Lv20=6,580、Lv25=19,716、Lv30=51,460、Lv35=117,872、Lv40=237,824を確認した。
- 初回の設定アセット読み取りは推測パス`Resources/GameConfig.asset`が実在せずガード停止した。実在パスは`safe-search.ps1 -FilesOnly`で`Resources/Config/GameConfig.asset`と確定し、以後は確認済みパスだけを使用した。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーがXP倍率2.5倍で全Stageをプレイし、Lv40前後へ自然に到達するか確認する。
- TODO: 実測がLv40から大きく外れる場合は、敵XPではなく`xpRequirementGrowthEnd`を中心に微調整する。
- Blockerなし。

## Next Action

- 実装完了を報告し、全Stage実機プレイの到達レベルと体感を受けて必要なら曲線を微調整する。

# Previous Inherited Task (2026-07-25 Raincaller Plume Investigation)

## Goal

Lv1からLv40まで体感が急変しない滑らかな必要XP曲線を設計し、XP倍率2.5倍でLv40前後へ到達させる。

## Completed

- 雨呼びの羽飾りはCommon、`WeaponRangeBonus`、対象`ArrowRain`、値`1.0`で、「アローレインの攻撃範囲 +1」と定義されている。
- 補正は`stats.range += 1.0`で、セル数ではなくワールド半径へ直接加算される。
- 標準セル幅0.7に対して+1.0は半径約+1.43セルに相当する。
- アローレインはLv1半径2.1→3.1（3.0→4.43セル）、Lv10半径3.36→4.36（4.8→6.23セル）になる。
- アローシャワーはLv10進化直後半径1.96→2.96（2.8→4.23セル）になる。
- 面積換算ではアローレインLv1が約+118%、Lv10が約+68%、アローシャワーLv10進化直後が約+128%になる。
- `AdvancedWeaponArea`は同じ`stats.range`を見た目、ダメージ判定、塗り範囲へ使用する。
- アローシャワーの敵選択半径`evolvedGroundStrikeTargetRadiusCells=15`は別設定で、雨呼びの羽飾りでは変化しない。
- 単一の`×1.23+3`ではLv40累計246,528 XPになるが、Lv18累計が2,302まで下がり序盤が現行の約2.7倍速くなる。
- 推奨案として、必要XP成長率をLv2時点の1.35からLv39時点の1.10まで線形補間し、固定加算+3を維持する滑らかな曲線を試算した。
- 推奨曲線はLv18累計4,065、Lv30累計51,460、Lv40累計237,824。XP2.5倍時の敵基礎XP換算95,130で、現行Lv30の94,730とほぼ一致する。

## Important Decisions

- 「+1」は1セルではなく1ワールド単位の半径加算であることが、大きく見える主因。
- アローシャワーにも進化元`ArrowRain`のステータスとしてレリック補正が適用される。
- 今回は仕様確認のみで、数値変更は行わない。
- Lv18などの明示的な切替点を設けず、必要XPは毎レベル増やしつつ増加率だけを少しずつ緩和する方針を推奨する。
- 敵XPはHP14あたり1の関係を維持し、まず必要XP曲線だけを変更する。

## Files Changed

- `ctx/current.md`のみ。ゲーム実装・設定は変更していない。

## Verification

- `RelicCatalog`の雨呼びの羽飾り定義を確認した。
- `RelicEffects.ApplyWeaponStatBonuses`の加算式と`WeaponController`の進化補正→レリック補正順を確認した。
- `GameConfig.asset`のアローレインLv1半径2.1、Lv10半径3.36、進化着弾基礎半径0.7、敵選択半径15セルを確認した。
- `AdvancedWeaponRuntime`と`AdvancedWeaponArea`で`stats.range`が着弾半径、Visual、Damage、Paintへ共通使用されることを確認した。
- 単一成長率1.20～1.35と、開始1.35／終了1.08～1.22の線形補間曲線を現行と同じ丸め規則で比較した。
- 直前の設定追加前の古い行番号で別武器範囲を読んだため数値確定を停止し、配列名を再検索して現行行番号から再取得した。ゲーム状態・設定変更へは未到達。
- Unity Compile／Play Modeは実行していない。

## TODO / Blocker

- TODO: ユーザー判断後、雨呼びの羽飾りの`value`を縮小するか、「1セル相当」の0.7へ変更する。
- 完了: Lv2～39で成長率1.35→1.10を線形補間する必要XP計算を実装し、通常レベルアップと開始レベルボーナスで共通化した。
- Blockerなし。

## Next Action

- XP倍率2.5倍の全StageプレイでLv40前後になることを実機確認する。

# Previous Inherited Task (2026-07-25 Evolution Attack Power)

## Goal

XP倍率2.5倍のプレイで、現行のLv30前後ではなくLv40前後へ到達できる必要XP曲線を設計する。

## Completed

- 進化基礎攻撃力を、ソードラッシュ16、黄金の弓16、ファイアミサイル16、デュアルシールド12、女神の祝福12、バナナ12、エクスカリバー12、アローシャワー10、マシンガン80、フロストストーム5、サンダーストーム10へ設定した。
- 全進化基礎攻撃力を`GameConfig`の個別serialized fieldへ保存した。
- 進化前Lv10までの攻撃力成長分を維持し、進化基礎値だけを差し替える`WeaponController.ResolveEvolutionAttackPower`へ標準4武器とAdvanced 7武器を統一した。
- 銃はLv1を50へ変更し、従来の1Lvごと+2成長を維持してLv10を68へ変更した。`CreateAdvancedWeaponLevel`の既定値も同じ式へ更新した。
- `WeaponEvolutionBatchValidator`へ指定基礎値、銃Lv1/Lv10、全11種のLv10進化直後攻撃力検証を追加した。
- Lv18までは現行`×1.35+3`を維持し、Lv18以降を`×1.14+1`へ変更する案を試算した。
- 現行Lv30累計236,823 XPに対し、提案曲線のLv40累計は236,278 XP。2.5倍時の敵基礎XP換算は94,512で、現行Lv30の94,730とほぼ一致する。
- 提案曲線ではLv18累計6,281を維持し、Lv30累計58,313、Lv40累計236,278、Lv39→40必要29,933となる。

## Important Decisions

- 「進化後の基礎攻撃力」はLv1相当の基礎値とし、実際のLv10進化直後は進化前のLv成長分を加える。
- Lv10進化直後はソードラッシュ25、黄金の弓25、ファイアミサイル25、デュアルシールド21、女神の祝福21、バナナ21、エクスカリバー21、アローシャワー19、マシンガン98、フロストストーム14、サンダーストーム19となる。
- 弾数、多段数、攻撃間隔、レリック、永続強化、ラン中強化は上記値へ含めない。
- Lv18までの良好な体感を維持するため、必要XP曲線はLv18を境に二段階化し、敵XPはまず変更しない方針を推奨する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Scripts/Game/Weapons/WeaponController.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- `Assets/AreaSurvivors/Editor/WeaponEvolutionBatchValidator.cs`
- `ctx/current.md`

## Verification

- 変更3 C#と`GameConfig.asset`を1件ずつAssetImportし、Unity Compile 1回成功。
- `Area Survivors/Validate/Weapon Evolution Batch`がfresh success markerを生成して成功。
- 銃Lv1=50／Lv10=68、全11進化基礎値、全11種のLv10進化直後攻撃力をValidatorで確認した。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- 対象4ファイルの`scoped-diff-check`成功。
- 現行曲線とLv18以降の成長率1.13／1.14／1.15を同じ丸め規則で比較し、1.14＋1のLv40累計236,278 XPを確認した。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で各進化武器のダメージ表示を確認する。
- TODO: 前タスクから継続して、Lv40到達用の必要XP曲線または敵XP調整方針を決定する。
- TODO: ユーザー了承後、Lv18以降を`×1.14+1`へ変更し、通常レベルアップと開始レベルボーナスの両経路を同じ計算関数へ統一する。
- Blockerなし。

## Next Action

- 提案曲線の了承後に実装し、2.5倍の全StageプレイでLv40前後になることをユーザー実機で確認する。

# Previous Inherited Task (2026-07-25 Weapon/XP Analysis)

## Goal

Lv18以降にレベルが上がりづらくなる原因を確認し、Lv40到達調整に備えて必要XP曲線・敵XP・獲得倍率を整理する。

## Completed

- `WeaponCatalog`で全11組の進化対応を確認した。
- `GameConfig.asset`の各武器Lv1／Lv10の`attackPower`を確認した。
- `WeaponController.ApplyStandardEvolutionBaseValues`と`ApplyAdvancedEvolutionBaseValues`を確認し、進化時の攻撃力補正を反映した。
- 基礎攻撃力は永続強化・ラン中強化・レリック・条件倍率を含まない、1発／1ヒット／1ダメージtickあたりの値として整理した。
- スラッシュは`attackPower 6 + slashDamageBonus 2`の実基礎8、ソードラッシュは設定基礎16として扱う。
- レベルは1、次Lv必要XPは5から開始し、レベルアップごとに`RoundToInt(前回必要XP × 1.35 + 3)`で増加する。
- 累計必要XPはLv18が6,281、Lv30が236,823、Lv32が431,834、Lv40が4,767,220。Lv30から40だけで追加4,530,397必要。
- 敵XPはHP14あたり1を基準とし、Stage 1は通常1/2・エリート5/10・ボス80、Stage 2は4/8・20/40・320、Stage 3は12/18・60/90・720、Stage 4は27/41・135/203・2430。
- 基礎XP倍率はナイト1.1、アーチャー1.0、メイジ1.3。永続`XpGain`は1Lvあたり+0.1（最大10Lv）、学びのレンズは合計倍率へ×1.1。

## Important Decisions

- 進化武器は独立したレベル配列を持たず、進化前武器のLv10ステータスを継承して進化補正を加える。このため「Lv1相当の進化基礎値」と「Lv10進化直後」の両方を報告する。
- 多段数、弾数、攻撃間隔、持続tick数は攻撃力一覧へ掛け合わせない。
- XP効率一覧は敵の基礎XPを正とし、実獲得は`敵XP × PlayerStats.xpGainMultiplier`を端数繰越しで整数化する。
- Lv18以降の停滞は、必要XPが毎Lv約35%増える指数曲線が主因と判断する。

## Files Changed

- `ctx/current.md`のみ。ゲーム実装・設定は変更していない。

## Verification

- `WeaponCatalog.cs`の全11進化ペアを確認した。
- `GameConfig.asset`の11武器配列についてLv1／Lv10の`attackPower`を限定読み取りした。
- `WeaponController`の標準4武器とAdvanced 7武器の進化基礎補正を確認した。
- `GameManager`の初期レベル・必要XP更新式・端数繰越処理、`GameConfig.asset`の全20種XP、`PlayerStats`のXP倍率式を読み取り確認した。
- 90行の`safe-read`は既存`guard_code: 39`で入口拒否され、読み取り・状態変更へ未到達。元範囲を`safe-read-batch`で80＋10行へ自動分割し、`command-tools-self-test.ps1`全項目成功を確認した。
- Unity Compile／Play Modeは実行していない。

## TODO / Blocker

- TODO: ユーザー指定の新しい進化武器攻撃力へ調整する。
- TODO: 8分の全StageクリアでLv40前後へ到達するため、必要XP曲線・Stage 3/4敵XP・XP倍率のどれを変更するか決定する。
- Blockerなし。

## Next Action

- 現行XP一覧を基に、Lv40到達目標へ向けた新しい必要XP曲線または敵XPを設計する。

# Previous Inherited Task (2026-07-25 Relic Drop Eligibility)

## Goal

Stage 4の敵接触ダメージを下げ、ドラゴンブレスだけは72を維持する。

## Completed

- `RelicCatalog`で所持済みを除外した候補配列を先に構築し、その候補内だけで既存のレアリティウェイト50/30/15/5を使って抽選するようにした。
- 所持レリック数が9以下では`RelicRarity.Legendary`全種と`RelicType.SolitaryBlade`を候補から除外し、10以上で解禁する。
- 初回ボス報酬とフィールド上のレリック宝箱は同じ`RelicCatalog.TryPickRandom`を使用するため、両経路へ同じ制限を適用した。
- 重複時のトークン変換経路を削除した。全レリック所持済みで候補が0件の場合は「レリックが見つかりません」を表示する。
- `RelicDropEligibilityValidator`を追加した。
- 接触ダメージは `enemyDamage=3 × damageMultiplier` の四捨五入で決まり、Stage 4をリザード26、エリートリザード52、リザードマン32、エリートリザードマン64、ドラゴン96へ変更した。
- ドラゴン接触ダメージの低下に合わせ、ブレス倍率を0.5から0.75へ変更してブレスダメージ72を維持した。
- Stage 1～3の通常敵・エリート・ボス接触ダメージと、ドラゴン以外のボス特殊攻撃は変更していない。

## Important Decisions

- 「被りなし」は、ラン内だけではなく`ProgressionStore`に保存済みの所持レリックを抽選候補から除外する。
- 強力レリックの解禁境界は、所持数9では禁止、所持数10で許可とする。
- レアリティ確率自体は変更せず、候補が存在するレアリティだけを既存ウェイトで抽選する。
- 敵ダメージ一覧は防御適用前の攻撃値を正とし、プレイヤーの実被害は `Ceil(攻撃値 - defense)` で計算される。
- ドラゴンブレスの72は接触ダメージ96の0.75倍として維持する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/RelicCatalog.cs`
- `Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs`
- `Assets/AreaSurvivors/Scripts/Game/Pickups/RelicChest.cs`
- `Assets/AreaSurvivors/Editor/RelicDropEligibilityValidator.cs`
- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`

## Verification

- 変更4スクリプトを1件ずつAssetImportし、Unity Compile 1回成功。
- `Area Survivors/Validate/Relic Drop Eligibility`がfresh success markerを生成して成功。
- 所持9個でレジェンダリー／孤高の刃が候補0、所持10個で解禁、所持済み3種が候補から除外、全所持時は候補0を確認した。
- `Area Survivors/Validate/Stage Transition Enemy Defeat`も再実行して成功。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- 変更4ファイルの`scoped-diff-check`成功。
- 81行の`safe-read`は既存`guard_code: 39`で入口拒否され、読み取り・状態変更へ未到達。正式契約確認後、元範囲を`safe-read-batch`で自動分割し、`command-tools-self-test.ps1`全項目成功を確認した。
- `GameConfig.asset`の基礎攻撃力と全20種の倍率、`EnemyController`の接触ダメージ式、各ボス特殊攻撃Controllerの倍率・適用式を読み取り確認した。敵設定の変更は行っていない。
- Stage 4ダメージ調整後、変更したC#とAssetをImportし、Unity Compile 1回成功。Console Error表示ログ0件。
- Stage 4ダメージ調整対象のC#・Asset・`ctx/current.md`で`scoped-diff-check`成功。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で、所持済みレリックが宝箱と直接獲得のどちらからも再出現しないことを確認する。
- TODO: 所持9個ではレジェンダリー／孤高の刃が出ず、10個以降で出現し得ることを実機で確認する。
- TODO: ユーザーが実機でStage 4の新しい接触ダメージと、ドラゴンブレス72が維持されていることを確認する。
- Blockerなし。

## Next Action

- 実機結果を基に、必要ならStage 4の対象だけを再調整する。

# Previous Inherited Task (2026-07-25 Stage Rewards)

## Goal

初回クリア後のボス報酬フローを修正する。再クリア時のDragonはゲーム終了前にレリックを獲得させ、ステージ1～3はボス撃破演出→画面フラッシュ→残敵討伐→全XP／トークン吸引→次ステージの順に進める。

## Completed

- 初回クリア後のステージ1～3だけ従来どおりフィールド上のレリック宝箱を生成し、ステージ4以降では物理宝箱を生成しない報酬方針を明示した。
- 再クリアのDragonは`GameClearRoutine`内で既存のレリック獲得処理とパネル完了を待ってから`EndRun`へ進むようにした。
- ステージ1～3の遷移順を、残敵討伐完了→フィールド上の全XP／トークン吸引→ROUND表示／次ステージへ変更した。画面フラッシュ後に残敵討伐が開始される既存順序は維持した。
- 残敵討伐のタイムアウト時も敵を報酬なしで消さず、強制撃破でXP／トークンをドロップさせ、全敵の破棄完了まで待つようにした。
- `ExperienceOrb`／`TokenOrb`へステージ遷移専用の全域吸引を追加した。取得値を一度だけ予約して0へ移し、通常時と同じ`MoveTowards`・各Orbの`speed`でPlayerへ追尾した後に一括付与する。
- ユーザー確認により「吸い込ませる」は固定時間補間ではなく、ボス討伐後だけ通常吸引の距離制限を解除する意味だと確定した。旧0.75秒の三次補間を削除し、遠いOrbほど到着に時間がかかる通常追尾へ修正した。
- XPとトークンは吸引開始時点の全フィールド分を集計し、吸引完了後にそれぞれ1回だけ加算する。XP効果音も重複させず1回だけ再生する。
- `StageTransitionEnemyDefeatValidator`を拡張し、報酬方針、処理順、タイムアウト経路、Dragonのレリック獲得待機、XP／トークン予約の一度限り契約を検証した。

## Important Decisions

- ステージ1～3の再クリアではレリック宝箱の従来挙動を変えず、今回の全域吸引対象は依頼どおりXPとトークンだけにする。
- Dragon再クリアはゲーム終了で物理宝箱の取得猶予がないため、初回クリアと同じレリック獲得処理を直接実行し、パネルを閉じるまでゲーム終了を待つ。
- 残敵由来の報酬を確実に含めるため、残敵の死亡・破棄・ドロップ完了後にOrbを列挙する。
- 吸引アニメーション中の通常接触取得との二重加算を防ぐため、Orb側で値を予約した時点で通常取得値を0にする。
- ボス討伐後の吸引は通常時の移動関数と速度を共用し、違いは`attractRange`判定を通さず全Orbを追尾開始することだけにする。固定時間Lerpや瞬間移動を通常完了経路に使わない。
- Play Modeと実画面操作は行わず、実際の演出順、吸引表示、レリック画面はユーザーへ確認を依頼する。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs`
- `Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs`
- `Assets/AreaSurvivors/Scripts/Game/Pickups/ExperienceOrb.cs`
- `Assets/AreaSurvivors/Scripts/Game/Pickups/TokenOrb.cs`
- `Assets/AreaSurvivors/Editor/StageTransitionEnemyDefeatValidator.cs`

## Verification

- 変更5スクリプトを1件ずつAssetImportし、Unity Compile 1回成功。
- `Area Survivors/Validate/Stage Transition Enemy Defeat`がfresh success markerを生成して成功。
- 10ユニット離れたOrbが速度6・0.1秒で0.6ユニットだけ進み、Player位置へ瞬間移動しないこと、距離÷通常速度で移動時間を見積もることをValidatorで確認した。
- Unity Console Errorは`logs: []`、`displayedCount: 0`。
- `command-tools-self-test.ps1`全項目成功。
- 変更5ファイルの`scoped-diff-check`成功。
- Console確認で誤って未定義`-MaxLines`を渡した1回はCLI引数解析で終了コード1となり、Unity処理・状態変更へ未到達。正式契約`-MaxCount`と自己テストを確認してから同じ確認を1回だけ再開した。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で、再クリアDragon撃破後にレリック獲得画面が出て、閉じるまでリザルトへ進まないことを確認する。
- TODO: ユーザーが実機で、再クリアのステージ1～3が「ボス撃破演出→画面フラッシュ→残敵討伐→残敵分を含む全XP／トークンが通常速度で距離に応じて飛来→次ステージ」の順になることを確認する。
- TODO: 吸引XPでレベルアップが発生するケースでも遷移が破綻しないことをユーザーが確認する。
- Blockerなし。

## Next Action

- 実装・静的検証完了を報告し、ユーザーの実機確認結果に応じて限定修正する。

# Previous Inherited Task (2026-07-24)

## Goal

全ステージクリアまでの `token_run_log.jsonl` を分析し、プレイ時間、ステージ／難易度クリア状況、トークン収支を整理する。

## Completed

- 通常敵HPを `14 → 28 → 56 → 112 → 168 → 252 → 378 → 567` へ変更した。
- オーガから後は、次の通常敵が直前の通常敵の1.5倍になる。
- エリート敵は対応する通常敵の5倍を維持し、後半4種を `840 / 1260 / 1890 / 2835` HPへ変更した。
- ステージ先頭通常敵とのHP比率を維持し、リッチを10080、ドラゴンを34020 HPへ変更した。ゴブリンロードのHPは4480を維持した。
- オークキングのHP倍率を40から80へ変更し、HPを560から1120へ強化した。
- ゴブリンロードの黒い弾ダメージ倍率を0.25から0.5へ変更し、1回のダメージを12から24へ強化した。
- 全20種のXPを `HP ÷ 14` へ再設定した。
- `xpValue` は整数のため、リザードマンは40.5を41、エリートリザードマンは202.5を203へ切り上げた。
- ボス以外は敵固有の速度差を維持したまま、実移動速度へStage 1は+0、Stage 2は+0.2、Stage 3は+0.4、Stage 4は+0.6を加算するよう変更した。
- ボス以外の通常スポーンと召喚スポーンの両方へステージ速度補正を適用した。
- 全4ボスをステージ速度補正の対象外とし、速度倍率0.31、実移動速度0.279へ統一した。
- プレイヤー防御力の現状を確認し、基礎値はナイト3・アーチャー1・メイジ0、プレイヤーレベル上昇ごとに+0.5であることを確認した。
- スキルツリーの永続防御強化は1レベルあたり+1、最大10レベルである。`runDefenseBonus=1`は設定されているが、現在は`AddDefense`の呼び出しがなくラン中選択肢には使われていない。
- ボス以外の通常敵8種の接触ダメージを `6 / 9 / 12 / 15 / 21 / 24 / 32 / 48` へ変更した。
- 対応するエリート8種の接触ダメージを通常敵の2倍となる `12 / 18 / 24 / 30 / 42 / 48 / 64 / 96` へ変更した。
- `GameConfig.cs` の初期定義と `GameConfig.asset` の実データを同じ倍率へ更新した。
- ボスの接触攻撃力、Token、特殊攻撃値は変更していない。
- 通常敵へ中心塔とプレイヤーの両参照を渡し、プレイヤーが5セル以内かつ中心塔より近い場合だけプレイヤーへ追跡先を切り替えるようにした。
- 5セル判定は `TileGrid.WorldCellSize()` のX/Y実寸で正規化し、非正方形セルでもセル単位の円形距離として判定する。
- プレイヤーが非アクティブまたは死亡中の場合は中心塔へ戻す。
- ボスとエリートは追跡先切替の対象外とし、従来どおり中心塔を狙う。
- 建造物基礎HPを中心塔200、バリスタ150、木の壁100、監視塔150へ強化した。強化後中心塔は900を維持した。
- 永続強化は中心塔を1レベルあたり+30（最大10レベル）へ強化し、木の壁は3系統合計1レベルあたり+20（各最大5レベル）を維持した。
- 建造後アップグレードはバリスタ・木の壁・監視塔を各+200 HPへ強化した。最終値はバリスタ350、木の壁300～600、監視塔350。
- `20260724-095116-670` のトークンランログ38プレイを集計した。アクティブプレイ時間は2時間47分05秒、クリア終了8回、未クリア終了30回。
- 初回クリアはStage 1が6プレイ目、Stage 2が21プレイ目、Stage 3が30プレイ目、Stage 4が31プレイ目。難易度2～5の全Stageクリアは34・35・36・38プレイ目。
- 累計トークン獲得は15,015、プレイ間残高差からの推定消費は10,915、最終残高は4,100で収支が一致した。
- 最終の難易度5全Stageクリアは8分、レベル32、9,341キル、3,158,279ダメージ、1,480トークン増加。

## Important Decisions

- 後半通常敵は1.5倍進行とし、エリートは通常敵の5倍、後半ボスは各ステージ先頭通常敵との従来比率を維持する。
- オークキングはHPだけを2倍化し、ゴブリンロードは接触攻撃力を維持したまま黒い弾だけを24ダメージにする。
- XPはボス・エリートを含めてHP14あたり1とし、整数にできない場合は切り上げる。
- ボス以外の移動速度は敵固有の倍率を上書きせず、現在ステージに応じた実速度加算で調整する。ボスは全ステージ0.279で固定する。
- エリートの接触ダメージは、対応する通常敵の2倍とする。ボスとボス特殊攻撃は今回の対象外とする。
- それ以外の速度・報酬は、現在の難易度を確認してから調整する。
- 通常敵のプレイヤー追跡距離は `GameConfig.normalEnemyPlayerAggroRangeCells=5` を正とする。
- 中心塔の建造後アップグレードは900固定とし、永続強化分を900へ加算しない。
- プレイ時間は各ログの `survivedSeconds` 合計を実プレイ時間とし、最初と最後の記録時刻差は休憩を含む経過時間として分けて扱う。
- トークン消費は、直前プレイ終了残高と次プレイ開始残高の減少分を合計した推定値として扱う。

## Files Changed

- `Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- `Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- `Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs`
- `Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs`
- `Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs`
- `Assets/AreaSurvivors/Scripts/Game/Buildings/BallistaTower.cs`
- `Assets/AreaSurvivors/Scripts/Game/Buildings/WoodenBarrier.cs`
- `Assets/AreaSurvivors/Scripts/Game/Buildings/WatchTower.cs`
- `Assets/AreaSurvivors/Editor/NormalEnemyPlayerTargetingValidator.cs`
- `ctx/current.md`

## Verification

- `GameConfig.cs` とImport後の `GameConfig.asset` で変更対象10種のHP倍率が一致することを限定検索で確認。
- 正式なAssetImportとクールダウン後、Unity Compile 1回成功。
- オークキングHP・黒い弾調整後も正式なAssetImportとクールダウン後、Unity Compile 1回成功。
- XP調整後もImport後の全20種の値を確認し、Unity Compile 1回成功。
- ステージ速度調整後、変更したAssetとC# 2ファイルをImportし、Unity Compile 1回成功。
- ボス速度固定後も変更したAssetとC# 2ファイルをImportし、Unity Compile 1回成功。
- `GameConfig.asset`、`PlayerStats.cs`、`GameManager.cs`、`Health.cs`の防御計算経路を読み取り確認した。防御関連の実装変更は行っていない。
- Console Error 0件。
- 対象2ファイルの `git diff --check` 成功。
- 通常敵プレイヤー追跡実装後、変更したC# 5ファイルをImportし、Unity Compile 1回成功。
- 専用Validatorで5セル境界、範囲外、中心塔優先、等距離、ボス除外、エリート除外を確認し、fresh marker生成成功。
- 追跡実装後のConsole Error / Warning表示ログは0件。
- 変更対象7ファイルの `git diff --check` 成功。
- 追跡範囲を5セルへ縮小後、関連C# 3ファイルをImportしてUnity Compile 1回成功し、5セル版Validatorもfresh marker生成成功。
- 5セル調整後のConsole Error表示ログ0件、対象5ファイルの `git diff --check` 成功。
- 通常・エリート敵の接触ダメージ調整後、変更したAssetとC#をImportし、Unity Compile 1回成功。
- ダメージ調整後のConsole Error表示ログ0件。
- ダメージ調整対象のC#・Asset・`ctx/current.md`で `git diff --check` 成功。
- `GameConfig.asset` と建造物Runtimeコードを読み取り、4系統の基礎HP・永続強化・建造後アップグレードの計算経路を確認した。建造物設定の変更は行っていない。
- 建造物HP調整後、変更したAssetとC# 4ファイルをImportし、Unity Compile 1回成功。Console Error表示ログ0件。
- 建造物HP調整対象のC# 4ファイル・Asset・`ctx/current.md`で `git diff --check` 成功。
- `token_run_log.jsonl` 全38行をJSONとして解析し、累計獲得15,015－推定消費10,915＝最終残高4,100を照合した。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機で通常敵の追跡先切替と、ボス・エリートが中心塔を狙い続けることを確認する。
- TODO: ユーザーが実機で新しい敵HPの難易度を確認する。
- TODO: ユーザーが実機で敵撃破時のXP量とレベルアップ速度を確認する。
- TODO: ユーザーが実機で各ステージの通常・エリート敵の速度上昇と、全ボスが0.279であることを確認する。
- TODO: ユーザーが実機で通常・エリート敵の新しい接触ダメージと防御力込みの難易度を確認する。
- TODO: ユーザーが実機で新しい建造物HPと建造後アップグレードの耐久感を確認する。
- TODO: 木の壁はスキル強化+20×最大15レベルを維持すると、アップグレード後の範囲が300～600になる。希望上限670を優先する場合のスキル強化仕様を確認する。
- TODO: 今回の全Stageクリアログを基に、難易度進行またはトークン経済を再調整する場合はユーザー指定を反映する。
- TODO: 前作業から継続して、アンロック済みキャラクターをロビーで選択できることをユーザーが確認する。
- Blockerなし。

## Next Action

- ログ集計結果を基に、必要なら未クリア回数が多い区間、難易度別の敵強度、トークン獲得／消費バランスを調整する。

# Previous Task Context

## Recent Completed Task (2026-07-23)

- Steamストアページ `Area Survivors` の公開を確認した。
- X告知では、独自性の高い「床を塗って敵を減速・エリア効果を発動」を冒頭で訴求し、ストア公開、ウィッシュリスト誘導、URLの順に記載する方針とした。
- 日本語版30秒PVをメイン投稿へ添付し、必要に応じて英語告知は別投稿に分ける。
- Steamページの実ブラウザ表示は詳細欄・発売予定欄とも `2026年8月7日`。Defuddleの取得HTMLだけ `2026年8月6日` だったため、サーバー基準日時とクライアント側ローカル表示の差と判断し、プレイヤー向け告知は8月7日を正とする。
- Xの通常投稿は地域限定できない。海外だけへ限定する場合はX AdsのPromoted-only Postで対象国・地域と言語を指定する。無料運用では英語版を通常の別投稿として公開する。

## Goal

スキルツリーでアーチャー／メイジをアンロック済みでも、ロビーのキャラクター選択でシルエットのままになる原因を特定し、アンロック状態を正しく反映する。

## Completed

- 直前状態の作業記録から、横向きは `直立 → 全身の踏み出し` の2ポーズ、上下は全身画像の最終キーだけUV反転する仕様だったことを確認した。
- 以前の作業で変更した画像が `Right_0.png` と、その完全反転である `Left_0.png` のみだったことを確認した。
- ほか10枚は、現在の `_1` とGit基準画像の画素一致、および直前作業記録を根拠にGit基準へ復元した。
- `Right_0.png` を既存StandingSourceから以前と同じ条件で再処理し、`Left_0.png` をその完全反転として復元した。
- Animator生成規約はメイジのみ旧仕様へ戻し、アーチャーの3姿勢仕様は維持するようコードを限定修正した。
- メイジ12枚を再ImportしてAnimatorを再生成し、専用Validatorを通した。
- 直近実装で追加したメイジ用NeutralLegsのSource/Cutoutと各meta（計16ファイル）を削除した。アーチャー用素材は維持した。
- 実セーブ `progression-save-v1.json` で `UnlockArcher=53`、`UnlockMage=54` がともにレベル1であることを確認した。
- `CharacterUnlockCatalog`、`ProgressionStore`、スキルノードのID対応は正常で、シルエットの原因がアンロック保存ではなくアイコン色の復元処理にあることを特定した。
- `[ExecuteAlways]` の `LobbyScreen` がEdit Modeでも `Start` / `Update` を実行し、黒く変更済みの色をアンロック時の復元色として保持し得る経路を修正した。

## Important Decisions

- メイジ横向きは `frames[0] → frames[1] → frames[0] → frames[1]` の全身2ポーズへ戻す。
- メイジ上下は同じ2ポーズ列を使い、最終キーだけ `flipHorizontal=1` とする旧仕様へ戻す。
- メイジ待機Clipは従来どおり `frames[1]` を表示する。
- `Right_0.png` は既存StandingSourceから以前と同じ処理で復元し、`Left_0.png` は完全反転とする。
- アーチャーの上下3姿勢と全身反転廃止は変更しない。
- `LobbyScreen` の実行時初期化はPlay Mode中だけ行い、キャラクター選択アイコンはアンロック時に白、未アンロック時に黒へ明示的に切り替える。

## Files Changed

- `Assets/AreaSurvivors/Editor/DirectionalCharacterAnimatorMigration.cs`
- `Assets/AreaSurvivors/Editor/DirectionalCharacterAnimatorValidator.cs`
- `Assets/AreaSurvivors/Sprites/Generated/Walk/Mage/Right_0.png`
- `Assets/AreaSurvivors/Sprites/Generated/Walk/Mage/Left_0.png`
- `Assets/AreaSurvivors/Scripts/UI/LobbyScreen.cs`
- `ctx/current.md`

## Verification

- Unity Compile 2回成功（復元Runner登録・実行、Runner削除後Refresh）。
- `Area Survivors/Validate/Player Directional Animator Migration` がfresh markerを生成して成功した。
- Console Error/Warningの取得結果はどちらも表示ログ0件。
- 12枚のコンタクトシートを目視し、上下左右とも腕と全身の姿勢差が復元され、左右の0番が直立姿勢であることを確認した。
- キャラクターアンロック修正のUnity Compile 2回成功（限定Validator Runner、Runner削除後Refresh）。
- `Character Unlock Skills` と `Lobby Character Selection` の両Validatorをまとめた限定Validatorがfresh markerを生成して成功した。
- 修正後のConsole Error / Warningはともに0件。
- Play Modeは開始していない。

## TODO / Blocker

- TODO: ユーザーが実機でアンロック済みキャラクターを選択できることを確認する。
- Blockerなし。

## Next Action

- ユーザーの実機確認結果を受け、必要ならロビーのキャラクター選択表示だけを限定調整する。
