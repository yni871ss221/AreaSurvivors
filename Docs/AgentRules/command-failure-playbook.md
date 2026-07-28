# Command Failure Playbook

コマンド失敗、タイムアウト、無応答、想定外の戻り値が出た時に、機能実装を続ける前に原因と再発防止を残すための手順。

## 原則

- 別コマンド、別Shell、Eval、手動Editor操作へ自動的に切り替えない。まず失敗した方式の境界を調べる。
- 状態変更を止め、実行コマンド、引数、終了コード、経過時間、capture path、Unity Console/Editor状態を保存する。
- 同種の失敗を2回確認したら手打ち再試行を止める。3回目の前にWrapper、Validator、Reporterのいずれかへ部品化する。
- 共有作業ツリーでLeadとサブエージェントの`apply_patch`が同時刻に走り、実在する書込可能ファイルへ`Failed to write file`が出た場合は、同時ファイル操作の停止を全担当へ確認する。Attributes/IsReadOnlyを読み取り確認し、競合解消後の同じpatch再実行は1回だけとする。
- 原因確定、入口の防止策、限定的な自己テストが揃うまで元の機能実装を再開しない。

## 調査順序

1. `Safe-Command.ps1` の記録から、終了コード、`timed_out`、`capture_path`、実際に実行されたコマンドを確認する。
2. 失敗境界を Transport、CLI契約、Unity状態、AssetDatabase、C# Compile、対象データの6種に分類する。
3. CLI/serverのHandler実装、生成物の作成・削除タイミング、UnityのImport/Compile/Play遷移を読み取りで確認する。
4. 原因を再現する最小入力を、Unity状態を変えないValidatorまたは構文テストで固定する。
5. 危険入力を入口で拒否するか、安全な実行順を単一Wrapperへ固定する。自動フォールバックは禁止する。
6. `Tools/TokenUsage/command-tools-self-test.ps1` と対象Wrapper固有の限定テストを実行する。Unity Compile/Playを伴う確認は通常予算へ数える。
7. `AGENTS.md`、該当詳細ルール、必要ならObsidian `Knowledge/` と `mistakes.md` を更新する。

## 既知事例

### 構造Reporterが旧固定パスのまま停止する

- 症状: 現行クラスは存在するのに、Reporterが`<旧パス> not found`で対象読取前に終了する。
- 原因: Runtime等へのフォルダ再編後も、構造Reporterが移動前の単一ファイルパスを固定値で保持していた。
- 対応: 既知の狭い親ディレクトリ以下から対象ファイル名を再帰列挙し、候補が厳密に1件の時だけ続行する。0件と複数件は候補数と実パスを含む診断で停止し、自己テストで旧固定パスの再混入を拒否する。
- 禁止: エラーに表示された旧パスへファイルを戻すこと、候補の先頭1件を暗黙採用すること、Reporter失敗を理由に別の広域検索結果だけで構造判断を続けること。

### Sort-Objectの複数キーへDescendingを個別指定してParserErrorになる

- 症状: 新規PowerShell Reporterの構文自己テストが`Missing argument in parameter list`で停止する。
- 原因: `Sort-Object Key1 -Descending, Key2 -Descending`のように、switchを各キーへカンマ区切りで付与できると誤認した。
- 対応: 複数キーの昇降順は`@{ Expression = "Key1"; Descending = $true }`形式のproperty hashtableを列挙し、Reporter初回実行前に`command-tools-self-test.ps1`のParser検査を必須にする。
- 禁止: カンマやswitch位置を推測変更しながらReporterを直接再実行すること、Parser検査を外して本番データで構文確認すること。

### Windows PowerShell 5.1でgeneric Listを配列化すると型不一致になる

- 症状: Parser検査済みのReporterが、`@($genericList)`で`Argument types do not match`を返す。
- 原因: Windows PowerShell 5.1の動的配列化境界で`System.Collections.Generic.List[object]`を直接列挙した。
- 対応: generic Listは`@($genericList.ToArray())`へ明示変換する。新規集計Wrapperは小さいfixtureを使う`-SelfTest`を持ち、`command-tools-self-test.ps1`から本番データ読取前に実行する。
- 禁止: generic型を非generic `ArrayList`へ場当たり的に変更すること、実データで同じReporterを再実行して変換可否を試すこと。

### アセット監査がPackage GUIDと原本コピーを不要物へ誤分類する

- 症状: プロジェクト固有ルートだけのmeta辞書で多数の未解決GUIDを報告し、重複ハッシュの大半を削除候補として集計する。
- 原因: Scene/PrefabはUnity PackageのGUIDも参照する一方、辞書が`Assets/AreaSurvivors`だけだった。また`External/*Source.png`と生成版が同一内容でも、原本保存ルールを重複分類へ反映していなかった。
- 対応: GUID辞書は`Assets`、`Packages`、存在する`Library/PackageCache`のmetaを統合する。重複は`source-generated-preserved`、`historical-review`、`internal-review`へ分類し、削除候補量には後二者だけを含める。
- 禁止: プロジェクト固有metaにないGUIDをMissing参照と断定すること、ハッシュ一致だけでExternal原本または生成版を削除すること。

### functions.exec内のapply_patch成功戻り値が空になる

- 症状: `text(await tools.apply_patch(patch))`を実行したcellが終了コード相当の異常を出さず、出力だけ`{}`または空になる。一方で対象ファイルのsentinel行は反映済みになっている。
- 原因: 入れ子`apply_patch`は成功時に表示用の戻り値を返さない場合があり、その`undefined`相当を`text(...)`へ直接渡すと成功表示が生成されない。Patch適用結果と会話への戻り値は別境界である。
- 対応: `await tools.apply_patch(patch); text("patch_submitted; verify sentinels")`のように送信完了だけを明示し、成功判定は`safe-read.ps1 -LiteralPattern`で各変更対象の固有sentinelを確認してから`scoped-diff-check.ps1`で確定する。
- 禁止: 空戻り値をPatch失敗とみなして同じPatchを再適用すること、反映確認前に別編集方式へ切り替えること。

### UTF-8 BOM付きskill metadataの先頭行patch

- 症状: `SKILL.md`と`agents/openai.yaml`を同じ`apply_patch`へ含め、YAML先頭の`interface:`をcontextにしたところ、表示内容は一致していても`Failed to find expected lines`となり全patchが不適用になった。
- 原因: `openai.yaml`先頭にUTF-8 BOMがあり、見た目に出ない文字のため先頭行contextが一致しなかった。複数ファイルpatchだったため、YAML側の検証失敗がSKILL.md側も停止させた。
- 対応: skill本体は単独patchにする。`agents/openai.yaml`はskill-creatorの`generate_openai_yaml.py`で再生成し、BOM、改行、UI metadataを正規化する。既存YAMLを限定修正する場合も先頭行をpatch contextに含めない。
- 禁止: 同じ複数ファイルpatchの再試行、BOMを無視した先頭行context、SKILL.md未反映のままmetadataだけを更新すること。

### skill-creator Python群のWindows既定encoding

- 症状: UTF-8日本語を含む`SKILL.md`へ`generate_openai_yaml.py`または`quick_validate.py`を実行すると、`Path.read_text()`がCP932を使い`UnicodeDecodeError`で停止する。
- 原因: skill-creator付属Python群の`Path.read_text()`／`write_text()`にencoding指定がなく、Windows localeへ依存していた。
- 対応: `generate_openai_yaml.py`と`quick_validate.py`の読み取りを`encoding="utf-8-sig"`、generatorと`init_skill.py`の書き込みを`encoding="utf-8"`へ固定する。日本語skillでgeneratorとquick validatorが成功することを確認する。
- 禁止: 日本語を削って回避すること、手書きYAMLへ切り替えること、CP932でSKILL.mdを再保存すること。

### 入れ子Python呼び出しのregex検証

- 症状: encoding未指定の`write_text(...)`を探す正規表現が、`write_text(EXAMPLE.format(...), encoding="utf-8")`の内側`)`を外側呼び出し終端と誤認し、修正済み行をfalse positiveにした。
- 原因: 正規表現だけでは入れ子括弧を持つPython呼び出し構文を正しく解析できない。
- 対応: Python呼び出しの引数有無は実行fixture、AST、または既知call siteの限定読み取りで検証する。skill-creatorのUTF-8境界は日本語skillへのgenerator成功、BOMなしYAML、quick validator成功を根拠とする。
- 禁止: 入れ子括弧を含むPythonコードの正しさを単純regexのno-matchだけで確定すること。

### functions.exec内のshell_command戻り値をJSON化すると空になる

- 症状: `JSON.stringify(await tools.shell_command(...))` または戻り値の `.exit_code` / `.stdout` 参照が `{}` や未定義になり、実コマンドの成否と出力を会話側で確認できない。
- 原因: 入れ子`tools.shell_command`の戻り値は表示用結果であり、列挙可能な通常オブジェクトとして扱えない場合がある。実コマンドは成功していても、JSON化境界だけで情報が失われる。
- 対応: `const result = await tools.shell_command(...); text(result);` で戻り値をそのまま表示する。成否が欠落した既存実行は同じコマンドを再発行せず、`TokenReports/*.jsonl`の`exit_code`、`timed_out`、`capture_path`から確定する。
- 禁止: 入れ子`tools.shell_command`の戻り値を`JSON.stringify`すること、表示欠落を実コマンド失敗とみなして同じコマンドを再実行すること。

### 複数Tempファイル削除で先頭の既消去Pathがapply_patchを止める

- 原因: 前工程や自動クリーンアップで既に消えたTempファイルを、古い作業一覧のまま複数ファイルDelete patchの先頭へ含めた。`apply_patch`は適用前検証で停止するため、後続の実在ファイルも削除されない。
- 対応: `Tools/TokenUsage/temp-file-presence-report.ps1 -Path Temp/AgentAssets/<file>`で各候補を1件ずつ確認し、`temp_file_exists: true`の対象だけを1ファイル単位のDelete patchへ渡す。既消去は成功済みクリーンアップとして扱い、同じ複数Delete patchを再送しない。
- 禁止: 過去の作成ログだけでTempファイルが残っていると仮定すること、先頭Pathだけを外して未確認の複数Delete patchを再送すること。

### Domain Reload後にUniCLI server.pidがAssetImportWorkerのPIDになる

