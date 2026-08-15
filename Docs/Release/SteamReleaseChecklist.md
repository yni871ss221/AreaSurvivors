# Steam Release Checklist

Area SurvivorsのSteam審査提出、テストブランチ更新、default公開前に使用する。
ストア表示と実際のBuildが一致し、一般利用者がDeveloper権限やローカル設定に依存せず利用できることを確認する。

## 運用ルール

- 各項目を `PASS`／`FAIL`／`WAIVED`／`RISK-ACCEPTED`／`N/A` で記録し、確認日、確認者、証跡を残す。
- `STOP-SHIP`が1件でも`FAIL`または未確認なら、審査提出・default公開を行わない。
- `CAUTION`を見送る場合は`WAIVED`とし、理由と対応予定を記録する。
- 実施不能な`STOP-SHIP`を代替証跡で進める場合は、リスク、実施不能理由、代替証跡、承認者を記録し、ユーザーが明示承認した場合だけ`RISK-ACCEPTED`とする。未確認のまま黙示的に適用しない。
- Steam配信用BuildはSteamクライアントのテストブランチから起動する。EXEの直接起動だけで合格としない。
- Developer Compライセンスだけでなく、Developer権限を持たない一般アカウントでも確認する。
- チェック結果は本書の「リリース判定記録」を複製して残す。

## 対象リリース

| 項目 | 記録 |
| --- | --- |
| バージョン／リリース名 |  |
| Git commit |  |
| Steam Build ID |  |
| Depot manifest ID |  |
| テストブランチ |  |
| 確認日／確認者 |  |
| Developerアカウント |  |
| 一般アカウント |  |
| 使用コントローラ／接続方式 |  |
| 確認解像度／画面比率／フルスクリーン方式 |  |

## 1. Buildとストア表示

- [ ] `STOP-SHIP` リリース対象commitが確定し、意図しない未コミット差分を含んでいない。
- [ ] `STOP-SHIP` SteamworksにアップロードしたBuild IDと、テストブランチでインストールされたBuild IDが一致する。
- [ ] `STOP-SHIP` 審査提出／default公開するBuildが、テスト完了済みの同一Build IDである。
- [ ] `STOP-SHIP` SteamストアのSupported Featuresが、現在のBuildで実際に利用できる機能だけを表示している。
- [ ] `STOP-SHIP` Store page、App Admin、Steam Cloud、Controller設定の変更を保存し、必要な変更をPublishした。
- [ ] ロールバック対象Build IDと復旧手順を記録した。

## 2. Steam Cloud

詳細設定は [`Docs/SteamCloudSetup.md`](../SteamCloudSetup.md) を参照する。

- [ ] `STOP-SHIP` App Data AdminのSteam Cloudページで **`Cloud support for developers only` が未チェック**である。
- [ ] `STOP-SHIP` Steam Cloudの設定変更をPublish済みである。
- [ ] `STOP-SHIP` Developer権限を持たない一般アカウントで、終了時にセーブがアップロードされる。
- [ ] `STOP-SHIP` 別PCまたはクリーンな環境で、同じ一般アカウントのセーブがダウンロード・復元される。
- [ ] `STOP-SHIP` 同期対象が`progression-save-v1*.json`に限定され、一時ファイルや端末固有設定を同期しない。
- [ ] `cloud_log.txt`またはSteam Consoleの結果を証跡として保存した。

## 3. コントローラとSteam Input

Steamworks参考資料: [Getting Started for Developers](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)

