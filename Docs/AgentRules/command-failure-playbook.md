# Command Failure Playbook

コマンド失敗、タイムアウト、無応答、想定外の戻り値が出た時に、機能実装を続ける前に原因と再発防止を残すための手順。

## 原則

- 別コマンド、別Shell、Eval、手動Editor操作へ自動的に切り替えない。まず失敗した方式の境界を調べる。
- 状態変更を止め、実行コマンド、引数、終了コード、経過時間、capture path、Unity Console/Editor状態を保存する。
- 同種の失敗を2回確認したら手打ち再試行を止める。3回目の前にWrapper、Validator、Reporterのいずれかへ部品化する。
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

### functions.exec内のapply_patch成功戻り値が空になる

- 症状: `text(await tools.apply_patch(patch))`を実行したcellが終了コード相当の異常を出さず、出力だけ`{}`または空になる。一方で対象ファイルのsentinel行は反映済みになっている。
- 原因: 入れ子`apply_patch`は成功時に表示用の戻り値を返さない場合があり、その`undefined`相当を`text(...)`へ直接渡すと成功表示が生成されない。Patch適用結果と会話への戻り値は別境界である。
- 対応: `await tools.apply_patch(patch); text("patch_submitted; verify sentinels")`のように送信完了だけを明示し、成功判定は`safe-read.ps1 -LiteralPattern`で各変更対象の固有sentinelを確認してから`scoped-diff-check.ps1`で確定する。
- 禁止: 空戻り値をPatch失敗とみなして同じPatchを再適用すること、反映確認前に別編集方式へ切り替えること。

### 複数Tempファイル削除で先頭の既消去Pathがapply_patchを止める

- 原因: 前工程や自動クリーンアップで既に消えたTempファイルを、古い作業一覧のまま複数ファイルDelete patchの先頭へ含めた。`apply_patch`は適用前検証で停止するため、後続の実在ファイルも削除されない。
- 対応: `Tools/TokenUsage/temp-file-presence-report.ps1 -Path Temp/AgentAssets/<file>`で各候補を1件ずつ確認し、`temp_file_exists: true`の対象だけを1ファイル単位のDelete patchへ渡す。既消去は成功済みクリーンアップとして扱い、同じ複数Delete patchを再送しない。
- 禁止: 過去の作成ログだけでTempファイルが残っていると仮定すること、先頭Pathだけを外して未確認の複数Delete patchを再送すること。

### Domain Reload後にUniCLI server.pidがAssetImportWorkerのPIDになる

- 症状: C# Compileは終了コード0で完了し、メインUnityも応答中だが、直後の`Menu.List`等は同じpipeへ5回接続して`Connection timeout`になる。`Library/UniCli/server.pid`はメインUnityではなく、MainWindowHandleが0のAssetImportWorker PIDを指す。
- 原因: UniCLI v1.5.0の`UniCliServerBootstrap.EnsurePidFile()`がAssetImportWorkerでも実行され、メインEditorのPIDファイルを上書きし得る。Compile成功とUniCLI server再接続成功は別境界である。
- 対応: `safe-unity`はCompile以外のUniCLI操作前にserver.pidのPID、Unity process、MainWindowHandleを確認し、不一致を`guard_code: 45`で接続前に拒否する。プロジェクトは`Packages/com.yucchiy.unicli-server`の埋め込みパッケージを正とし、Bootstrapの先頭で`AssetDatabase.IsAssetImportWorkerProcess()`を判定してWorkerのPID書き込みとServer起動を禁止する。更新・復旧後は`Tools/TokenUsage/validate-unicli-worker-guard.ps1`を先に通し、メインEditorでStart Serverを1回押してから失敗した同一操作だけを再開する。
- 禁止: connection timeout後にMenu、Console、Statusを順番に試すこと、server.pidをShellで手動上書きすること、`Library/PackageCache`のBootstrapを直接改変すること、埋め込みパッケージを削除して未修正版へ戻すこと。

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

### `safe-unity ConsoleErrors` の件数引数が見つからない

- 原因: 検索・読み取りWrapperの `-First` と、Unity Console入口の正式契約 `-MaxCount` を混同した。
- 対応: `safe-unity.ps1 -Action ConsoleErrors -MaxCount <件数>` を使い、自己テストでparam転送を固定する。
- 禁止: `-First`、`-Count`などの候補を手打ちで順番に試すこと。

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

### safe-searchの件数制限でrgがbroken pipeになる

- 原因: `rg -l ... | Select-Object -First N` は、N件到達時にPowerShellが入力パイプを閉じ、`rg` が終了コード `-1` になる場合がある。
- 対応: `rg` の全出力と終了コードを先に配列へ取得し、実エラーを伝播した後、配列へ `Select-Object -First N` を適用する。通常検索、`safe-search.ps1 -FilesOnly`、`focused-search.ps1` はすべてこの順序を固定する。終了コード1は一致なしとして正常な空結果へ正規化する。

### Web資料検索の出力超過後に直接URLをopenして拒否される

- 原因: 複数のWeb検索を1回へ集約して結果が切れた後、検索結果の参照IDを保持できないまま公式URLを直接`open`した。WebツールはそのURLを安全な参照先として認識できず、非再試行エラーを返した。
- 対応: API資料の調査は単一クエリか既知ページ1件だけを`response_length: short`で検索し、返された参照IDを同じ調査列で`open`する。検索出力が切れた場合は実装を止め、取得済みのローカル証拠で原因境界を確定するまで別URLや別Web手段へ切り替えない。
- 禁止: 複数クエリのmedium/long応答、切れた検索結果の直接URL再構成、同一URLの表記変更による再試行。

