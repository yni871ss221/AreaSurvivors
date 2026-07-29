# Combat Rules

## 武器進化

- 進化条件はプレイヤーへ表示しているラン中武器Lvを基準にし、基礎武器Lv.10以上で次回レベルアップ候補へ進化を優先挿入する。
- 進化後も装備順、表示Lv、ラン強化、レリック、特殊効果の内部キーは基礎武器のまま維持し、表示種別だけ進化武器へ変換する。
- 進化仕様で変更を明示されていない発射タイミング、同時発射、ターゲット配分、クールダウン経路は基礎武器の処理をそのまま共有する。進化固有分岐は明示された差分（例: 黄金の弓の攻撃力・貫通・色）だけに限定し、専用Validatorで基礎武器と共有する発射契約を検証する。
- 基礎武器と「同じサイズ」を指定されたProjectileは、別画像のPPUで近似せず、基礎Prefabと同じSprite、Transform Scale、Colliderを使う。弓系の一斉射は1クールタイムにつき1回、`min(矢本数, 射程内の敵数)`本を同一フレームで別対象へ発射し、敵が少ない時に同じ対象へ巡回配分しない。
- 発射回数やバースト数の不具合では、計算Helperの戻り値やタイマーFieldの存在だけをValidator成功条件にしない。1クールタイム内に実際の発射入口が何回通り、何個のProjectileが生成されたかを実行可能な限定テストまたは実機traceで確認し、同時発射と時間差の複数斉射を分けて検証する。
- 弓系の発射入口は、対象探索やProjectile生成より前にクールダウン予約を確定する。同一時刻の2回目は対象の有無にかかわらず拒否し、対象0体でも予約を消費して敵出現直後の多重斉射を防ぐ。05_GameはGameManager 1個・静的PlayerController 0個・静的WeaponController 0個、Player PrefabはPlayerController/WeaponController各1個をValidatorで保証する。
- 進化武器のテスト開始で表示Lv.10を作る時、通常ランの個別強化履歴とGameConfig内部Lv.10の全ステータス自動曲線を同一視しない。論理Lv・表示Lv・ステータス参照Lvを分離し、Reset後・Refresh前に基礎武器Lv.1を参照して、通常のレベルアップ候補に並ぶ各パラメータを2回ずつ強化した共通テストプロファイルを全進化武器へ適用する。特定進化武器だけLv.10自動曲線や最大弾数へ戻す例外を追加しない。
- 進化値は現在値の倍率計算ではなく、`現在の基礎武器値 + (進化武器の基礎値 - 基礎武器Lv.1基礎値)` で構成する。これにより、それまでのレベルアップ加算分を二重化せず引き継ぐ。
- 進化選択そのものでは表示Lvを加算しない。進化後の通常強化は基礎武器キーへ正規化して同じ表示Lvを継続する。
- 進化の初回獲得は永続Saveへ発見済みとして記録し、図鑑のネタバレ解除に使う。テスト初期化は進化発見状態だけを消し、通常の武器解放やラン状態を変更しない。