- [ ] `STOP-SHIP` 「Partial Controller Support」または「Full Controller Support」を表示する場合、ゲームプレイ中の全機能をコントローラで操作できる。
- [ ] `STOP-SHIP` Xbox系コントローラで、タイトル、全メニュー、ゲーム開始、移動、攻撃、建築、レベルアップ、ポーズ、終了まで確認した。
- [ ] `STOP-SHIP` DualShock 4をBluetooth接続し、上記と同じ一連の操作を確認した。
- [ ] `STOP-SHIP` D-padの上下左右、左スティック、決定、キャンセル、各Face Buttonの対応が正しい。
- [ ] `STOP-SHIP` Steamクライアントのゲーム別設定をデフォルトへ戻し、Steam Input有効状態で操作できる。
- [ ] `STOP-SHIP` 利用者側の「Steam Inputを無効にする」設定を前提にしていない。
- [ ] `STOP-SHIP` Steamテストブランチから起動し、LocalTestや配信用EXEの直接起動だけで判定していない。
- [ ] `STOP-SHIP` タイトル、ロビー、アップグレード、ゲーム、結果画面を往復しても入力が継続する。
- [ ] `STOP-SHIP` ゲーム中にコントローラを抜き差しし、再接続後に入力が復帰する。
- [ ] `STOP-SHIP` Player.logで通常時の入力経路が`route=input-system`となり、旧Input Manager／DirectInput固有マッピングへフォールバックしていない。
- [ ] `STOP-SHIP` Controller Supportをストアに表示する場合、SteamworksでDeveloper's Recommended Configurationを作成・公開した。
- [ ] キーボード／マウス操作がコントローラ対応後も正常である。

## 4. コントローラ利用時の品質

- [ ] `CAUTION` コントローラ操作へ切り替えたらマウスカーソルが画面から消え、マウス操作時には再表示される。
- [ ] `CAUTION` コントローラのHome／Guideボタン等でSteam Overlayを開いた際、ゲーム進行が停止する。
- [ ] `CAUTION` ゲーム中にコントローラを切断した際、自動的にポーズするか、安全な再接続案内を表示する。
- [ ] `CAUTION` Steam Overlayを閉じた後、入力とポーズ状態が正常に復帰する。
- [ ] `CAUTION` Alt+Tab、フォーカス喪失・復帰後も入力デバイスが正常に機能する。

## 5. 表示解像度と画面比率

- [ ] `STOP-SHIP` Steamテストブランチの公開対象Buildで、16:9（1920×1080または同等）とウルトラワイド21:9以上（2560×1080、3440×1440または同等）を確認した。
- [ ] `STOP-SHIP` ウルトラワイドはフルスクリーンで確認し、Unity EditorのGame Viewシミュレーションだけで合格としていない。
- [ ] `STOP-SHIP` タイトル、オプション、ロビー、強化、武器図鑑、レリック一覧、ゲームHUD、レベルアップ、ポーズ、結果画面を表示し、主要UIを横断確認した。
- [ ] `STOP-SHIP` 決定、キャンセル、戻る、ゲーム開始など操作に必要なボタンが全体表示され、マウスとコントローラの両方で操作できる。
- [ ] `STOP-SHIP` 初期フォーカスの白枠が画面内に表示され、すべての操作対象へフォーカス移動できる。
- [ ] `STOP-SHIP` 画面上下端の見出し、所持数、説明、ツールチップ、モーダルが見切れず、重なりや意図しない縦横の引き伸ばしがない。
- [ ] 背景が画面全体を意図した方法で覆い、意図しない黒帯や描画欠けがない。
- [ ] 解像度／フルスクリーン設定を変更して画面遷移しても、UI配置と入力が正常に復帰する。
- [ ] 32:9などさらに横長の環境をサポート対象とする場合、その解像度でも同じ一連の確認を行った。
- [ ] 確認解像度、画面比率、フルスクリーン方式、使用ディスプレイ、各画面のスクリーンショットを証跡として残した。

## 6. 一般アカウントでの最終確認

- [ ] `STOP-SHIP` Developer権限を持たないアカウントで購入／ライセンス付与、インストール、初回起動ができる。
- [ ] `STOP-SHIP` Steamクライアントから起動し、正しいApp IDとして初期化される。
- [ ] `STOP-SHIP` セーブ、Steam Cloud、実績解除、言語設定が正常である。
- [ ] `STOP-SHIP` 正常終了後、Steamの「プレイ中」表示が解除される。
- [ ] `STOP-SHIP` Player.logにException／Error／Crashがない。
- [ ] アンインストール／再インストール後の起動とCloud復元を確認した。

## 7. リリース判定記録

| 分類 | 結果 | 証跡／備考 |
| --- | --- | --- |
| Buildとストア表示 |  |  |
| Steam Cloud |  |  |
| Controller／Steam Input |  |  |
| Controller品質 |  |  |
| 表示解像度／画面比率 |  |  |
| 一般アカウント最終確認 |  |  |
| 総合判定 | `GO`／`NO-GO` |  |

