# Current Task

## Goal

Steam配信版のコントローラ入力をUnity Input Systemへ完全移行し、テスト済み候補をdefault公開できる状態にする。

## Latest Decision

- Unity Input System `1.14.2`を唯一の入力経路とし、旧Input Manager／WinMMフォールバックは使用しない。
- DirectInput／XInput固有の軸番号・ボタン番号を使わず、`Gamepad`の方向・South／East等の論理入力へ統一する。
- `Active Input Handling`はInput System専用とする。
- Unity Editorでは`SteamAPI.Init()`を実行せず、Steam連携確認はLocalTest／Steamビルドで行う。製品ビルドのSteam初期化は維持する。
- Steamworks側は一般ゲームパッド用テンプレートを前提とし、機種別Rawマッピングは追加しない。

## Latest Verification

- runtime／editor Assembly current、Input System Migration Validator passed（failed 0／warnings 0／errors 0）、Console Error 0件。
- Runtimeコードの旧`Input.*`／`KeyCode`／WinMM参照は0件。
- 新Input System専用ビルドをSteam `input-test`へBuild ID `24751074`として配信済み。
- ユーザー実機確認でタイトル、オプション、ロビー、アップグレード、武器図鑑、レリック、ゲーム、Scene往復、パッド抜き差し、キーボード／マウスがすべて正常。
- Player.logでは通常時と全Sceneが`XInputControllerWindows`／`interface=XInput`／`route=input-system`。切断中のみ`route=none`となり、再接続後に復帰。Exception／Error／Crash 0件。
- EditorでのDS4切断原因は、Editor内の`SteamAPI.Init()`によるSteam Inputの所有／マッピング切替と確定し、Editor限定の初期化スキップで解消済み。

## TODO

- default公開後、一般アカウントで購入、インストール、起動、実績、セーブ、終了時の「プレイ中」解除を確認する。
- 旧Sword Rush Evolution Validatorの進化条件／交互フレーム2件と現行仕様の整合は、次回Validator保守時に判断する。

## Blocker

- 入力対応としてのBlockerなし。作業ツリーには今回のコミット対象外となる既存差分が残る。