- 症状: C# Compileは終了コード0で完了し、メインUnityも応答中だが、直後の`Menu.List`等は同じpipeへ5回接続して`Connection timeout`になる。`Library/UniCli/server.pid`はメインUnityではなく、MainWindowHandleが0のAssetImportWorker PIDを指す。
- 原因: UniCLI v1.5.0の`UniCliServerBootstrap.EnsurePidFile()`がAssetImportWorkerでも実行され、メインEditorのPIDファイルを上書きし得る。Compile成功とUniCLI server再接続成功は別境界である。
- 対応: `safe-unity`はCompile以外のUniCLI操作前にserver.pidのPID、Unity process、MainWindowHandleを確認し、不一致を`guard_code: 45`で接続前に拒否する。プロジェクトは`Packages/com.yucchiy.unicli-server`の埋め込みパッケージを正とし、Bootstrapの先頭で`AssetDatabase.IsAssetImportWorkerProcess()`を判定してWorkerのPID書き込みとServer起動を禁止する。更新・復旧後は`Tools/TokenUsage/validate-unicli-worker-guard.ps1`を先に通し、メインEditorでStart Serverを1回押してから失敗した同一操作だけを再開する。
- 禁止: connection timeout後にMenu、Console、Statusを順番に試すこと、server.pidをShellで手動上書きすること、`Library/PackageCache`のBootstrapを直接改変すること、埋め込みパッケージを削除して未修正版へ戻すこと。

### Domain ReloadがMemoryStream corruptionで止まりUniCLIが再開しない

- 症状: CompileとAssembly Reloadは成功し、`server.pid`も応答中のメインEditor PIDを指しているが、Menu実行は5回ともconnection timeoutになる。Editor.logが`The file 'MemoryStream' is corrupted! Remove it and launch unity again!`と`[Position out of bounds!]`で止まり、UniCLIサーバー再開ログがない。
- 原因境界: C# CompileやAssetImportWorker PID誤登録ではなく、Domain Reload後のEditor内部状態復元で停止している。ログに実ファイルパスがないため、`MemoryStream`という名前のファイルを推測して削除してはいけない。
- 既知トリガー: Inspectorで選択中の`ScriptableObject`型へserialized fieldを追加し、Editor RunnerがRuntime/Editor依存Scriptを逐次Importすると、Csc中の追加timestamp変更で`Tundra build interrupted`が反復した後、最終Compile成功後の状態復元が`Read ... bytes but expected ... bytes`で破損し得る。
- 対応: メインEditorのPID、MainWindowHandle、Responding、command lineを`unity-process-report.ps1 -IncludeCommandLine`で確認し、失敗した同一Unity操作だけを最大1回再試行する。同じtimeoutが続いたらUnityを通常再起動し、再起動後に元のMigration/Validatorをfresh marker付きで再開する。serialized layout変更時は対象AssetをInspector選択したまま依存Scriptを逐次Importせず、Runtime型のDomain Reload完了後にEditor Menu検証へ進むか、Importを一括化してCsc中の追加timestamp変更を防ぐ。再起動後も再発する場合はEditor.logで破損対象の実パスを確定するまで削除しない。
- 禁止: 別MenuやEvalへ切り替えること、Scene YAMLを手編集してMigrationを迂回すること、パス不明の`MemoryStream`候補やLibrary全体を削除すること。

### sandbox内のMainWindowHandle=0をAssetImportWorkerと誤判定する

- 症状: `safe-unity AssetImport`等が接続前に`guard_code: 45`で停止するが、同じPIDを権限外の`unity-process-report.ps1 -IncludeCommandLine`で確認すると、window titleと非0のMainWindowHandleを持つ応答中のメインEditorである。
- 原因: sandbox内の`Get-Process`では権限境界によりメインEditorの`MainWindowHandle`が0に見える場合がある。旧PID Guardは0をAssetImportWorkerと即断し、実際のcommand lineを確認する前に誤分類していた。
- 対応: PIDがUnityでMainWindowHandle=0の場合だけ`Get-CimInstance Win32_Process`でcommand lineを確認する。command line取得自体がアクセス拒否ならWorker異常ではなく`guard_code: 26`とし、同じ`safe-unity`コマンドを権限昇格付きで1回だけ再実行する。権限外で`AssetImportWorker`または`-batchMode`を確認できた場合だけ`guard_code: 45`とする。
- 禁止: `server.pid`を手動上書きすること、メインEditorである証拠があるのにStart Serverを反復すること、Import・Compile・Menuを別経路へ切り替えること。

### Obsidian CLIをPATH登録済みと仮定して直接呼ぶ

- 原因: ObsidianアプリまたはローカルVaultが存在していても、`obsidian` CLIがWindows PowerShellのPATHへ登録されているとは限らない。未登録環境では対象ノートへ到達する前に`CommandNotFoundException`で終了する。
- 対応: 既存ローカルノートへの追記は `append-vault-note.ps1 -VaultRoot <vault> -RelativePath <existing.md> -ContentPath <utf8-file>` を固定入口とし、事前に`-WhatIf`を通す。CLI固有機能が必要な場合だけ、状態変更前に`Get-Command obsidian`で利用可否を検証する。
- 禁止: `obsidian`失敗後に実行ファイル候補やPATHを推測して再試行すること、VaultへShellの直接追記で迂回すること。

### 生rgへ既知Pathと推測Pathを混在させる

- 原因: 既知の実在ファイルと、ファイル名から補完した未確認候補を同じ `rg` / `rtk rg` へ列挙し、候補側だけが `os error 2` になった。既知ファイルの一致が同時に返ってもコマンド全体は成功扱いにできない。
- 対応: 複数の明示Pathを生rgへ渡さない。既知Pathは1回につき1つだけ検索し、未知のPathは `safe-search.ps1 -FilesOnly` で実在確認してから限定読み取りする。
- 禁止: 正しい一致行が一部返ったことを理由にエラーを無視すること、別の候補Pathを同じ形式で追加再試行すること。

### 広域 `git diff --check` が対象外Scene/Prefabの末尾空白を大量検出する

- 原因: 既存の未コミット変更を含む作業ツリー全体へ差分検査をかけ、今回の所有範囲とユーザー差分を分離しなかった。
- 対応: `scoped-diff-check.ps1` で今回変更したファイルを1つずつ検査する。対象外のScene/Prefabを整形・保存し直さない。
- 同一Scene/Prefab内に以前の未コミット変更がある場合、ファイル単位のscoped検査でも既存のUnity YAML空値行（`m_Name: `等）を検出する。この場合は`Safe-Command.ps1`で同一ファイルのdiffをcaptureし、今回変更したObject名・fileID・serialized fieldだけを`safe-read.ps1 -Pattern`で確認する。
- 同一ファイル内の既存空白が原因なら、終了コード2を今回のScene移行失敗とは扱わない。Reporter/Validator、Compile、Console結果と今回変更フィールドの限定diffを根拠に判定する。
- 禁止: 広域結果を今回の実装破損とみなし、ユーザーの既存差分へ空白除去や再保存を行うこと。

### `rtk pwsh` またはWrapper引数名が見つからない

- 原因: Windows環境にPowerShell 7の `pwsh` がない、またはWrapperの正式契約（例: `safe-read.ps1 -Path`）と手元の記憶が一致していない。
- 対応: 対象処理へ未到達であることを確認し、固定入口 `rtk C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe ...` とWrapperのparam定義をルール・自己テストへ固定する。
- 禁止: 実体名や引数名を推測して手打ち再試行を重ねること。

### リポジトリ内Wrapperをユーザー領域の同名パスと誤認する

- 原因: `Tools/TokenUsage/*.ps1` がリポジトリ相対パスであることを確認せず、`C:\Users\<user>\.codex\Tools\TokenUsage` 配下に存在すると補完した。また、存在診断へWindows PowerShell 5.1非対応の三項演算子 `? :` を含めた。
- 対応: Wrapperは先に `<project-root>\Tools\TokenUsage\<name>.ps1` の実在を単純な `Test-Path -LiteralPath` で確認する。診断式も固定入口のWindows PowerShell 5.1構文だけを使い、型や属性の追加確認は存在確認成功後に分離する。
- 禁止: `.codex` 配下へプロジェクトWrapperのパスを補完すること、診断用の単発式にPowerShell 7専用構文や複数段の条件式を埋め込むこと。

### 行数不明ファイルへ`safe-read -PrintOutput`の推測上限を渡す

- 症状: 実ファイルが80行未満でも、`safe-read.ps1 -StartLine 1 -EndLine 150 -PrintOutput`のような指定が`guard_code: 39`で停止する。
- 原因: `safe-read`の対話表示ガードは実際のEOFではなく指定範囲の推定行数を入口で判定する。Skill全文読取で十分大きい上限を指定し、`safe-read-batch`へ先にルーティングしなかった。
- 対応: 行数不明のSkill、Markdown、ルール本文は最初から`safe-read-batch.ps1 -Ranges "1-<十分な上限>" -PrintOutput`で80行ずつ読む。80行以内と確定済みの範囲だけ`safe-read -PrintOutput`を使う。
- 禁止: `safe-read`の`-EndLine`を80、120、150のように手探りで変更して再試行すること、EOFが短いはずという推測で80行超の指定を直接渡すこと。

### `safe-unity ConsoleErrors` の件数引数が見つからない

- 原因: 検索・読み取りWrapperの `-First` と、Unity Console入口の正式契約 `-MaxCount` を混同した。
- 対応: `safe-unity.ps1 -Action ConsoleErrors -MaxCount <件数>` を使い、自己テストでparam転送を固定する。

### Play Mode終了直後の連続性能計測がguard 23で止まる

- 症状: `safe-unity.ps1 -Action PlayExit`成功直後に次の性能シナリオ準備Menuを呼ぶと、`guard_code: 23`でPlay Mode遷移中として停止する。
- 原因: 1回目の計測シーケンス終了と2回目のシナリオ準備を連続実行し、`last-playmode-exit.utc`の20秒クールダウン内にUniCLI Menuへ到達した。
- 対応: 連続戦闘性能計測は`combat-performance-probe.ps1`を入口とし、同Wrapperがmarker時刻から残りクールダウンを待ってから次のMenuを実行する。PlayExit後に別のUnityコマンドを手打ちで続けない。
- 禁止: guard 23を無効化すること、別Menu/Evalへ切り替えること、markerを手動削除して遷移中のUnityへ接続すること。

### AssetImportへフォルダを渡した後もCompileがstale判定される

