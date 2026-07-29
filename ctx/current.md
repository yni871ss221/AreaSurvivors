# Current Task

## Goal

トークン削減基盤の実運用効果を観測する。構造的な実装作業は完了済み。

## Latest Decision

- 検索、読取、diff、Graphify、Unity、Token計測は`area-tool.ps1`を唯一の公開入口とする。
- 操作契約の正は`AreaTool/operations.psd1`。内部WrapperやReporterを公開Docsから直接呼ばない。
- TokenReportsはJSONLを正とし、Library内SQLite Indexへ追記分だけ自動反映する。
- 意味要約はAreaSurvivors C#とTools PowerShellだけを対象にし、現在SHA一致時だけ利用する。
- 外部記憶は運用しない。現在状態はこのファイル、完了履歴は`ctx/archive/`、変更履歴はGitを正とする。
- 明示された「締め作業」は、Project状態確認、Token集計、current整理、検証、commit、現在branchのpushまでを含む。

## Latest Verification

- `area-tool -Operation Test.Commands`: 7 modules passed。
- Context GuardはAGENTS 50／70行、current 37／60行でstatus `ok`。コード、文書、Tools、ctxの対象差分は`git diff --check`成功。
- Token集計は旧全件走査と5条件で一致し、変更なし約0.4〜0.7秒、型付き入口約0.76秒。
- 締め作業の直近8 recordsは表示7,232 token、measurement coverage 100%、blocked／high／critical 0件。
- 固定11シナリオ読取コストはGit HEAD比で合計88.9%減。
- Unity Compile 3回成功、最終Console Error 0件、20%逓減後の通しプレイはユーザー確認済み。

## TODO

- 数回の実運用後、Operation別TokenReportsと`Code.Summary.Stats`のhit率を再集計し、固定Benchmarkとの差を確認する。

## Blocker

- なし。

## Constraints

- Branch: `feature/02_GameSystemUpdate`。既存の未コミット変更を戻さない。
- Scene／Prefab／HUDのユーザー調整値を正とし、Runtime／Migration／Validatorから上書きしない。
- UI見た目確認はユーザーが行う。明示依頼なしにPlay Modeを開始しない。
- 通常開始時は`AGENTS.md`とこのファイルだけを読み、`ctx/archive/`は履歴確認時だけ使う。