### 新規PNGのMigrationが成功表示でもmetaを生成していない

- 原因: 外部処理で`Assets/`へ配置したPNGはまだAssetDatabaseへ未登録であり、AssetDatabase検索だけでパスを解決するImporterがnullを返して処理を中断した。Menu実行APIの`executed=true`はMenu呼び出し成立だけを示し、内部例外やMigration完了を保証しない。
- 対応: 正規のプロジェクト相対パスに対応するディスク上の実ファイルを先に確認し、そのパスを`AssetDatabase.ImportAsset`へ渡してからSprite設定を適用する。Migration/Validatorは成功時だけ`Library/AreaSafeUnity`へ完了マーカーを作成し、Menu実行結果だけで成功判定しない。
- 禁止: `.meta`不在を画像名やUnity接続不良と推測して別Importerへ切り替えること、`executed=true`だけでPrefab保存済みと報告すること。

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
- 対応: 初回利用時にhelpまたは`param(...)`を読み、件数上限は`safe-search.ps1 -First <件数>`だけで指定する。20件を超える意図的な検索だけ`-AllowMany`を併用する。
- 禁止: `-MaxResults`、`-Limit`、`-Top`を順番に試すこと。ParameterBindingException後は検索を再発行する前に正式契約へ戻る。

### safe-readのFirstが80行を超える

- 原因: 既定の会話出力上限80行を確認済みでも、ファイル全体の想定行数をそのまま`-First`へ渡した。
- 対応: `guard_code: 39`が返す`suggested_first=80`以下へ限定する。80行を超える意図的な単一範囲は`safe-read-batch.ps1`へ元の範囲を1回だけ渡して自動分割する。
- 禁止: `-First 100`等のGuard後に79、80などの候補を手打ち再試行すること。

### safe-readのStartLine-EndLineが80行を1行だけ超える

- 原因: 行範囲が両端を含むことを見落とし、`40-120`を80行ではなく81行として指定した。
- 対応: `guard_code: 39`が返す`suggested_end_line`へ限定するか、元の範囲を変更せず`safe-read-batch.ps1 -Ranges '<start>-<end>'`へ1回だけ渡して自動分割する。
- 禁止: Guard後に終了行を119、118と手打ちで調整すること、`-AllowHighOutput`で単純な境界ミスを回避すること。

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
- 対応: `safe-read.ps1`は`-PrintOutput`の推定出力が80行を超える場合を`guard_code: 39`で入口拒否する。範囲・一致数・Contextを狭めて直列に読む。`functions.exec` 1回につき `-PrintOutput`を伴う読み取りは1件だけとし、複数コマンドの出力を合算しない。単一呼び出しの出力予算を明示的に確保した場合だけ`-AllowHighOutput`を使い、その呼び出しと他の出力を並列化しない。
- 禁止: 切り捨て後に同じ大範囲読取を再発行すること、単体が80行以内でも複数の`-PrintOutput`を同じ`functions.exec`へまとめること、`-AllowMany`だけで大きい`-PrintOutput`を並列実行すること、capture済みの結果を未回収のまま別検索方式へ切り替えること。

### PowerShell `-File`へ`.cs`を直接渡して読み取ろうとする

- 原因: 並列コマンドの一部で`safe-read.ps1 -Path`を付け忘れ、対象C#パスをPowerShellの実行スクリプトとしてbindした。
- 対応: ソース読み取りは必ず`safe-read.ps1 -Path <既存ファイル>`を入口にする。PowerShell `-File`の直後は`.ps1` Wrapperだけを置き、C#やSceneパスを置かない。
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

### safe-unity-searchが別クエリの最新レポートを返す

- 原因: 複数プロセスが固定の検索語ファイルと最新レポート選択を共有し、Write→Menu→report選択→deleteが競合する。最新時刻だけでは自分の結果と証明できない。
- 対応: 名前付きMutexで検索全体を直列化し、実行前のreport署名を保存する。実行後に新規または更新されたreportだけを候補とし、先頭の `Query:` が要求語と完全一致することを必須にする。不一致は `guard_code: 30` で停止する。
- Play Mode中はMenu受付が成功してもReporterファイルが生成されない場合がある。検索語を書き込む前に `PlayMode.Status` を確認し、Play中は `guard_code: 32` で拒否する。Edit ModeでもReporter書込みはMenu応答より遅れる可能性があるため、検索語ファイルを保持したまま最大10秒だけ一致reportを待つ。

### Menu.Executeが`executed: true`でもMenu本体が例外終了する

- 原因: UniCLIのMenu応答は`EditorApplication.ExecuteMenuItem`の受付結果であり、Menuメソッド内部の例外や後続Scene保存の完了を表さない。途中例外でもコマンド終了コード0・`executed: true`になり得る。
- 対応: Migration/Reporterは対象Sceneを変更する前に全検索対象・複製元・保存先をPreflightし、成功行または完了markerを処理末尾でだけ出す。呼び出し側は専用Validator、完了marker、Console Errorのいずれかで副作用完了を確認する。
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