承認者:
承認日時:
公開対象Build ID:

## 過去の審査指摘・障害記録

### Steam Cloudが一般利用者に同期されない

- 区分: `Failure`
- Steam審査報告のBuild ID: `24466612`
- 症状: ストアにはSteam Cloud対応と表示されているが、Steamworksで`Cloud support for developers only`がチェックされ、非Developer利用者は同期できなかった。
- 原因: Developer向け限定設定を解除しないまま審査Buildを提出した。
- 再発防止: Cloud欄のチェック解除、Publish、一般アカウントと別環境によるアップロード／復元を`STOP-SHIP`とする。

### Partial Controller Support表示と実Buildが不一致

- 区分: `Failure`
- Build ID: 提供された審査文面では個別記載なし。
- 症状: ストアにPartial Controller Supportを表示していたが、ゲームプレイ機能をコントローラで完結できず、Bluetooth接続のDualShockがまったく機能しなかった。
- 原因: Steam配信経路、Steam Input有効状態、実機・接続方式を含むテストマトリクスが不足していた。
- 再発防止: Xbox系とBluetooth接続DualShockの両方で、Steamテストブランチから全ゲームプレイ機能を確認する。満たせない場合は公開前にController Support表示と推奨テンプレートを外す。

### Steam Input有効時の方向・ボタン配置崩れ

- 区分: リリース後障害
- 症状: LocalTestでは操作できたが、Steam配信版では無反応またはD-padの左が上、右が下、Face Buttonの役割が入れ替わる状態になった。利用者設定でSteam Inputを無効にすると一時的に動作した。
- 原因: 旧Input Manager／DirectInput固有の軸・ボタン番号と、Steam Inputが提示するXInput経路を同一マッピングとして扱っていた。配信用EXEの直接起動をSteamテストブランチ起動の代替にしていた。
- 恒久対応: Unity Input System `1.14.2`専用へ移行し、論理Gamepad入力へ統一。旧Input Manager／WinMMフォールバックを撤去した。
- 検証済み修正Build: `24751074`（Steam `input-test`）。全Scene、抜き差し、キーボード／マウスを確認し、通常時`route=input-system`を記録した。
- 追加原因: Unity Editor内で`SteamAPI.Init()`するとSteam Inputの所有切替によりDS4がUnityから切断された。EditorではSteam API初期化をスキップし、Steam連携はBuildで確認する。

### Steam審査のController品質指摘

- 区分: `Caution`（審査通過の必須条件ではない）
- コントローラ操作中もマウスカーソルが残り、手動で画面外へ移動する必要があった。
- Steam Overlayを開いてもゲームがポーズせず、Overlay操作中も進行した。
- ゲーム中にコントローラを抜いても自動ポーズしなかった。
- Controller Support表示がある場合に推奨されるDeveloper's Recommended Configurationが未設定だった。
- 再発防止: 本書「コントローラ利用時の品質」を毎回確認し、未対応項目は`WAIVED`理由を明記する。

### ウルトラワイドで画面下部の操作ボタンが見切れる

- 区分: リリース後障害
- 症状: 21:9以上のウルトラワイド・フルスクリーンで、ロビー、強化、武器図鑑、レリック一覧の下部ボタンが画面外へ出て操作できなかった。
- 原因: 1280×720基準のCanvasScalerが`Match Width Or Height = 0`（横幅完全優先）で、ウルトラワイド時に有効な基準高さが720未満へ縮み、下端UIが表示範囲外になった。
- 恒久対応: `Lobby UI`、`Upgrade UI`、`Weapon Book UI`、`Relic Book UI`のCanvasScalerを`Screen Match Mode = Expand`へ変更し、RectTransformの個別Runtime補正は追加しない。
- 検証: ウルトラワイドのロビー、強化、武器図鑑、レリック一覧で表示・ボタン操作・初期フォーカスを確認済み。他画面も問題なし。
- 再発防止: 本書「表示解像度と画面比率」を毎リリースの`STOP-SHIP`とし、Steamテストブランチの同一Buildで実機フルスクリーン確認を行う。