- 症状: `safe-unity AssetImport -AssetPath <folder>` は成功応答を返すが、続くCompileが `guard_code: 41` で古いAssemblyを拒否する。
- 原因: UniCLIのAssetImportは `AssetDatabase.ImportAsset(path, ForceUpdate)` を呼び、フォルダ指定時に `ImportRecursive` を付けないため、配下の変更Scriptを再Importしない。
- 対応: `safe-unity AssetImport` は既存の単一ファイルだけを受け付ける。変更した各 `.cs` を1件ずつImportしてからCompileする。全体Refreshを意図する場合だけ `AssetRefresh` を明示使用する。
- 禁止: フォルダImportの成功応答を再帰Import成功とみなすこと、stale判定後に同じCompileを再実行すること。
- 禁止: `-First`、`-Count`などの候補を手打ちで順番に試すこと。

### safe-readの80行対話上限を同一作業で再発させる

- 症状: `safe-read.ps1 -PrintOutput`へ81行以上の範囲を渡して`guard_code: 39`になった後、別ファイルでも同じ形式を使って再度停止する。
- 原因: Guard出力と`token-tools.md`が`safe-read-batch`を示しているのに、対象ファイルが変わったことで同じ呼び出し形式を再利用した。
- 対応: 1回目の`guard_code: 39`以降、その作業では行範囲指定の`safe-read.ps1`を使わない。読み取り対象や行数にかかわらず`safe-read-batch.ps1 -Ranges "<start>-<end>" -PrintOutput`へ固定し、自動80行分割へ任せる。
- 検証: Guardは対象読取前に停止して状態変更0であること、`safe-read-batch`の限定読み取りと`command-tools-self-test.ps1`が成功することを確認する。
- 禁止: 対象ファイルを変えて同じ`safe-read`形式を使うこと、`-AllowMany`で対話上限を迂回すること。

### 複数のsafe-read出力を1回のShellへ合算してcontext超過

- 症状: 個々の`safe-read-batch.ps1`は成功しているが、複数ファイル・複数範囲を同じShellで直列実行した結果、会話への合算出力が途中で切れる。
- 原因: 各Wrapperの80行上限は個別呼び出し単位であり、外側Shellが返す合計出力量までは制限しない。
- 対応: `-PrintOutput`付きの読み取りは1回のShellにつき1ファイル・1つの確認目的に限定する。次のファイルは前の結果を確認してから別コマンドで読む。
- 検証: 対象ごとの限定Pattern読取が終了コード0で完了し、必要なsentinelとcapture pathを取得できることを確認する。
- 禁止: 成功出力をセミコロン等で1つのShellへ合算すること、切れた同じ大規模コマンドを再発行すること。

### 未確認contextを含む複数ファイルapply_patch

- 症状: 複数ファイルPatchの末尾ファイルで`Failed to find expected lines`となり、先行ファイルを含む全変更が未適用になる。
- 原因: 一部だけ読んだメソッドの末尾を推測し、実在確認していない行列をPatch anchorへ含めた。
- 対応: 複数ファイルPatchは全anchorを事前に限定読取する。1件でも未確認contextを含む場合はファイル単位へ分け、失敗後はsentinelで全体未適用を確認してから正確な最小anchorで続行する。
- 検証: 失敗Patchの先頭対象sentinelが旧状態のままであることと、修正版Patch後の各sentinelを個別に確認する。
- 禁止: 一部一致を根拠に同じ複数ファイルPatchを再発行すること、未確認のメソッド末尾や隣接メソッド名をanchorに使うこと。

### Runtime field削除後の裸シンボル参照漏れ

- 症状: field宣言、代入、代表的な早期returnを削除して限定検索0件と判断したが、別メソッドの条件式に裸のfield参照が残りCompile Errorになる。
- 原因: 削除対象シンボルそのものではなく、想定した利用パターンだけをOR検索した。
- 対応: Runtime fieldを削除・改名する前後は、対象RuntimeファイルとEditor経路でシンボル名そのものを完全一致検索する。利用形別検索は補助に限定する。
- 検証: 全対象で裸シンボル0件、Editor Validatorの旧field参照0件を確認してからCompileする。
- 禁止: 宣言・代入・特定条件式だけの検索結果を全参照確認として扱うこと。

### Graphify出力の空行でMandatory string配列が停止する

- 症状: `safe-graphify-pilot.ps1 -Action Explain`のGraphify本体は結果を返すが、`Get-GraphifyOutputSignals -OutputLines`で`Cannot bind ... because it is an empty string`となる。
- 原因: native出力配列に空行が含まれ、PowerShellの`[Parameter(Mandatory)] [string[]]`が空文字要素を拒否した。Graphify検索ではなくWrapperの出力解析境界である。
- 対応: 出力解析用配列へ`[AllowEmptyString()]`と`[AllowEmptyCollection()]`、結合後テキストへ`[AllowEmptyString()]`を明示する。空行は保持したままcaptureと使用記録へ流す。
- 検証: `command-tools-self-test.ps1`で属性の再脱落を拒否し、失敗した同一`Explain`だけを1回再実行する。
- 禁止: 空行をGraphify結果なしと誤認すること、`focused-search`等へ迂回して失敗境界を残すこと。

### Graphify Explainが同名のSource欠落ノードを正常扱いする

- 症状: Runtimeクラス名を`Explain`したのに、Editor Validator内の同名ノードIDが選ばれ、`Source:`が空、接続1件だけの結果を終了コード0で返す。
- 原因: Graphifyの同名ノード解決が別ファイルの抽出ノードへ寄り、Wrapperも空の`Source:`を曖昧結果として検出していなかった。
- 対応: `Explain / Path / Affected`結果の`Source:`が空なら`missing-source-path`を理由に`graphify_verification_required: true`とし、既存の`focused-search` fallbackを表示する。
- 検証: `command-tools-self-test.ps1`で判定sentinelを固定し、同一Explainがfallback要求を返すことを確認する。
- 禁止: node名が一致しただけで依存証拠にすること、Source欠落結果から呼び出し関係を推測すること。

### `safe-unity Screenshot`へ絶対パスを渡す

- 原因: 通常のローカル画像参照と、Unity Screenshot入口の保存先契約を混同した。
- 対応: `safe-unity.ps1 -Action Screenshot -ScreenshotPath Temp/<name>.png`のように、プロジェクト相対かつ`Temp/`または`TokenReports/`配下のPNGだけを渡す。絶対パス、`..`、他拡張子は入口Guardで拒否する。
- 禁止: Guard拒否後に絶対パス表記を変えて試すこと、別Screenshot方式へ切り替えること。

### `safe-search` の日本語一致行だけが文字化けする

- 原因: native `rg` のUTF-8出力をWindows PowerShell 5.1が既定コードページで復号する境界。
- 対応: `Safe-Command.ps1` の子PowerShellでConsole入出力と `$OutputEncoding` をUTF-8へ固定し、日本語を含む `safe-search` 限定自己テストを通す。
- 禁止: 文字化けした検索結果をソース内容の破損と解釈すること、または別検索方式へ切り替えて機能実装を続けること。

### Windows PowerShell 5.1でUTF-8 `.ps1` の日本語リテラルがParserErrorになる

- 原因: BOMなしUTF-8スクリプトをWindows PowerShell 5.1がANSIとして読み、日本語のバイト列が引用符などへ誤解釈される。
- 対応: Wrapperや自己テストの非ASCII文字列はUnicodeコードポイントから実行時に組み立てる。ファイル全体のエンコーディングを場当たり的に変更しない。
- 禁止: 文字化けしたParserErrorを括弧や引用符の単純な記述ミスとして推測修正すること。

### 新規EditorスクリプトのMenuが見つからない

- 原因: UniCLI `Compile` は `CompilationPipeline.RequestScriptCompilation()` を呼ぶが、`AssetDatabase.Refresh/ImportAsset` は呼ばない。
- 対応: `invoke-unity-editor-runner.ps1 -Phase RegisterAndRun` で Import→Compile→Menu完全一致確認→Execute を固定する。
- 禁止: Menuが見つからない時に、同じ処理を長いEvalへ移して続行すること。

### Editor Runner Compileで新規依存型だけが見つからない

- 原因: Runner本体だけを`AssetDatabase.Import`し、同時に追加したRuntime/ValidatorスクリプトがAssetDatabaseへ未ImportのままCompileした。
- 対応: `invoke-unity-editor-runner.ps1 -DependencyScriptPaths` にセミコロン区切りで全依存C#を列挙し、全Import完了後にCompileを1回だけ実行する。
- 禁止: Missing typeごとにCompileを繰り返すこと、またはAssetDatabase未Importをusing/asmdefの問題と推測してコードを書き換えること。

### Compile検証がAssemblyの更新時刻だけを根拠にstale判定する

- 原因: Unity/Beeは再Compile結果が既存`Library/ScriptAssemblies`と同一内容の場合、配布先DLLの更新時刻を進めないことがある。最新ソースよりDLLのmtimeが古いだけではCompile失敗を証明できない。
- 対応: `verify-unity-script-compilation.ps1` はmtimeが古い場合、ソース更新後に生成された最新Bee artifactと`ScriptAssemblies`のSHA-256一致を第二成功条件として確認する。Editor.logの`Tundra build success`、Domain Reload完了、Bee artifact時刻・Hashを原因調査の証拠にする。
- 禁止: mtimeのstale結果だけでC#エラーと断定すること、同じCompileを再実行して時刻だけを更新しようとすること、DLLの時刻を手動変更すること。

### Compile鮮度待機がguard_code 41で終了しConsole Errorが空になる

- 原因: AssetImportはC# Compileを開始したが、C#エラーでBee buildが終了してEditor Assemblyが更新されなかった。`verify-unity-script-compilation`はAssembly鮮度だけを見て`guard_code: 41`を返し、`Console.GetLog`にはcompiler errorが載らない場合がある。
- 対応: 指定timeout内の単一鮮度待機後もstaleならCompileを再発行せず、権限付き`safe-read -Last 80`でUnity `Editor.log`末尾の`ExitCode`／`Tundra build failed`／CSエラーを確認する。今回のUnity 2022.3では`TextureImporter.spriteAlignment`が存在せず、`TextureImporterSettings`経由へ修正する必要があった。
- 禁止: Console Errorが0件という理由だけでC#エラーなしと断定すること、stale検証を3回目へ進めること。

### Console.GetLogのError結果が空でもtotalCountが1以上になる

- 原因: UniCLIの`Console.GetLog`は`totalCount`へ全Console種別の総数を返し、`logs`と`displayedCount`だけを`--logType`で絞り込む。Validator成功の通常Logだけがある場合も、Error取得は`logs: []`、`displayedCount: 0`、`totalCount: 1`になる。
- 対応: 指定種別の有無は`logs`と`displayedCount`で判定する。内訳が必要な場合は同じ時点の`ConsoleLogs`を最大件数付きで1回だけ取得し、`totalCount`の内容を確定する。
- 禁止: `totalCount`をError件数として扱うこと、または空のError結果だけを理由にCompile鮮度失敗を無視すること。

