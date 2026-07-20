# Steam Cloud Setup

## ゲーム側の保存対象

- 進行データは `Application.persistentDataPath/progression-save-v1.json` へ保存する。
- 前回保存は `progression-save-v1.backup.json` として保持し、メインファイル破損時に読み込む。
- 旧バージョンの `PlayerPrefs` にある `AreaSurvivors.Save.v1` は、初回ロード時にJSONへ自動移行し、移行成功後だけ旧キーを削除する。
- 画面、音量、言語設定は端末固有設定としてクラウド同期しない。

Windows版の保存先は現在のPlayer Settingsにより次の場所になる。

`%USERPROFILE%/AppData/LocalLow/Codex/Area Survivors/`

## Steamworks Auto-Cloud設定

Steamworks App AdminのSteam Cloud設定で、ユーザーごとの容量とファイル数を設定した後、次のRoot Pathを追加する。

| 項目 | 設定値 |
| --- | --- |
| Root | `WinAppDataLocalLow` |
| Subdirectory | `Codex/Area Survivors` |
| Pattern | `progression-save-v1*.json` |
| OS | `Windows` |
| Recursive | `Disabled` |

このPatternはメインとバックアップのJSONだけを同期し、一時書き込みファイル `progression-save-v1.tmp` を除外する。

設定後はページ下部で保存し、Steamworks変更をPublishする。公開済みタイトルで先行確認する場合は、最初にdeveloper-onlyモードを使う。

## 確認手順

1. Developer Compライセンスを持つSteamアカウントでサインインする。
2. Steam Consoleで `testappcloudpaths <AppId>` を実行する。
3. Steamからゲームを起動し、進行データを更新して終了する。
4. Steam Consoleまたは `%Steam Install%/logs/cloud_log.txt` でアップロードを確認する。
5. 別PCで同じSteamアカウントから起動し、進行データが復元されることを確認する。
6. テスト後は `testappcloudpaths 0` を実行し、developer-onlyモード解除後に設定をPublishする。

macOSまたはLinuxへ対応する場合は、同じRoot PathをAll OSesに変更し、各OSの `Application.persistentDataPath` に対応するRoot Overrideを追加する。
