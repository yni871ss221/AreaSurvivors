# Combat Rules

- 攻撃、弾、爆発、戦闘演出では `area-survivors-attack-animation` skill を使う。
- 敵アニメーション取り込みでは `area-survivors-enemy-animation-import` skill を使う。
- 見た目と当たり判定が一致すべき攻撃は、調整可能なColliderを優先する。
- Knightの斬撃など、画面上の範囲が重要な攻撃で隠れた `OverlapBoxAll` / `OverlapCircleAll` 判定を残さない。
- 武器の範囲が広がる場合は、当たり判定だけでなく見た目のサイズも追従させる。
- 火球や爆発のVisual Scaleを爆発半径に直結させない。見た目の大きさとダメージ範囲は必要に応じて別管理にする。
- 着弾時のPixelBurst系バーストは通常攻撃では不要。負荷や視覚ノイズを増やさない。