### 全体Localization Coverageが対象外の既存未登録文言で失敗する

- 症状: 今回追加した文言は翻訳辞書へ登録済みだが、`Area Survivors/Validate/Localization Coverage`が別Scene・Prefabの既存文言を列挙してsuccess markerを作らない。
- 原因境界: 今回変更した文言の不足ではなく、全Scene・Prefabを対象にするValidatorが既存の未解消項目も同時に検出している。Menu受付成功だけでは合格扱いにしない。
- 対応: Console Errorで今回の対象文言が一覧に含まれないことを確認し、機能専用Validatorで対象文言ごとの`LocalizationTextCatalog.Translate(..., GameLanguage.English)`期待値を検証する。既存未登録文言は別TODOとして分離する。
- 禁止: 対象外の全翻訳を便乗修正すること、全体Validator失敗を今回の文言不足と推測して辞書項目を重複追加すること、success markerなしで全体Validator成功と報告すること。

### 外部編集したC#をImportせずsafe-unity Compileへ渡してstaleになる

- 原因: `safe-unity -Action Compile`を再Compile要求と誤認した。このActionは現行Assembly/Bee artifactの鮮度検証だけを行い、AssetDatabase Importを実行しない。Unity外で変更したRuntime/Editor C#が未Importなら、C#エラーの有無に関係なく旧Assemblyのまま`guard_code: 41`になる。
- 対応: 変更した全C#を`AssetImport`するか、Editor Menu実行を伴う作業は`invoke-unity-editor-runner.ps1 -Phase RegisterAndRun -DependencyScriptPaths ...`で依存を全列挙し、Import→Compile→Menu完全一致→実行を1シーケンスに固定する。その後のCompile Actionは検証として数える。
- 禁止: stale後に同じCompileを再発行すること、1ファイルだけImportして依存C#を残すこと、staleをコード構文エラーと推測してソースを書き換えること。

### 長いEvalでC#の引用符が欠落する

- 原因: PowerShell→RTK→ネイティブCLIの引数境界で引用符を含むコードが変形し得る。Eval serverは受信コードをそのまま生成C#へ埋め込み、失敗後は生成C#を削除する。
- 対応: `safe-unity.ps1` は引用符・改行を含むEvalを `guard_code: 25` で拒否する。Editor Runnerを使う。
- 証拠保全: Evalのcapture outputを先に保存する。`Temp/UniCliEval` の生成C#が失敗後も残る前提で調査しない。

### PlayMode.Exit直後の状態確認が長時間化する

- 原因: Exit要求成功とUnityの終了遷移完了は同義ではない。遷移中の後続UniCLIが待機する場合がある。
- 対応: PlayExitを検証列の最後にし、`safe-unity.ps1` の20秒cooldownと強制timeoutを使う。

### PowerShellの配列スプラットで名前付き引数が位置引数になる

- 原因: `@("-Note", $Task, ...)` の配列スプラットは文字列を位置引数として渡し、パラメータ名として再解釈されない。
- 対応: 呼び先がPowerShellスクリプトなら `@{ Note = $Task; UiPercent = ... }` のhashtableスプラットを使う。

### 調査用の入れ子PowerShellで変数が消える

- 原因: 外側PowerShellの二重引用文字列内に `$variable` を含めると、内側へ渡る前に外側で展開される。
- 対応: 長い調査式を `-Command` へ埋め込まない。`safe-read.ps1`、`focused-search.ps1`、またはapply_patchで作成した限定スクリプトを `-File` で呼ぶ。

### Native commandの件数制限でbroken pipeになる

- 原因: `rg -l ... | Select-Object -First N`や`git show ... | Select-Object -First N`は、N件到達時にPowerShellが入力パイプを閉じ、Native command側が終了コード`-1`または`1`になる場合がある。要求行が表示されても、Native command完了前に切断されているため成功扱いにできない。
- 対応: `rg`や`git show`の全出力と終了コードを先に配列へ取得し、実エラーを伝播した後、配列へ`Select-Object -Skip/-First`を適用する。通常検索、`safe-search.ps1 -FilesOnly`、`focused-search.ps1`はこの順序を固定する。履歴ファイルの限定読取も`$lines = @(git show <revision>:<path>); $gitExit = $LASTEXITCODE`を先に完了させてから範囲を抽出する。
- 禁止: Native commandを`Select-Object -First`へ直接パイプすること、必要な標準出力が見えたことだけで非0終了を無視すること。

### Web資料検索の出力超過後に直接URLをopenして拒否される

- 原因: 複数のWeb検索を1回へ集約して結果が切れた後、検索結果の参照IDを保持できないまま公式URLを直接`open`した。WebツールはそのURLを安全な参照先として認識できず、非再試行エラーを返した。
- 対応: API資料の調査は単一クエリか既知ページ1件だけを`response_length: short`で検索し、返された参照IDを同じ調査列で`open`する。検索出力が切れた場合は実装を止め、取得済みのローカル証拠で原因境界を確定するまで別URLや別Web手段へ切り替えない。
- 禁止: 複数クエリのmedium/long応答、切れた検索結果の直接URL再構成、同一URLの表記変更による再試行。

### 新規PNGのMigrationが成功表示でもmetaを生成していない

- 原因: 外部処理で`Assets/`へ配置したPNGはまだAssetDatabaseへ未登録であり、AssetDatabase検索だけでパスを解決するImporterがnullを返して処理を中断した。Menu実行APIの`executed=true`はMenu呼び出し成立だけを示し、内部例外やMigration完了を保証しない。
- 対応: 正規のプロジェクト相対パスに対応するディスク上の実ファイルを先に確認し、そのパスを`AssetDatabase.ImportAsset`へ渡してからSprite設定を適用する。Migration/Validatorは成功時だけ`Library/AreaSafeUnity`へ完了マーカーを作成し、Menu実行結果だけで成功判定しない。
- 禁止: `.meta`不在を画像名やUnity接続不良と推測して別Importerへ切り替えること、`executed=true`だけでPrefab保存済みと報告すること。

### 画像後処理が書き込み完了表示後にtimeoutする

- 症状: 画像処理スクリプトが`Wrote <path>`と統計を出力した後、外側コマンドが終了コード124でtimeoutする。
- 原因境界: 既定10秒が画像デコード・透過処理・PNG圧縮・終了処理の合計より短く、成果物書き込み後の終了待ちで外側だけが打ち切られる場合がある。
- 対応: 同じ処理を再発行せず、残存プロセス、対象PNGの存在・更新時刻、画像デコード、alpha cornerとsubject coverageを読み取り検証する。PNGが正常なら成果物を採用し、次回の同処理は最初から30秒以上のtimeoutを確保する。
- 禁止: timeoutだけを根拠に同じ画像処理を再実行すること、検証前に成果物を削除・上書きすること。

### append-vault-noteで未確認のKnowledge名を指定する

- 原因: `append-vault-note.ps1`は既存ノートへの追記専用であり、存在未確認の新規`Knowledge/*.md`を自動作成しない。
- 対応: その作業内または直近の証拠で実在確認済みのノートへ追記する。新規ノートが必要な場合はObsidian記憶ルールに従う明示的な作成手順を別途使い、追記Wrapperへ作成を期待しない。
- 禁止: 類似名を推測してappendを反復すること、失敗後に外部VaultへShell書き込みで迂回すること。

### focused-searchの引数・Path契約違反

- 原因: `focused-search.ps1` に `safe-search.ps1` 用の `-Query` / `-FilesOnly` を推測で渡す、または `powershell -File` で複数Pathをカンマ結合した1文字列として渡す。
- 対応: `focused-search.ps1 -Pattern <語> -Path <既存パス>` を使う。検索対象ファイル数は `-TopFiles`、またはsafe-searchと共通のalias `-First` で指定する。Wrapperは各Pathの存在を `rg` より前に検証し、ファイル探索の非ゼロ終了をcapture path付きで失敗させる。

### safe-searchへ未定義のRoot引数を渡す

- 原因: 検索ツール一般の用語から`-Root`を推測し、`safe-search.ps1`のparam定義にある`-Path`を確認せず実行した。
- 対応: 正式契約を`safe-search.ps1 -Pattern <regex> [-Path <既存パス>]`としてhelp、詳細ルール、自己テストへ固定する。検索起点は常に`-Path`を使う。
- 禁止: `-Root`、`-Directory`、`-BasePath`を順番に推測して再試行すること。

### safe-searchへ未定義のMaxResults引数を渡す

- 原因: 他の検索APIで使う件数指定名から`-MaxResults`を推測し、`safe-search.ps1`の正式な`-First`を確認せず実行した。
- 対応: 初回利用時にhelpまたは`param(...)`を読む。正式名は`safe-search.ps1 -First <件数>`で、既存の検索APIから持ち込みやすい`-MaxMatches`だけ互換aliasとして受け付ける。20件を超える意図的な検索だけ`-AllowMany`を併用する。
- 禁止: `-MaxResults`、`-Limit`、`-Top`を順番に試すこと。ParameterBindingException後は検索を再発行する前に正式契約へ戻る。

### safe-readのFirstが80行を超える

- 原因: 既定の会話出力上限80行を確認済みでも、ファイル全体の想定行数をそのまま`-First`へ渡した。
- 対応: `guard_code: 39`が返す`suggested_first=80`以下へ限定する。80行を超える意図的な単一範囲は`safe-read-batch.ps1`へ元の範囲を1回だけ渡して自動分割する。
- 同一タスク中に`guard_code: 39`が一度でも発生した後は、そのタスクの残りで`safe-read.ps1 -Pattern`を手打ちしない。既知ファイルは`safe-read-batch.ps1 -Ranges '<start>-<end>'`の80行自動分割へ固定し、未知ファイルは`safe-search.ps1 -FilesOnly`でパスだけ確定してから同じbatch入口で読む。
- 禁止: `-First 100`等のGuard後に79、80などの候補を手打ち再試行すること。

### safe-readへTailを渡してParameterBindingExceptionになる

- 原因: 内部の`Get-Content -Tail`または一般的な末尾読み取り名から、正式引数`-Last`を確認せず`-Tail`を推測した。
- 対応: 正式名は`safe-read.ps1 -Last <行数>`とし、`-Tail`は互換aliasとしてWrapperと自己テストへ固定する。初回利用時は引き続き`param(...)`を確認する。

