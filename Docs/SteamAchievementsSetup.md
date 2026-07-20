# Steam実績設定

App ID: `4980380`

Steamworksの「統計と実績」で以下のAPI名を完全一致で登録し、全項目の設定後に変更内容をPublishする。
全実績を公開表示とし、Steam Statsによる途中進捗表示は初期実装では使用しない。

| API名 | 日本語名 | 日本語説明 | English name | English description | 解除済みアイコン |
|---|---|---|---|---|---|
| `ACH_FIRST_SORTIE` | 初陣 | 初めて戦場に出撃する | First Sortie | Enter battle for the first time | `first-sortie.png` |
| `ACH_KILL_100` | 百人斬り | 累計100体の敵を倒す | Hundred Foes | Defeat 100 enemies in total | `kill-100.png` |
| `ACH_KILL_1000` | 千軍撃破 | 累計1,000体の敵を倒す | Thousand Foes | Defeat 1,000 enemies in total | `kill-1000.png` |
| `ACH_KILL_10000` | 万夫不当 | 累計10,000体の敵を倒す | Ten Thousand Foes | Defeat 10,000 enemies in total | `kill-10000.png` |
| `ACH_CLEAR_STAGE_1` | 最初の勝利 | ステージ1をクリアする | First Victory | Clear Stage 1 | `clear-stage-1.png` |
| `ACH_CLEAR_STAGE_2` | 第二戦線突破 | ステージ2をクリアする | Break the Second Line | Clear Stage 2 | `clear-stage-2.png` |
| `ACH_CLEAR_STAGE_3` | 第三戦線制圧 | ステージ3をクリアする | Master of the Third | Clear Stage 3 | `clear-stage-3.png` |
| `ACH_CLEAR_STAGE_4` | 全土奪還 | ステージ4をクリアする | Area Survivor | Clear Stage 4 | `clear-stage-4.png` |
| `ACH_FIRST_EVOLUTION` | 進化の兆し | 初めて武器を進化させる | Evolution Begins | Evolve a weapon for the first time | `first-evolution.png` |
| `ACH_ALL_EVOLUTIONS` | 武器進化の極致 | 現在実装されている全ての進化武器を発見する | Evolution Arsenal | Discover every currently available evolved weapon | `all-evolutions.png` |
| `ACH_MAX_ALL_SKILLS` | すべてを極めし者 | 現在実装されているスキルツリーの全ノードを最大レベルにする | Master of Every Skill | Max out every currently available skill tree node | `max-all-skills.png` |
| `ACH_ALL_RELICS` | 遺物収集家 | 現在実装されている全てのレリックを獲得する | Relic Collector | Collect every currently available relic | `all-relics.png` |
| `ACH_CLEAR_ALL_DIFFICULTY_5` | 最高難易度の覇者 | 現在実装されている全ステージを難易度5でクリアする | Master of Difficulty Five | Clear every currently available stage on Difficulty 5 | `clear-all-difficulty-5.png` |

## アイコン

- 解除済み: `Docs/SteamStore/Achievements/Icons/Unlocked/`
- 未解除: `Docs/SteamStore/Achievements/Icons/Locked/`
- 未解除アイコンは対応する解除済みアイコンを白黒化したものを使用する。
- 全画像は正方形PNG、最終サイズ256x256、文字・ロゴ・透かしなし。

## 既存データの扱い

- プレイ回数、累積討伐数、ステージクリア、武器進化、スキル、レリックは既存セーブから遡及判定する。
- 既存セーブには難易度5の実クリア履歴がないため、全ステージ難易度5実績はアップデート後のクリア記録だけを使用する。
- Steam未起動・オフライン時もゲーム進行を止めず、次回Steam接続成功時にセーブ全体を再評価する。
- Steam側で解除済みの実績をゲーム側から取り消さない。

## 実機確認

1. Steamworks側へ全API名、日英名称・説明、解除済み／未解除アイコンを登録する。
2. 変更をPublishする。これはストアやゲーム本体のリリース操作ではない。
3. App ID `4980380` の開発ビルドをSteamクライアントから起動する。
4. 条件達成時にSteam Overlay通知が1回だけ表示されることを確認する。
5. Steamライブラリの実績一覧へ解除状態が保存されることを確認する。
6. 問題時はSteamインストール先の `logs/stats_log.txt` を確認する。
