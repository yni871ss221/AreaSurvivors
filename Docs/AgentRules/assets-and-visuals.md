# Assets And Visuals Rules

- 生成済みゲーム用Spriteは `Assets/AreaSurvivors/Sprites/Generated` に統一する。`Assets/AreaSurvivors/Resources/Generated` を新規追加しない。
- 画像素材追加・差し替えでは `area-survivors-asset-import` skill を使う。
- 取得画像をそのままゲームに使わない。原本は `Assets/AreaSurvivors/Sprites/External/*Source.png` に残し、背景透過、トリミング、解像度調整、既存資産とのサイズ比較、Unity Importer設定を行う。
- 建造物、アップグレード後表示、建造中表示、HUD画像、建造メニュー画像などの静的VisualはPrefab/Sceneに参照を持たせる。
- 静的Visualのために `GeneratedSpriteLoader.Load` でSpriteを実行時に当てはめない。スキルツリー/HUD/建造メニューのアイコンもScene/Prefabの serialized Sprite reference を正とし、Runtimeで差し替えない。
- ゲーム実行中に静的VisualやUIを新規配置・生成しない。静的なものはSceneへ直接配置し、動的に出るものはPrefab化してPrefab参照から生成する。
- `GeneratedSpriteLoader` は、歩行アニメ、弾、マップ外画像、地面バリアントなど、Prefab/Scene参照だけでは成立しない実行時選択が必要な非UI用途へ限定する。
- 画像差し替え時はPNGだけでなく、Prefab参照、Scene参照、TilePalette、Editor生成ツール、`GeneratedSpriteCatalog.asset`、古いSprite/Source/Prefab/Tile/Metaを確認する。
- 新規PNGを外部処理で`Assets/`へ追加した直後は、`.meta`未生成でもディスク上の正規パスをImporterが認識できる入口を使う。`GeneratedSpriteAssetUtility`はAssetDatabase検索だけで存在判定せず、直下の実ファイルを確認して`AssetDatabase.ImportAsset`へ渡す。Importer/Validatorは`.meta`生成、Sprite型、PPU、Point、Uncompressed、透過設定を確認する。
- built-in `image_gen` が180秒以上結果・エラー・保存先を返さない場合は想定外の長時間待機として停止し、`generated_images` に当該出力がないことを確認する。プロンプトを簡略化したbuilt-in再試行は1回までとし、再発時はblockerにする。ユーザーの明示許可なしにCLI/APIフォールバックへ切り替えない。
- built-in `image_gen`で`referenced_image_paths`を使う場合、`num_last_images_to_include`は値が0でも同時指定せず、キー自体を省略する。会話画像方式を使う場合だけ1〜5を指定する。生成前に`Tools/AssetGeneration/imagegen-request-preflight.ps1`を通し、`guard_code: 42/43/44`を解消してから実行する。
- 画像後処理Helperで `python` が必要な場合、PATH上の `python` / `py` を推測して呼ばない。Codex Workspace DependenciesからPython実体パスを取得し、その実体でSkill付属Helperを実行する。
- 建造物画像は背景除去、可視範囲トリミング、占有セル横幅 `セル数 * 64px` に合わせたアスペクト比維持リサイズを行う。高さはセルに無理に収めず、Prefabで下端と横幅を合わせる。
- 破壊済み建造物画像も占有セル横幅 `セル数 * 64px` に合わせる。破壊画像をセルに収めるためにPrefab/RuntimeでScale倍率を追加しない。`Transform Scale = 1` で成立するPNG寸法とImporter設定を正とする。
- Sprite比率や下端ずれをRuntimeのScale/Rotation/Y補正で直さない。
- `PaperMeshVisual.OnValidate` ではMesh/Renderer変更を直接実行せず、必要ならEditorの遅延実行で反映する。
- Unity 2022.3でSingle SpriteのCustom PivotをEditorコードから設定・検証する場合、`TextureImporter.spriteAlignment`を直接使わない。`TextureImporterSettings`を作成し、`ReadTextureSettings`→`settings.spriteAlignment`／`settings.spritePivot`→`SetTextureSettings`→`SaveAndReimport`の順で反映する。