### 複数のSKILL全文を1つのfunctions.exec出力へ集約して切れる

- 原因: 個々の`safe-read`/`safe-read-batch`が出力上限内でも、複数ファイルの全文を同じ`functions.exec`で連続出力すると合計応答がcontext上限を超える。
- 対応: 選択した`SKILL.md`は1ファイルずつ、必要なら80行以下の範囲ごとに別の`functions.exec`で直列に読む。切れた一括コマンドは再発行しない。
- 禁止: 複数スキルの全文を1つのcellへ集約すること、切れた出力を全文読了とみなすこと。

### safe-readへ未定義のPinpoint引数を渡す

- 原因: 他Wrapperの限定出力用引数を`safe-read.ps1`にも存在すると推測した。
- 対応: `safe-read.ps1`は`-Pattern`、`-Context`、`-MaxMatches`、必要に応じて`-LiteralPattern`と`-PrintOutput`を使う。初回または引数に迷った場合は先頭の`param(...)`を読む。
- 禁止: `-Pinpoint`など未確認の引数を転用すること。

### safe-readのStartLine-EndLineが80行を1行だけ超える

- 原因: 行範囲が両端を含むことを見落とし、`40-120`を80行ではなく81行として指定した。
- 対応: `guard_code: 39`が返す`suggested_end_line`へ限定するか、元の範囲を変更せず`safe-read-batch.ps1 -Ranges '<start>-<end>'`へ1回だけ渡して自動分割する。
- 禁止: Guard後に終了行を119、118と手打ちで調整すること、`-AllowHighOutput`で単純な境界ミスを回避すること。

### safe-readが出力なし・TokenReports記録なしでexit code 1になる

- 原因境界: 対象Pathが実在し、同じ範囲を`safe-read-batch.ps1`経由で読むと成功し、失敗時のTokenReports記録もない場合、対象ファイルや`safe-read`処理内ではなくRTKから直接Wrapperを起動するtransport境界で停止している。
- 対応: 対象Pathを`Test-Path`で非エラー確認し、同じ直接コマンドを再送せず、`safe-read-batch.ps1 -Path <file> -Ranges '<start>-<end>'`へ1範囲だけ渡す。batch経由のcaptureと終了コード0を限定自己テストとして保存する。
- 禁止: 空出力を対象ファイル欠損や内容エラーと推測すること、同じ`safe-read`直接呼び出しを反復すること、未記録の失敗を成功扱いすること。

### 複数ファイルapply_patchのhunk境界が壊れる

- 原因: 更新hunkの末尾へ不要な`@@`を残し、次の`*** Update File`がhunk本文として解釈された。
- 対応: `apply_patch verification failed: invalid hunk`では対象未変更を確認し、ファイル単位のpatchへ分割して各hunkを`*** Begin Patch` / `*** End Patch`内で完結させる。
- 禁止: 同じ長いpatch文字列へ場当たり的に記号を追加して再送すること、Shell書き込みへ切り替えること。

### sandbox内のGet-Command pythonが空になる

- 原因: managed sandboxのPATHと承認済み外側PowerShellのPATHが異なり、sandbox内の`Get-Command python`だけではインストール有無を確定できない。
- 対応: 画像処理などでPythonが必須なら、同一の`Get-Command python`を権限境界だけ変えて1回確認し、返された実在する絶対パスを以後の固定入口にする。Python未導入と判定する前に、実行した権限境界を証拠へ残す。
- 禁止: `python`、`py`、`python3`、推測したインストール先を順に試すこと、PATH差を実装不良や依存欠落として扱うこと。

### safe-searchへsafe-read専用のLiteralPatternを渡す

- 原因: コード記号を固定文字列検索する際、`safe-read.ps1`の`-LiteralPattern`を`safe-search.ps1`にも存在すると推測して転用した。
- 対応: `safe-search.ps1`の`-Pattern`は正規表現専用として、必要な記号を正規表現で明示的にエスケープする。単一既知ファイル内の固定文字列読取は`safe-read.ps1 -LiteralPattern`を使う。
- 禁止: `safe-search`へ`-LiteralPattern`、`-FixedString`、`-SimpleMatch`等の未定義switchを順番に試すこと。

### safe-unity-searchへPathを転用する

- 原因: 通常テキスト検索`safe-search.ps1`の`-Path`を、固定Scene Reporter入口の`safe-unity-search.ps1`にも存在すると推測した。
- 対応: 正式契約を`safe-unity-search.ps1 -Query <対象名> [-PrintOutput]`としてhelp、詳細ルール、自己テストへ固定する。検索範囲はReporterが管理する。この入口はUnity接続とEditor Menuを実行するため、Unity/Menu禁止タスクでは通常ファイル検索だけを使う。
- 禁止: `-Path`、`-Root`、Scene名を追加引数として渡すこと。対象Sceneを絞る必要がある場合は既存Reporter/Validatorの設計を確認し、引数を推測追加しない。

### safe-unity-searchのQuery先頭・末尾空白で完全一致待機が失敗する

- 原因: Wrapperは入力Queryをそのまま一時ファイルと期待ヘッダーへ使う一方、Unity Reporterは読取後にtrimしたQueryをレポートへ記録する。`" Icon"`ではReporter自体は`Query: Icon`を生成して成功しても、Wrapperが`Query:  Icon`を10秒待って`guard_code: 30`になる。
- 対応: `Invoke-AreaUnitySearch.ps1`は空文字および先頭・末尾空白を`guard_code: 40`でUnity接続前に拒否する。部分一致用の空白を検索語へ足さず、trim済みの実際の語を渡す。
- 禁止: 完全一致待機失敗後に空白数を変えて再試行すること、生成済みレポートを無視して別Reporterへ切り替えること。

### scoped-diff-checkのカンマ区切りPathが1つの長いパスになる

- 原因: `rtk`からWindows PowerShellの`-File`境界へ渡した`-Path a,b`はPowerShell式として再評価されず、カンマを含む単一引数としてbindされる。
- 対応: `scoped-diff-check.ps1 -Path "a;b"`を正式契約とし、Wrapper内でセミコロン分割してから存在確認・git転送する。カンマは入口で説明付き拒否し、自己テストで固定する。
- 禁止: カンマ、空白、複数の`-Path`指定を推測で反復すること、または広域`git diff --check`へ切り替えること。

### scoped-diff-checkへ存在しないMode引数を推測で渡す

- 原因: 差分概要取得と差分構文検査を混同し、`scoped-diff-check.ps1`のparam定義を確認せず`-Mode Summary`を推測で渡した。
- 対応: 正式契約を`-Path "a;b" [-PrintOutput]`へ固定し、Wrapper先頭のhelpと自己テストで明示する。差分概要が必要な時はscoped-diff-checkへ機能を推測追加せず、専用safe diff入口を使う。
- 禁止: 未定義の`-Mode`、`-SummaryOnly`、`-Stat`を手打ちで順番に試すこと。

### RTK越しのPowerShell -Commandで画像コピー配列の引用符が消える

- 原因: 外側Shell、RTK、内側PowerShellの3境界を通るインライン`-Command`へ、二重引用符を含む配列リテラルを埋め込んだ。内側PowerShellが要素を裸トークンとして受け取り`MissingArgument`になった。
- 対応: 対応表をUTF-8 manifestへ分離し、`copy-generated-image-batch.ps1`へSourceDirectory、ManifestPath、DestinationDirectoryを単純引数として渡す。`-ValidateOnly`で全18入力と保存先境界を確認後、同じWrapperでコピーする。
- 禁止: quoteの種類やescapeだけを変えてインライン`-Command`を再試行すること、コピー元と保存先の対応を手打ち反復すること。

### サブエージェントが依存待ちでrunningのまま無応答に見える

- 原因: 作業担当がLeadのWrapper整備完了を待つため、自身で`wait_agent`を反復した。実行中コマンドがないのにrunning状態が続き、処理遅延やhangと区別できなくなった。
- 対応: 依存待ちは再開条件と現状をfinalで返して一度終了する。Leadは依存解消後に`followup_task`で新しい作業ターンを明示起動する。
- 禁止: 作業担当がメッセージ受信待ちだけのために`wait_agent`を呼ぶこと、実行中コマンドの有無を報告せずrunningを維持すること。

### functions.execのcellをcollaboration.wait_agentで待って無応答になる

- 原因: `functions.exec`が返した`Script running with cell ID`をcommand継続として扱わず、エージェントmailbox用の`collaboration.wait_agent`を呼んだ。元command cellは未回収のまま残り、依存ローダー自体が遅いように見えた。
- 対応: 元cellへ`functions.wait({cell_id, yield_time_ms<=10000})`を行い、結果または継続状態を回収する。`collaboration.wait_agent`はサブエージェントのmessage/final待ちだけに使う。
- 禁止: cell未回収のまま同じコマンドを再発行すること、mailbox timeoutをcommand timeoutとして扱うこと、cell IDを失った状態で別方式へ切り替えること。

### functions.exec内のexec_command継続sessionを出力だけで破棄する

- 原因: 30秒を超えるRunnerへ`exec_command`を使い、戻り値の`output`だけを表示して`session_id`を保持しなかった。内側commandは継続中でも外側cellが完了し、後続出力を回収できなくなった。
- 対応: 長時間になり得るcommandは、同一`functions.exec`内で`exec_command`の戻り値を保持し、`session_id`がある間は`write_stdin`で10秒以下ごとにpollして完了まで回収する。外側の`functions.wait`はfunctions cell用、内側の`write_stdin`はexec session用として二層を分離する。
- 禁止: `session_id`を表示・保存せず外側scriptを終了すること、孤立sessionの完了未確認で同じUnity RunnerやMenuを再発行すること。

### collaboration.wait_agentへ最小値未満のtimeoutを渡す

- 原因: 即時pollのつもりで`timeout_ms=1000`を指定したが、Tool契約の許容範囲は`10000`〜`3600000`である。
- 対応: 即時状態確認は`list_agents`、mailbox更新待ちは`timeout_ms>=10000`の`wait_agent`を使う。Tool schemaの範囲を引数Validatorとして扱う。
- 禁止: 最小値未満の値を刻んで再試行すること、status確認とmailbox待ちを同じ入口として扱うこと。

### append-vault-noteへ記憶上の引数名を渡す

