# Map And Testing Rules

- `05_Game.unity` のGround TilemapはSceneに全セル保存せず、`TileGrid.Build()` の実行時生成を正とする。
- `90_GameplayTest.unity` は `05_Game.unity` のコピーにしない。空に近いBootstrap Sceneとして維持する。
- GameplayTestはScenario AssetとBootstrapで再現する。通常プレイでランダム発生を待つ検証は避ける。
- Scenario切り替えでScene差分を出さない。Scenario選択は `EditorPrefs` を使う。
- 通常検証はコード確認、Unity Compile 1回、関連GameplayTest 1件を目安にする。
- 大規模変更、再発バグ、見た目確認が必要な変更、ユーザー指定がある場合だけ完全検証を行う。
- よく使う確認は `unicli exec Compile`、`unicli exec Console.GetLog --logType Error --maxCount 30`、`git diff --check`。
- UniCLIやUnity検証が止まったように見える場合は、同じ呼び出しを繰り返す前にUnityの状態、プロジェクトロック、ログ、実行中コマンドを確認する。
- UniCLI `Eval` に複雑なC#コードを直接渡さない。Scene操作、Validator実行、移行処理は一時Editor Runner/Migratorを作成し、単純なEvalで呼び出す。
- Scene/Prefabの調整値は、可能なら小さなConfig asset、ScriptableObject、座標表、専用Reporter/Validatorへ逃がし、Scene/Prefab YAML全文を読まなくて済む構造にする。
- HUD、スキルツリー、建造メニューなどは、座標・重なり・参照欠けを要約するReporter/Validatorを優先し、全文diffや高解像度スクリーンショット確認を最後に回す。
- `.unity` / `.prefab` / `.asset` の差分確認は `safe-diff -SummaryOnly`、Reporter、対象オブジェクト検索を先に使う。本文diffは最終手段にする。
- Reporter追加候補を決める時は `reporter-candidates.ps1` を使う。
