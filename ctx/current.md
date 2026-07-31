# Current Task

## Goal

`feature/03_releaseUpdate`ブランチで、Area Survivorsのリリース後更新に備える。

## Latest Decision

- Git上のリリース候補差分は`feature/02_GameSystemUpdate`から`main`へ統合し、以後の更新は`feature/03_releaseUpdate`を起点にする。
- Steamのリリース候補はApp ID `4980380`、Build ID `24466612`、Depot ID `4980381`。`default`ブランチへ設定済み。
- ストアプレゼンスは承認・公開済み。ゲームビルドはValveレビュー待ちで、リリース予定は2026年8月7日0時（JST）。
- ローンチ割引は10%・7日間。Steamは自動リリースされないため、承認後の予定時刻に手動でリリースする。
- Valveから修正指示がない限り、審査中のSteam `default`ビルドは変更しない。

## Latest Verification

- Build ID `24466612`をSteamクライアントからインストールし、起動設定修正後に通しプレイを完了。ユーザー確認で問題なし。
- SteamPipeアップロード、Depot取得、`Area Survivors.exe`起動、ゲーム終了後のプロセス終了を確認済み。
- Unityコンパイル、関連Validator、Console Warning／Error 0件をリリースビルド作成前に確認済み。
- Command Tool自己テスト（7 modules）、変更対象の`Git.Check`、current-context guardが成功。

## TODO

- Valveのゲームビルドレビュー結果を確認し、フィードバックがあれば対応して再提出する。
- 2026年8月7日の予定時刻にSteamworksで`アプリをリリース`から手動リリースし、10%割引が7日間適用されたことを確認する。
- リリース後に一般アカウントで購入、インストール、起動、実績、セーブ、終了時の「プレイ中」解除を確認する。
- 旧Sword Rush Evolution Validatorの進化条件／交互フレーム2件と現行仕様の整合は、次回Validator保守時に判断する。

## Blocker

- Steam一般公開のみValveのゲームビルドレビュー承認待ち。Gitおよびリリース後更新の開発作業にBlockerはない。