- 原因: Wrapperの現行`param(...)`を再確認せず、存在しない`-VaultPath/-NotePath/-AppendFile`を指定した。正式契約は`-VaultRoot/-RelativePath/-ContentPath`である。
- 対応: 初回利用時に`param(...)`を読み、`command-tools-self-test.ps1`で正式3引数と旧誤引数の非存在を固定する。
- 禁止: 類似名を順番に推測すること、Obsidianファイルへ別コマンドで直接追記して回避すること。

### apply_patchが想定見出し不在で適用前検証に失敗する

- 原因: 追記先ファイルの現行見出しを読まず、記憶上の見出し文字列をcontextとして指定した。
- 対応: 既存文書へ追記する前に`safe-read.ps1 -Pattern`または行範囲で実在する直前contextを確認し、その最小contextだけをpatch anchorにする。適用前検証失敗はファイル変更なしとして証拠化する。
- 禁止: 見出し名や空行数だけを推測変更してpatchを反復すること。

### safe-readへ存在しないファイルを渡すと内部エラーが終了コード0に見える

- 原因: 推測したファイルパスを事前確認せず、旧`safe-read.ps1`も`Get-Content`実行前のPath存在検証を持っていなかった。PowerShellの非終端エラーが後続処理で正規終了に見える場合があった。
- 対応: `safe-read.ps1`は既存ファイル以外を`guard_code: 33`で入口拒否する。クラス名しか分からない場合は、先に`safe-search.ps1 -FilesOnly`または`focused-search.ps1`で実在パスを確定する。
- Migration、Validator、Importerなど既知の管理スクリプトにRoot定数や対象Path定数がある場合は、その既存ファイルを先に読み、定数から実在パスを組み立てる。`Projectiles/`や`Generated/`など一般的な分類名を記憶や推測で補ってはならない。
- `safe-search.ps1 -Path`も推測したディレクトリを渡さない。検索起点が未確定なら、既知の直近親ディレクトリを`-Path`にして`-FilesOnly`で実在ファイルを確定してから、狭い検索へ進む。Path Guardの入口拒否後に類似ディレクトリ名を手打ちで試さない。
- サブエージェントへ検索調査を委譲する場合、Leadが既知の現行ファイルと既存親ディレクトリを依頼文へ列挙する。Agent側で`Scripts/Game/Player`等の分類名を補完させない。AgentがPath Guardで停止した場合はLeadが原因・ルール・Knowledge・自己テストを完了してから、正しいパスを明記した`followup_task`で再開する。
- 禁止: Skillや記憶にある旧パス、推測したクラス名をそのまま`safe-read`へ渡し、内部の`Get-Content`エラーを読み取り結果として扱うこと。

### safe-read -PrintOutputの合算出力が会話コンテキスト上限を超える

- 原因: `-AllowMany`はファイル読取件数の意図確認であり、会話へ流す標準出力量を制限していなかった。大きい行範囲や`MaxMatches × Context`を別の読取と並列実行すると、各コマンドは成功しても`functions.exec`の合算結果が切り捨てられる。
- 対応: `safe-read.ps1`は`-PrintOutput`の推定出力が80行を超える場合を`guard_code: 39`で入口拒否する。Pattern検索は実行前に`MaxMatches * (Context * 2 + 4) <= 80`を確認し、範囲・一致数・Contextを狭めて直列に読む。`functions.exec` 1回につき `-PrintOutput`を伴う読み取りは1件だけとし、複数コマンドの出力を合算しない。単一呼び出しの出力予算を明示的に確保した場合だけ`-AllowHighOutput`を使い、その呼び出しと他の出力を並列化しない。
- 禁止: 切り捨て後に同じ大範囲読取を再発行すること、単体が80行以内でも複数の`-PrintOutput`を同じ`functions.exec`へまとめること、`-AllowMany`だけで大きい`-PrintOutput`を並列実行すること、capture済みの結果を未回収のまま別検索方式へ切り替えること。

### PowerShell `-File`へ`.cs`や`.md`を直接渡して読み取ろうとする

- 原因: 並列コマンドの一部で`safe-read.ps1 -Path`を付け忘れ、対象C#パスをPowerShellの実行スクリプトとしてbindした。
- 対応: ソースやMarkdownの読み取りは必ず`safe-read.ps1 -Path <既存ファイル>`を入口にする。PowerShell `-File`の直後は`.ps1` Wrapperだけを置き、C#、Markdown、Sceneパスを置かない。
- 禁止: 拡張子エラー後に`Get-Content`や別Shellへ切り替えること。正規の`safe-read`入口で同じ読み取りを1回だけ行う。

### 並列safe-readの1本だけPath末尾の引用符が余る

- 原因: 複数のShell文字列を同時に手組みし、1本の`-Path '...ps1'`直後へ余分な`'`を付けた。PowerShell ParserErrorとなり、`safe-read`のPath Guardへ到達しなかった。
- 対応: Wrapperの`param(...)`確認は1本ずつ既知の`safe-read.ps1 -Path '<path>' -StartLine ...`テンプレートで行う。並列化は契約確認完了後の独立読み取りだけに限定する。
- 同一ファイルの複数行範囲を読む時は、複数のShell文字列を組み立てず、`safe-read-batch.ps1 -Path <既存ファイル> -Ranges "start-end;start-end"`を使う。範囲内はハイフン、範囲間はセミコロンに固定し、コロンやカンマを使わない。Wrapperは全範囲を検証してから同一の`safe-read.ps1`入口へ直列転送する。
- 80行を超える意図的な各範囲は`safe-read-batch.ps1 -AllowMany`で明示し、同じswitchを各`safe-read.ps1`呼び出しへ転送する。batch Wrapperで未定義switchを推測せず、正式契約と自己テストを先に確認する。
- 禁止: ParserError後に引用符候補を手当たり次第変更すること、同一呼び出し内で未確認Wrapperを複数組み立てること。
- `focused-search.ps1` の拡張子指定は `-Extension cs` または明示Aliasの `-Include '*.cs'` を使う。PowerShellの部分一致へ依存した未定義パラメータ名を渡さず、Aliasとワイルドカード正規化はWrapper自己テストで固定する。
- 要約や報告にファイル名だけがあり正確な相対パスがない場合、その名前からパスを補完して `safe-read` / `safe-read-batch` を実行しない。先に対象ディレクトリを限定した `safe-search.ps1 -FilesOnly` で実在パスを確定する。

### 検索一致なしをrgエラーとして扱う

- 原因: `rg` は一致なしで終了コード1を返す。出力を後段で正常処理してもPowerShellプロセスに `$LASTEXITCODE=1` が残ると、Wrapperが実エラーと誤認する。
- 対応: 終了コード2以上だけを実エラーとして伝播し、一致なしと正常検索は出力処理後に明示 `exit 0` へ正規化する。非C#検索ではExtension指定を確認し、Unity YAMLはReporterを使う。

### `safe-search -Pattern` にファイルglobを渡してrgが終了コード2になる

- 原因: `-Pattern` は正規表現契約だが、ファイルglobのつもりで `*Evolution*` のような先頭アスタリスク付き文字列を渡した。正規表現では先頭の `*` に反復対象がないため構文エラーになる。
- 対応: 部分一致は `Evolution`、明示的な前後任意文字は `.*Evolution.*` を使う。`safe-search.ps1` は典型的な `*term*` 誤用を `rg` 実行前に拒否する。
- 禁止: 終了コード2を一致なしと解釈すること、または別検索方式へ切り替えて調査を続けること。

### `safe-read -Pattern`へ`kills++`等のコードリテラルを渡す

- 原因: `-Pattern`は正規表現契約であり、コード中の`+`や`?`を量指定子として解釈して意図しない広範囲一致・高出力を発生させる。
- 対応: コード文字列の完全一致は`safe-read.ps1 -Pattern "kills++" -LiteralPattern`を使う。Wrapperは典型的な未エスケープ`++/**/??`を`guard_code: 36`で拒否する。
- 禁止: 高出力Guard後にContext/MaxMatchesだけを変えて同じregexを再試行すること、または別読み取り方式へ切り替えること。

### `safe-read -Pattern`へ未閉じ`(`を含むコード文字列を渡す

- 原因: Wrapperがregex構文を入口検証せず、生成した行ループ内の`-match`が全行で構文エラーを繰り返した。
- 対応: `safe-read`は`[regex]::new($Pattern)`を事前実行し、無効regexを`guard_code: 37`で拒否する。コード文字列は`-Pattern "EnsureWeaponLevels(" -LiteralPattern`を使う。
- 複数語検索でも、`WeaponController|Configure(`のようにregexの`|`と未エスケープのコード句読点を混在させない。コード識別子は1語ずつ独立した`-LiteralPattern`呼び出しに分け、契約確認済みの読み取りだけを並列化する。
- 禁止: 無効regexのままContext/MaxMatchesを下げること、括弧だけを推測で削ること、別読み取り方式へ切り替えること。

### `safe-search -Path` の2つ目がExtensionへ位置束縛される

- 原因: `powershell -File safe-search.ps1 -Path path1 path2` と空白区切りで複数Pathを渡すと、2つ目が次の配列パラメータ`-Extension`へ位置束縛され、`-g '*.path2'`へ変形する場合がある。終了コード0でも本来の2パス検索ではない。
- 対応: `powershell -File`では1回につき1つの`-Path`だけを渡す。独立検索は個別Wrapper呼び出しに分ける。`safe-search.ps1`は区切り文字を含む`-Extension`を入口拒否する。
- 禁止: 空結果を一致なしとして受け入れること、または変形した`rg`コマンドのまま実装判断へ使うこと。

### safe-search HitSummaryが一致しても空captureになる

- 原因: `Group-Object`から得たPowerShellオブジェクトをそのまま出力し、直後に`exit 0`したため、遅延した表示整形がcaptureへ書き出されなかった。また絶対パスを最初のコロンで分割するとWindowsドライブ文字`C:`で切れ、全結果が`C`へ集約された。
- 対応: CountとNameを明示文字列へ変換してから終了する。ファイルパスは先頭コロンではなく`:<行番号>:`の末尾構造で抽出する。自己テストは絶対パス配下の既知ファイル名がHitSummary出力へ含まれることを確認する。
- 禁止: 空captureを一致なしとして受け入れること、または別検索方式の結果だけで実装判断を続けること。

### safe-searchのPatternに二重引用符を含めるとrg exit 2になる

- 原因: Windows PowerShell 5.1からnative `rg`へ引数を渡す境界で、正規表現内の二重引用符が保持されず、`safe-search`の事前`.NET Regex`検証は通っても`rg`が終了コード2かつ空captureで終了する。
- 対応: `safe-search.ps1`はPattern内の二重引用符を`guard_code: 45`で入口拒否する。引用符そのものを検索条件へ含めず、`MenuItem\(`など周辺の十分に限定されたPatternへ分ける。
- 禁止: 二重引用符をバッククォートやバックスラッシュで手直しして同じnative境界へ再投入すること、空captureを一致なしとして扱うこと。

