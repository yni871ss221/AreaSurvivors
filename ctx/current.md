# Current Task

## Goal

AreaSurvivorsの残TODOをすべて解決し、最終Steamリリース、記録、Git締め作業まで完了して対応を終了する。

## Latest Decision

- 旧`SwordRushEvolutionValidator`の現行仕様不一致2件は、最終検証前にValidatorを現行仕様へ更新して解決する。
- 入力／フォーカス、ウルトラワイドUI、Validator保守、リリース文書／ルールは責務別に確認・コミット済み。
- 最終Buildはクリーンな確定commitから作成し、Steamテストブランチへアップロードする。同一Build IDでチェックリストを完了してからdefault公開する。
- 公開後はBuild ID、Depot manifest ID、Git commit、確認結果、証跡を記録し、現在branchをpush、worktreeをクリーンにして終了する。

## Latest Verification

- branchは`feature/03_releaseUpdate`。入力／フォーカス`5f85b7ba`、ウルトラワイド`4d7daba1`、Validator保守`ea2418c3`、リリース文書`185d827b`へ責務別コミット済み。
- 入力、フォーカス、ドロップダウン、ウルトラワイド対象画面はユーザー実機確認済み。責務別diffレビュー済み。
- `SwordRushEvolutionValidator`の表示条件期待値を「武器Lv.10」＋「ゲームプレイ回数5回以上」へ更新し、旧`animationFrames`要求をAnimator／AnimationClip／SpriteRenderer参照検査へ置換済み。ゲーム側コード、数値、Prefab差分なし。
- Editor Assembly current、Unity Script Compile passed、`Area Survivors/Validate/Sword Rush Evolution` passed（failed 0／warnings 0／errors 0）、対象`Git.Check` passed。
- `FrostStormSpike Animator visual is missing`は、Visual欠落ではなく、ネスト済みVisualをValidatorだけがルート直下検索していた誤検知と特定。Migrationと同様の再帰検索へ修正済み。
- Editor Assembly current、Unity Script Compile、Input System Migration、HUD Layout Mutation Guard、Combat Animator Migration、Sword Rush Evolutionがすべてpassed。Console Error 0件、Git diff check、current-context-guardもpassed。

## TODO

- 確定commitからSteam配信Buildを作成し、テストブランチへデプロイする。
- 新Build IDで`Docs/Release/SteamReleaseChecklist.md`の全`STOP-SHIP`を確認する。Steam Cloud一般アカウント、Steam Input／Xbox／Bluetooth DS4、全画面遷移／抜き差し、16:9／21:9フルスクリーン、一般アカウント起動／セーブ／実績／正常終了を含む。
- 同一Build IDをSteam defaultへ公開し、default経路の起動と主要修正を最終スモーク確認する。
- 公開記録を残し、必要な最終文書commit／push、Token.Summary、`ctx/current.md`整理、クリーンworktree確認で締める。

## Blocker

- コード検証上の既知Blockerは解消済み。
- Steam公開は、新Build IDのリリースチェックリストが未確認のためまだ進めない。