- 複数のEditor Menuへ同じ有効条件を付ける場合でも、1つのメソッドへ同型の`[MenuItem(..., true)]`を複数重ねない。UnityのMenu列挙は`Multiple custom attributes of the same type found`で停止するため、Menuごとに検証メソッドを1つずつ定義し、共通条件だけ通常メソッドへ委譲する。
- 複数フレームのSprite切り替え、落下・着弾・爆発の時系列Visualは、AnimationClipとAnimator Controllerを作成して対象Prefabへ直接設定する。RuntimeはAnimator Parameter設定または再生開始だけを担当し、`Update`、Coroutine、`Time.time`でSprite配列を切り替えない。
- プレイヤーの方向別歩行はAnimatorを正とする。キャラクター選択に伴うController選択は許可するが、選択後の方向・歩行フレームをRuntimeコードからSprite差し替えしてはならない。敵の方向別歩行は大量表示時の更新負荷と既存分散更新を比較し、共通Controller・共有Clipで同等以下の負荷を静的設計または限定計測で確認できた単位から段階移行する。
- `PaperMeshVisual`を維持するキャラクターAnimatorは、Clipからserialized field `sourceSprite`だけをPPtr Curveで変更し、`OnDidApplyAnimationProperties()`でMeshを反映する。Runtimeから`PaperMeshVisual.sprite`を歩行フレームごとに差し替えず、位置、Scale、Collider、YSort、遮蔽表示、アウトラインを移行時に変更しない。
- 画像を切り替えない建造物バウンス、単発着弾Spriteの拡大フェード、敵被弾時の白Overlay、PixelBurst、Projectileの移動・ホーミング・周回、範囲Mesh、Collider、ダメージ時刻はRuntime制御を維持する。フレーム画像を持つ爆発と混同して一括移行しない。
- `[ExecuteAlways]` の範囲Meshは、`Awake`、`OnValidate`、`CheckConsistency`中に`MeshFilter.sharedMesh`を設定・差し替えしない。形状変更はdirty flagへ記録して次の`Update`等の許可されたタイミングで反映し、`OnMeshFilterChanged`の`SendMessage cannot be called`警告を防ぐ。
- Animator移行後は旧Runtimeフレーム切り替えComponentとコードを削除する。Prefabに無効Componentとして残すこと、Runtime fallbackでSprite配列へ戻すことを禁止し、専用ValidatorでAnimator/Controller/Clip参照、Rotation X/Y=0、旧Component不在を確認する。
- 旧Animation MonoBehaviourのScript/`.meta`削除はPrefab移行完了後に行う。先に削除済みでMissing Scriptになった場合は、移行対象Prefabの全Transformへ`RemoveMonoBehavioursWithMissingScript`を適用し、全階層の欠損数0をassertする。Prefab保存は`SaveAsPrefabAsset`の戻り値を検査し、保存失敗をConsole Errorだけに残して処理継続しない。
- `PaperMeshVisual`から`SpriteRenderer`へ同一GameObject上で移行する場合は、旧Sprite・色・Sorting Orderを先に読み取り、`PaperMeshVisual`、`PaperBillboard`、`MeshFilter`、`MeshRenderer`を除去してから`SpriteRenderer`を追加する。Mesh系Componentが残った状態でSpriteRendererを追加しない。
- Animator管理へ移行したSpriteRendererのPrefab初期SpriteはAnimationClipの先頭フレームと一致させる。旧Visualから引き継ぐのは色、Material、Sorting Orderなどの見た目設定に限定し、旧静止SpriteでAnimator初期状態を上書きしない。
- 現在の武器制御本体は `Assets/AreaSurvivors/Scripts/Game/Weapons/WeaponController.cs`。Skill等に旧パス `Scripts/Game/WeaponController.cs` が残っていても使用せず、Path Validatorで拒否された場合は安全検索で現パスを確定する。
- `GameConfig`へ武器調整用のserialized fieldを追加する前に、同名fieldがCombat以外のセクションにも存在しないかファイル全体を限定検索する。ラン中プレイヤー能力と武器強化など用途が異なる値には、`runWeapon...`のように所有領域を含む名前を使い、CS0102をImport後に発見しない。
- 見た目と当たり判定が一致すべき攻撃は、調整可能なColliderを優先する。
- Knightの斬撃など、画面上の範囲が重要な攻撃で隠れた `OverlapBoxAll` / `OverlapCircleAll` 判定を残さない。
- 武器の範囲が広がる場合は、当たり判定だけでなく見た目のサイズも追従させる。
- `Circle Visual`、`Range Visual`、`Ellipse Range Outline`、`* Area Visual` など範囲そのものを示す表示は `PaperBillboard.faceCamera=true` や `Camera.main.transform.rotation`、Transform Rotation X/Y による疑似パース補正を使わない。
- 範囲表示、ダメージ判定、セル塗りが一致すべき攻撃は、同じ半径・縦横比からMesh/LineRenderer、Overlap候補、TileGrid塗りを計算する。見た目だけを傾けて合わせない。
- セル塗りと楕円表示を一致させる場合は `TileGrid.WorldCellSize()` を基準に縦横比を求め、楕円に少しでも重なるセルを塗る。固定値のRotation X `-40` 等で調整しない。
- AnimatorでSpriteを切り替える戦闘Visual、落下矢、着弾演出は`PaperBillboard`を付けず、PrefabのRotation X/Yを0にする。Animator Controller、Clip、表示Scale、SpriteRenderer参照はPrefabへ保存し、Runtimeは再生開始だけを行う。方向表現はZ回転だけを許容する。
- 落下開始高さや落下途中の位置はRuntimeコードでTransformへ加算せず、Prefabが参照するAnimationClipの`Transform.m_LocalPosition.y`キーとして保存する。フレーム数、高さ、着弾時刻はClipとPrefab Validatorを同時に更新し、Animationウィンドウから調整可能な状態を正とする。
- 同一TransformのYだけをAnimationClipで動かし、PrefabのXが0以外の場合、Unityは未設定Xを既定値0として再生中に上書きし得る。Prefab Xだけを調整して完了扱いせず、各対象の`m_LocalPosition.x`を定数カーブとしてClipへ保存するか、非アニメーション親AnchorへX配置を分離する。ValidatorはPrefab XとClip Xの両方を照合する。
- 落下矢など単体Spriteを`PaperMeshVisual`でフレーム切り替えする場合、Animator対象は通常Quadに限定し、`useEllipseShape=false`かつ`shapeSpriteOverride=null`をPrefab Validatorで確認する。攻撃範囲のFill/Outlineを別途複製する場合、その範囲コンテナへ`PaperMeshVisual`や旧エフェクトSpriteを混在させない。変更後は`Combat Visual Rotation Guard`で全武器Prefabを水平展開確認する。
- 着弾タイミングなどゲーム処理が依存する時刻を`AnimationClip`のAnimation Eventから逆算しない。EventがEditorメモリ上で取得できても`.anim`へ永続化されない状態と区別できないため、時刻はPrefab Componentのシリアライズ値として保持し、対応するSpriteキー時刻との一致をValidatorで確認する。100fps Clipの長さは最終キー時刻より1サンプル長くなるため、Validatorは保存後Assetの値を基準にする。
- `ArrowRainAreaVisual`等の参照フィールドを持つVisual Componentを別Prefabへ流用する際、`EditorUtility.CopySerialized`でComponent全体をコピーしない。色、幅、Sorting Order、縦横比など必要な値だけを個別コピーし、Sprite・Frame・子Visual参照は明示的に空または移行先Prefab内参照へ設定する。
- Prefab内のVisual子Objectを削除後に同名・同階層で再作成すると、`SaveAsPrefabAsset`の対応付けで旧fileIDと不要Componentが保持される場合がある。構成を置換するMigrationは移行先専用の子Object名を使い、保存後にPrefabを再ロードしてComponent数と旧fileID/旧Sprite参照の不在をValidatorで確認する。
- 落下矢のSprite import PPUは範囲Visualとは独立して管理し、矢の画面サイズを攻撃半径用Root Scaleだけで調整しない。既存アートより大きいと報告された場合は、Prefab Transformを上書きせず、専用Importer値とValidatorを同時に更新する。
- 既存Area Prefabから派生した攻撃Prefabへ新しい範囲コンテナを追加する場合、追加コンテナ内だけでなくPrefab全体の`PaperMeshVisual`を列挙する。Animator参照先以外の旧`Ellipse Range Outline`等はRoot直下を含めObjectごと除去し、全体個数をValidatorで1個へ固定する。
- 旧エフェクトSpriteの除去確認を`SpriteRenderer.sprite`だけで済ませない。Prefab全体の`PaperMeshVisual.sourceSprite`、AnimationClipのPPtr Curve、`AssetDatabase.GetDependencies(prefabPath, true)`を検査し、旧Spriteへの依存が0件であることを専用Validatorで固定する。新しいSpriteRendererが正しくても旧Mesh Visualが同時描画され得る。
- 火球や爆発のVisual Scaleを爆発半径に直結させない。見た目の大きさとダメージ範囲は必要に応じて別管理にする。
- 着弾時のPixelBurst系バーストは通常攻撃では不要。負荷や視覚ノイズを増やさない。
- 落下物や着弾Spriteの攻撃原点は画像中央と仮定しない。最終フレームの矢尻・接地面など、ダメージ中心となるピクセルを全フレーム共通のCustom Pivotへ設定し、Prefab原点・範囲Visual中心・当たり判定中心を一致させる。表示サイズは範囲半径やTransform Scaleで補正せず、専用SpriteのPPUで調整してValidatorへ期待値を固定する。
- 攻撃範囲と落下物・着弾物を同じPrefabへ格納する場合、範囲半径をPrefab RootへScaleして演出Spriteへ継承させない。動的な範囲Scaleは専用のRange Visual Containerだけへ適用し、演出SpriteはScale `(1,1,1)`を維持する。落下Spriteは`PaperBillboard.faceCamera=true`でカメラ正面を向け、地面に置くArea/Range Visualだけを非Billboardのままにする。