### safe-unity-searchが別クエリの最新レポートを返す

- 原因: 複数プロセスが固定の検索語ファイルと最新レポート選択を共有し、Write→Menu→report選択→deleteが競合する。最新時刻だけでは自分の結果と証明できない。
- 対応: 名前付きMutexで検索全体を直列化し、実行前のreport署名を保存する。実行後に新規または更新されたreportだけを候補とし、先頭の `Query:` が要求語と完全一致することを必須にする。不一致は `guard_code: 30` で停止する。
- Play Mode中はMenu受付が成功してもReporterファイルが生成されない場合がある。検索語を書き込む前に `PlayMode.Status` を確認し、Play中は `guard_code: 32` で拒否する。Edit ModeでもReporter書込みはMenu応答より遅れる可能性があるため、検索語ファイルを保持したまま最大10秒だけ一致reportを待つ。

### safe-readが内部のアクセス拒否を終了コード0として返す

- 原因: `Safe-Command.ps1`へ渡す複合PowerShell式で`Get-Content`のアクセス拒否が非終端エラーとなり、後続の空ループが正常終了して終了コード0を上書きした。
- 対応: `safe-read.ps1`が生成するすべての`Get-Content`式の先頭で`$ErrorActionPreference = 'Stop'`を設定し、読み取り不能を即時失敗として伝播する。
- 禁止: capture内の`PermissionDenied`を無視して終了コードだけで読み取り成功と判定すること。

### Menu.Executeが`executed: true`でもMenu本体が例外終了する

- 原因: UniCLIのMenu応答は`EditorApplication.ExecuteMenuItem`の受付結果であり、Menuメソッド内部の例外や後続Scene保存の完了を表さない。途中例外でもコマンド終了コード0・`executed: true`になり得る。
- 対応: Migration/Reporterは対象Sceneを変更する前に全検索対象・複製元・保存先をPreflightし、成功行または完了markerを処理末尾でだけ出す。呼び出し側は専用Validator、完了marker、Console Errorのいずれかで副作用完了を確認する。
- 未読込Sceneを`EditorSceneManager.OpenScene`で開くValidatorは、marker削除やScene操作より前に`EditorApplication.isPlayingOrWillChangePlaymode`を確認し、Play Mode中は入口で明示的に拒否する。ユーザーのPlay Modeを検証都合で停止したり、同じMenuを再実行したりしない。
- Editor Runnerから`Library/AreaSafeUnity`へmarkerを書く場合は、Unityプロセスの作業ディレクトリを仮定しない。`Application.dataPath`の親からプロジェクト絶対パスを組み立て、呼び出し側が確認するパスと完全一致させる。
- 禁止: `executed: true`だけでMigration成功と報告すること、途中まで変更してから不足参照を発見すること、例外後に同じMenuを再実行すること。

### Prefab移行前に旧MonoBehaviourを削除してMissing Script化する

- 症状: `SaveAsPrefabAsset`がMissing Scriptを理由に保存を拒否する一方、Menuコマンドは終了コード0を返し、後続Prefabだけ未移行になる。
- 原因: 旧Script/`.meta`をPrefab移行より先に削除すると型名によるComponent検索・削除ができない。単一GameObjectだけの欠損除去と、`SaveAsPrefabAsset`戻り値未確認では失敗を捕捉できない。
- 対応: 型が解決可能な状態でPrefab移行→Validator→旧Script削除の順にする。既に欠損している場合はPrefab全TransformでMissing Scriptを除去し、残数0をassertする。保存結果がnullなら即時例外にし、成功markerを作らない。
- 禁止: Prefab参照を調べず旧Scriptを先に削除すること、rootまたは既知childだけを欠損検査すること、Console Errorがある状態でMenu終了コード0を成功扱いすること。

### 入れ子Scroll Viewの同名`Viewport/Content`を誤認する

- 症状: 外側Scroll ViewをEditor上で全体表示する修正なのに、内側リストの`Content`だけが拡張され、外側Maskによるクリップが残る。
- 原因: ボタンや項目から親をたどって最初に見つかった`Content`/`Viewport`を画面全体のScroll Viewと仮定した。Scene内に同名階層が入れ子で存在すると、内側リストへ誤って結び付く。
- 対応: Migration/Validatorは変更対象の一意なRoot名（例: `Tool Scroll View`）をScene全体から先に1件だけ解決し、その直下`Viewport/Content`を参照する。対象Root、外側Content、内側リストContentのfileID・高さ・親子関係をPreflightし、意図したRootの高さとController参照を専用Validatorで確認する。
- 禁止: 子ボタン起点の親探索だけで画面全体のScroll Rootを確定すること、`Content`という名前だけで対象を選ぶこと、Menu受付成功だけで移行完了と報告すること。

### Unity named pipeへのアクセスが拒否される

- 症状: UniCLIが内部で5回接続を試み、`Last error: ... Access to the path is denied.` と終了する。timeoutやUnity無応答とは区別する。
- 原因: サンドボックス内の子PowerShellからUnity Editorのnamed pipeへアクセスできない実行境界。
- 対応: `safe-unity` はAction種別を問わず `guard_code: 26` とcapture pathを表示する。同じsafe-unityコマンドを外側の権限昇格付きで1回だけ再実行し、別コマンドや手動Editor操作へ切り替えない。

### 安全Guardの追加で既存Wrapperが拒否される

- 症状: 正規のWrapperを呼んだ時点で、内部実装が新しいGuardに拒否される。例: `safe-unity-search` 内部の引用符付きEvalが `guard_code: 25` になる。
- 原因: Guard単体の自己テストは通っていても、Guardを利用する既存Wrapperとの契約テストが不足している。
- 対応: 別検索へ逃げず、既存Wrapperから危険経路を除去する。検索語など動的文字列は一時入力ファイル→限定Reporter Menuで渡し、`command-tools-self-test.ps1`へ「危険経路が残っていない」静的検査を追加する。

### Play Mode中のEval timeout後にserverが永久busyになる

- 症状: Evalクライアントはtimeoutして終了コード124になるが、後続コマンドは即座に `Server is busy executing 'Eval'` を返す。
- 原因: Evalの`AssemblyBuilder`がPlay中にforced synchronous recompileとDomain Reloadを起こすと、UniCLIの`ProcessCommandAsync`継続と`finally`が失われ、`_currentCommand`が未完了のまま残る。クライアントプロセス停止だけではserver側busyを解除できない。
- 対応: Play中はEvalを実行しない。`safe-unity`の事前Status確認で `guard_code: 27` として拒否し、UI操作は事前コンパイル済みフックまたは通常入力へ寄せる。`command-tools-self-test.ps1` は共通Guardを迂回する直接Evalが他Wrapperへ残っていないことも検査する。
- 復旧: 発生済みbusyはUniCLI経由で解除できない。`unity-process-report.ps1` でUnity本体のPID・タイトル・応答状態を特定し、必要な証拠画面だけを `unity-window-control.ps1` で取得する。最後に同Wrapperの `StopPlay` を1回だけ実行し、以後Unityへ追加照会しない。
- 禁止: busy中にStatusやEvalを反復すること、PID・タイトル未確認のGUI入力、検証用Wrapperから `unicli exec Eval` を直接呼ぶこと。

### 複数のUnity Menu検証を同時実行すると先発の応答が失われる

- 症状: 後発は `Server is busy executing 'unknown'` で失敗し、先発はEditorログ上でMenu処理が完了していてもcaptureが空のままtimeoutする。
- 原因: 複数の `safe-unity -Action Menu` を並列起動し、UniCLIの単一コマンド／pipe応答境界へ同時接続した。
- 対応: `safe-unity` の名前付きMutexでUnityコマンド全体を直列化し、競合時は `guard_code: 34` でUniCLIへ接続する前に拒否する。複数Scene検証は `OpenSceneMode.Single` の往復を避け、必要SceneだけAdditiveで一時読込して閉じる。
- 禁止: Unity Menu、Compile、Console確認を `Promise.all` や未完了の非同期処理で並列実行すること。先発の終了コードを確認する前に次のUnityコマンドを開始しない。

### C#一括更新をBatchRefreshへ流すとtimeout後にserverがunknown busyになる

- 症状: `invoke-unity-editor-runner.ps1 -Phase RegisterAndRun -BatchRefresh` の `AssetDatabase.Import` が終了コード124でtimeoutする。Editor.logではasset import自体が完了していてもRuntime/Editor Assemblyはstaleのままで、後続の明示AssetImportは `Server is busy executing 'unknown'` を返す。
- 原因: `-BatchRefresh` はScene・Prefab・asset等のserialized asset一括更新用であり、外部変更C#の明示Importを代替しない。大量変更を含むRefreshがscript compilation／Domain Reload境界へ入るとpipe応答が失われ、UniCLI server側のcurrent commandまたはqueueが解放されない場合がある。
- 対応: Assemblyより新しい全C#を列挙し、永続Validatorを `RegisterAndRun` のScriptPath、残りを `-DependencyScriptPaths` として明示Importする。`RefreshAfterRemoval` は事前にAssembly currentを確認し、追加C#がある場合はUnity状態を変える前に拒否する。
- 発生済みbusy: `unity-process-report.ps1`、Safe-Command capture、Editor.log、Assembly freshnessを保存する。`busy executing 'unknown'` を確認した後はUnityコマンドを重ねず、Unity EditorまたはUniCLI serverの再起動でserver instanceを作り直してから、未実行だった明示Importを1回だけ再開する。
- 禁止: busy中にAssetImport、AssetRefresh、Compile、Menu、Statusを順番に試すこと、BatchRefreshのtimeout値だけを延ばしてC#初回Importを再試行すること。

### Unity保存直後の空scalarをgit diff --checkが末尾空白として報告する

- 症状: Unityが追加したMonoBehaviourの`m_Name: `、`m_EditorClassIdentifier: `だけを`git diff --check`がtrailing whitespaceとして終了コード1にする。
- 原因: Unity 2022.3のYAML serializerが空scalarをコロン＋空白で保存するため。Missing Scriptや空参照を意味しない。
- 対応: 対象を限定読取し、Unity生成Componentの標準ヘッダー2行だけであることを確認する。Sceneを手編集せず既知例外とし、Scene以外のtask対象へ`git diff --check`を限定して本来のwhitespace errorが0件であることを確認する。
- 禁止: diff checkを通すためだけにScene YAMLの空scalar表現を書き換えること、Component参照の欠損と推測してSceneを再生成すること。

