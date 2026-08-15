# Release And Steam Review

- Steam審査提出、テストブランチ更新、default公開の準備では、最初に`Docs/Release/SteamReleaseChecklist.md`を全体確認する。
- 対象Build ID、Depot manifest ID、Git commit、確認者、一般アカウント、使用コントローラ、証跡をチェック結果へ記録する。
- `STOP-SHIP`が未確認または`FAIL`なら審査提出・default公開を進めず、Blockerとして報告する。
- 新しいSteam審査指摘またはリリース後障害が発生した場合は、再現条件、原因、修正Build、再発防止項目をチェックリストの障害記録へ追加する。
- ストアのSupported FeaturesとSteamworks設定は、公開対象の同一Build IDで一般利用者が利用できる機能だけを有効にする。
