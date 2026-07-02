# Combat Rules

- 攻撃、弾、爆発、戦闘演出では `area-survivors-attack-animation` skill を使う。
- 敵アニメーション取り込みでは `area-survivors-enemy-animation-import` skill を使う。
- 見た目と当たり判定が一致すべき攻撃は、調整可能なColliderを優先する。
- Knightの斬撃など、画面上の範囲が重要な攻撃で隠れた `OverlapBoxAll` / `OverlapCircleAll` 判定を残さない。
- 武器の範囲が広がる場合は、当たり判定だけでなく見た目のサイズも追従させる。
- `Circle Visual`、`Range Visual`、`Ellipse Range Outline`、`* Area Visual` など範囲そのものを示す表示は `PaperBillboard.faceCamera=true` や `Camera.main.transform.rotation`、Transform Rotation X/Y による疑似パース補正を使わない。
- 範囲表示、ダメージ判定、セル塗りが一致すべき攻撃は、同じ半径・縦横比からMesh/LineRenderer、Overlap候補、TileGrid塗りを計算する。見た目だけを傾けて合わせない。
- セル塗りと楕円表示を一致させる場合は `TileGrid.WorldCellSize()` を基準に縦横比を求め、楕円に少しでも重なるセルを塗る。固定値のRotation X `-40` 等で調整しない。
- 弾本体や落下矢など「範囲ではないSprite演出」のビルボードは許容するが、Area/Range Visualとは分けて扱う。
- 火球や爆発のVisual Scaleを爆発半径に直結させない。見た目の大きさとダメージ範囲は必要に応じて別管理にする。
- 着弾時のPixelBurst系バーストは通常攻撃では不要。負荷や視覚ノイズを増やさない。