### AssetRefresh直後の明示Compileが空応答になる

- 症状: AssetRefreshは成功するが、直後の`safe-unity -Action Compile`が標準出力もSafe-Command JSONLも残さず終了する。Editor.logにはScriptCompilation要求と変更C# importだけが残る。
- 原因: AssetRefreshが開始した非同期script compilation／Domain Reload境界へ、明示Compileを重ねてUniCLI応答経路を失った。
- 対応: AssetRefresh／AssetImport成功時刻を`Library/AreaSafeUnity/last-asset-refresh.utc`へ記録し、30秒以内のCompile検証を`guard_code: 35`で拒否する。`safe-unity Compile`はUniCLI Compileを呼ばず、`verify-unity-script-compilation.ps1`で最新Runtime/Editor C#と`Assembly-CSharp*.dll`の更新時刻を比較する。
- 禁止: C#変更を含むAssetRefreshの完了応答を、script compilation／Domain Reload完了とみなして直ちにCompileを開始すること。`unicli exec Compile`で強制Domain Reloadと応答待ちを発生させない。
- Editor Runnerも例外ではない。`invoke-unity-editor-runner.ps1`は最後のAssetImport/AssetRefresh markerから31秒経過するまで同じRunner内で待ち、`guard_code: 35`を手打ち再試行で回避しない。

### safe-unity内部失敗が呼び出し元で終了コード0になる

- 原因: `Invoke-AreaSafeUnity.ps1`がSafe-Commandの`exit_code`を`$global:LASTEXITCODE`へ代入しただけで、PowerShellプロセス自体を`exit`していなかった。内部検証が1でも外側の`exec_command`は0になり、後続検証を誤って継続した。
- 対応: Mutex解放の`finally`完了後に`exit $safeExitCode`を必須とし、`command-tools-self-test.ps1`で終了コード伝播行を固定する。呼び出し側は標準出力の内部`exit_code`ではなく、プロセス終了コードも一致することを完了条件にする。
- `safe-unity -Action Compile`は再Compile要求ではなく最新Assemblyの検証入口である。直前にC#を編集した場合はstale失敗を再試行せず、既存Editor RunnerのImport→Compile→Menu検証を一体で1回実行する。
- 禁止: 内部失敗行があるのに外側0だけでConsole確認や次のUnity操作へ進むこと、stale判定後に同じCompile検証を手打ち再実行すること。

### Editor Runnerの長い戻り値でcommand cellの結果を回収できない

- 原因: `invoke-unity-editor-runner.ps1`が成功した各Safe Unityサブコマンドのpayloadをすべて会話へ返し、長いスレッドの残存contextを超えた。閉じたcellを`functions.exec`内の存在しない`tools.wait`で回収しようとすると、元処理とは別の入口エラーも重なる。
- 対応: Runnerは`-Concise`を付け、成功時は段階名だけ、失敗時は末尾40行とcapture情報だけを返す。`Script running with cell ID`は外側の`functions.wait(cell_id)`でのみ回収し、cell消失後はTokenReportsとSafe-Command captureから終了コード・timeout・実行段階を確定する。
- 禁止: 長いRunner結果を`text(JSON.stringify(result))`で無制限に展開すること、`functions.exec`内の`tools.wait`を呼ぶこと、cell結果が不明なまま同じRunnerを再発行すること。

### Editor Runnerのexec session IDを表示せず継続ハンドルを失う

- 原因: 31秒cooldownを含むRunnerへ`exec_command(yield_time_ms=30000)`を使い、戻り値から`exit_code`と`output`だけを表示した。30秒時点の正常な中間応答は`exit_code`未定義・`session_id`ありだが、session_idを捨てたため`write_stdin`で継続取得できなくなった。
- 対応: `tools.exec_command`の戻り値は`session_id`も必ず表示または保存する。失った場合は同じRunnerを再発行せず、`token-report-summary -Json`が返す`capture_path`・`timed_out`・`exit_code`から停止段階を確定する。summary Reporterはこれら3項目を保持する。
- 禁止: `exit_code=undefined`を完了扱いすること、session_id不明のまま同じRunnerを再発行すること、推測で次のMenu検証へ進むこと。

### 未コミット差分が多い作業ツリーで生のgit statusが高出力になる

- 原因: 既存差分が多いことを把握しているのに`git status --short`を直接会話へ展開し、今回対象外の変更一覧で出力上限を消費した。
- 対応: 状態確認は`safe-status.ps1`を使い、既定ではcaptureへ保存して会話へ展開しない。今回所有するファイルの品質確認は`scoped-diff-check.ps1 -Path "path1;path2"`へ分離する。
- 禁止: dirty worktree全体の生statusを直接表示すること、truncated後に別形式の生statusを再発行すること。

### 同一PowerShell読み取りWrapperの並列実行で-File境界が崩れる

- 症状: `safe-read-batch.ps1`をRTK経由で2本並列起動した際、片方だけ読み取り対象スクリプトが直接実行され、未定義`-Ranges`で終了コード1になった。もう片方は正常だった。
- 原因境界: 2本のコマンド文字列は正規形式で、差は並列実行のみ。失敗側は`safe-read-batch`の先頭出力へ到達しておらず、RTK→PowerShell `-File`引数境界でWrapperと対象Pathの対応が崩れた。
- 対応: 同一PowerShell読み取りWrapperは直列実行する。複数範囲は1回の`safe-read-batch -Ranges`へまとめ、複数ファイルは順番に読む。
- 禁止: 同一Wrapperを`Promise.all`で並列起動すること、失敗後に同じ並列形式を再発行すること。

### 引用した実行ファイルパスをPowerShellで直接並べるとParserErrorになる

- 症状: `"C:\path\python.exe" "C:\path\script.py" --flag`を実行すると、2つ目の引用文字列や`--flag`が`Unexpected token`としてPowerShell解析段階で拒否される。
- 原因境界: PowerShellでは引用文字列単体は実行式にならず、パスに空白がなくても引用した実行ファイルを呼ぶにはcall operator `&`が必要である。対象実行ファイルやスクリプトには到達していない。
- 対応: `& "C:\path\python.exe" "C:\path\script.py" --flag`の形式に固定する。再開前に同じ実行ファイルへ`& "<exe>" --version`を実行し、call operator経路だけを限定自己テストする。
- 禁止: ParserError後に引用符を外す、別Shellへ切り替える、`-Command`文字列へ複雑なエスケープを埋め込むこと。

### 入れ子の`powershell.exe -Command`で変数が外側Shellに先行展開される

- 症状: `powershell.exe -Command "$value = ..."`の`$value`が子PowerShellへ届く前に空文字へ変わり、コマンド先頭が`=...`となって終了コード1になる。
- 原因境界: Codexの既定ShellもPowerShellであるため、二重引用符内の`$variable`は外側Shellが先に展開する。子PowerShellや対象ファイルには意図した式が届いていない。
- 対応: 変数、複数式、検証ロジックを含む処理は`.ps1` Reporter/Wrapperへ分離し、`powershell.exe -File <wrapper.ps1> -NamedParameter <value>`で呼ぶ。限定確認はWrapperのJSON出力で行う。
- 禁止: バッククォートや引用符の追加で同じ`-Command`文字列を手直し再試行すること。

### `Get-Content`の一致行をそのままJSON化するとProvider情報が展開される

- 症状: 数行のログ抜粋だけを含むReporterが、`PSPath`、`PSDrive`、Provider型情報までJSON化して数千行へ膨張し、会話出力が切り捨てられる。
- 原因境界: Windows PowerShellのパイプライン上の文字列には拡張プロパティが付く場合があり、`ConvertTo-Json -Depth`がそのメタデータまで再帰展開する。
- 対応: ログ抜粋は`Select-Object -First <上限>`の後に`ForEach-Object { $_.ToString() }`で純粋な文字列へ変換してからReportへ格納する。
- 禁止: 切り捨て後に同じReporterを再発行すること、`ConvertTo-Json -Depth`を下げるだけで偶然抑えること。

### Unity Editorコードで`CompressionLevel`がCS0104になる

- 症状: `System.IO.Compression`と`UnityEngine`を同時にusingしたEditorコードで、`CompressionLevel.Optimal`がCS0104（ambiguous reference）になる。
- 原因境界: Unity 2022には`UnityEngine.CompressionLevel`も存在し、短い型名だけではZIP用の`System.IO.Compression.CompressionLevel`を選べない。
- 対応: ZIP生成の引数は`System.IO.Compression.CompressionLevel.Optimal`の完全修飾名で指定する。Compile鮮度失敗後はEditor.logのCS0104を確認してから限定修正する。
- 禁止: usingの順序変更や別ZIPライブラリへの切り替えで曖昧さを回避しようとすること。

### MenuExistsが`+`を含む登録済みメニューを未登録と誤判定する

- 症状: `Menu.List`の`items`には対象pathがあるのに、`safe-unity -Action MenuExists`が`guard_code: 24`を返す。
- 原因境界: JSON内の`+`が`\u002B`へエスケープされ、JSON生文字列に対する正規表現の完全一致では元のMenuPathと一致しない。
- 対応: `Invoke-AreaSafeUnity.ps1`は`ConvertFrom-Json`後の`items[].path`をOrdinal比較する。guard後はTokenReportsから当該captureを読み、構造化済みpathの有無で原因を確定する。
- 禁止: `+`を削った別MenuPathの手打ち試行、Evalでの代替確認、登録済みメニュー名をツール都合で変更すること。

### RTKがstaged diff checkの違反本文を表示しない

- 症状: `rtk git diff --cached --check`が終了コード1だけを返し、違反ファイル・行の本文を表示しない。
- 原因境界: ステージ前の`git diff --check`は未追跡ファイルを検査しない。新規ファイルをステージした後に末尾空白が検出されても、RTKのcompact diff経路は`--cached --check`の診断本文と元の終了コード2を会話へ転送しない場合がある。
- 対応: 締め作業のステージ後検査は`Tools/TokenUsage/staged-diff-check.ps1 [-PrintOutput]`へ固定する。内部で`Safe-Command.ps1`のcaptureと実終了コードを取得し、違反時は本文を表示して同じ終了コードで停止する。
- 禁止: RTKの空出力を違反0件と扱うこと、ステージ前`git diff --check`だけで新規ファイルも検証済みと判断すること、診断なしにcommitすること。
