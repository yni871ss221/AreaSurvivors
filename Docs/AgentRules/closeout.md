# Closeout And Memory Rules

- AreaSurvivorsではトークン節約を優先し、通常作業開始時のObsidian外部記憶読み込みは行わない。
- 作業ルール、恒久的な注意点、禁止事項、判断基準は `AGENTS.md` を正とする。
- Obsidianは、ユーザーが「履歴を読んで」「記憶を確認して」「Obsidianへ記録して」「締め作業」などを明示した場合だけ使う。
- Obsidianを使う場合も、読むノート名を事前に限定し、候補パス確認は最大2回までにする。
- ローカルVaultへの既存ノート追記は `Tools/TokenUsage/append-vault-note.ps1` を固定入口とし、`obsidian` CLIがPATH登録済みと仮定して直接呼び出さない。CLI固有機能が必要な場合だけ、状態変更前に `Get-Command obsidian` で利用可否を確認する。
- ユーザーが「締め作業」「作業終了」「今日の作業終了」「Obsidianへ記録」「コミット＆プッシュ」と依頼したら、`area-survivors-closeout` skill を使い、AreaSurvivors本体と `codex-external-memory` の両方を対象にする。
- Windowsで`codex-external-memory`が`core.autocrlf=true`かつMarkdownへ`eol=lf`を指定している場合、作業ツリーの`git diff --check`は内容エラーがなくてもCRLF→LF予告をstderrへ出し、RTK境界で非0終了になることがある。警告本文、`git config --get core.autocrlf`、`git check-attr text eol -- <note>`でこの境界を確定し、別検査へ切り替えず通常のstageでindexをLFへ正規化した後、`git diff --cached --check`を最終内容検査とする。
- 締め作業ではObsidianへ作業履歴を記録するだけでなく、その日の注意点、再発し得るミスの防止策、禁止事項、ユーザーのこだわりポイント、今後の判断基準を確認する。
- 今後のエージェント全体に効くルールはObsidianだけでなく `AGENTS.md` にも追記・更新する。単発の履歴や一時的な状況はObsidianへ残す。
