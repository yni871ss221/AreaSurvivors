# Code Navigation

C#／PowerShellの構造確認、検索、限定読取、diffにだけ適用する。入口は `Tools/TokenUsage/area-tool.ps1`。

## 調査順

1. 型・メンバー・引数・MenuItem・参照候補は `-Operation Code.Symbol -Symbol <exact-name>`。
2. 単一ファイルの構造と、内容SHAが一致する場合の意味要約は `-Operation Code.File -Path <project-relative-path>`。
3. 正確な文字列、数値、属性、コメントは `-Operation Code.Search -Pattern <text> -Path <path>`。
4. 2シンボル間の呼出経路はGraphify `Path`、影響候補は `Affected`。自然文やUnity YAMLには使わない。
5. 実装判断に必要な範囲だけ `-Operation Code.Read` で読む。

構造インデックスは `Assets/AreaSurvivors/**/*.cs` と `Tools/**/*.ps1` をQuery時のSHA-256比較で自動更新する。クラスごとの登録は不要。対象ルート変更時だけ内部Indexerと責務別自己テストを更新する。

意味要約はGit管理外の生成キャッシュとし、実作業で対象を理解した後だけ `Code.Summary.Store` へ現在SHAと短い責務・流れ・不変条件・副作用・検証を保存する。SHA不一致時は表示も保存も拒否する。要約は候補絞り込み用であり、編集時の条件式、数値、API契約は対象実装を限定読取する。
実使用のhit／miss／失効は `Code.Summary.Stats`、固定表示比較は `Benchmark.SummaryCache` で確認し、利用率が低い場合は対象を増やさず縮小または削除する。

## 検索と読取

- 初手で `Assets/AreaSurvivors` 全体へ広域検索しない。候補Path不明時は `Code.Search -SearchMode Files|Summary` で絞る。
- 周辺行が必要なら `Code.Search -Context <N>`。コードリテラルは `-Literal` を付ける。
- 行範囲は `Code.Read -StartLine <N> -EndLine <N>`、複数範囲は `-Ranges "1-40;120-160"`。
- 同じチャットで確認済みの範囲は、対象変更や失敗がない限り再読しない。
- `.unity`／`.prefab`／`.asset` は本文検索を避け、`Unity.Search`、Reporter、Validatorを先に使う。

## diff

- `Git.Diff -DiffMode Summary`、対象Path、必要なFull差分の順に確認する。
- 空白検査は `Git.Check -Path <paths> [-ExcludeUnityMeta]`。
- 未コミット差分が多い作業ツリーで広域diffを実行しない。

## 分割判断

- TokenReportsの読取頻度×表示量、メンバー数、同時理解が不要な責務境界を根拠にする。
- 分割後は入口から責務ファイルへ一意に到達し、旧実装・重複説明・無効化コードを残さない。
