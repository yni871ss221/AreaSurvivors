# エリアサバイバー（仮）

Unity 2022.3 LTS 向けの初期プロトタイプです。

## 構成

- `Assets/AreaSurvivors/Scenes`: タイトル、オプション、ロビー、強化、ゲームの5 Scene
- `Assets/AreaSurvivors/Prefabs`: プレイヤー、敵、塔、経験値オーブ、ダメージ表示
- `Assets/AreaSurvivors/Scripts`: ゲーム本体、UI、セーブ、エディタ生成処理
- `Packages/manifest.json`: UniCLI Server を Git URL で導入

## UniCLI

UniCLI は CLI 本体と Unity Package の両方が必要です。このプロジェクトでは Unity Package を `manifest.json` に追加済みです。

Unity Editor でこのプロジェクトを開いた後、以下を実行してください。

```powershell
unicli check
unicli exec Compile --json
```

接続できない場合は Unity Editor を開き直してから再実行してください。
