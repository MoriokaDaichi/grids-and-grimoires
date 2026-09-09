# 再検証 — モードB 手動ディープラン（cold-start 6サイクル）

- 日付: 2026-09-10
- ブランチ: `master`（改善ループ サイクル1〜31 の調整をすべて含む作業ツリー）
- 手法: モードB（`execute_code` 駆動）。常駐オートドライバ（WaveClear で進む/脱出、Reward で `RewardScreen.OnReturn` をリフレクション実行）＋手動経済判断。
- 方針: **資源注入なし**。ゲームAPI（ステ振り分け／建造・強化／研究割当／錬金釜変換／交換／依頼受領・受取／出撃）のみ。farm は脱出前提で戦利品確保、push だけ死んでよい。
- timeScale 40〜42。

## EditMode

| | 結果 |
|---|---|
| 検証前 | **206 / 206** グリーン（`GridsAndGrimoires.EditModeTests`）|
| 検証後（stop → RequestScriptReload → 再実行）| **206 / 206** グリーン（不変）|

## 到達深度の推移

| サイクル | 壁（push 死亡深度）| 終了時キット | このサイクルで解放したもの |
|---|---|---|---|
| 1 | **d11**（10wv, mhp5%）| HP180/Atk19/Def8、grid **4×4**、MegaFire+Flame+Fire+Thunder+Wind、`wand_apprentice`・`acc_mending_band`、設備 炉/机/釜/作業台 すべて Lv1、研究15(mg11) | 見習いの杖（→4×4）、全設備 Lv1、MegaFire |
| 2 | **d16**（15wv, mhp2%）| HP255/Atk58/Def14、grid **5×5**、GigaFire級はまだ / MegaFire+MegaThunder+Flame+Bolt+BuffDamage+受動2、`wand_oak`・`armor_cloth`・`acc_regen_torc`(タスク報酬)、炉/釜/作業台 Lv2、研究36(mg15) | tier2杖（→5×5）、炉Lv2＋釜Lv2で **t1変換が純増化**、MegaThunder、受動ダメバフ |
| 3 | **d20**（19wv, mhp6%）| HP430/Atk82/Def24、GigaFire+GigaThunder+Flame+Bolt+受動、`acc_phoenix_charm`（20%/wave 回復）| Giga級魔法、研究70(mg18) |
| 4 | **d20**（19wv, mhp3%）| HP500/Atk121/Def26、`armor_plate`(Hp+55)、作業台Lv3、研究机Lv2 | **プラトー**（HP+70/Atk+40 でも壁が動かず）|
| 5 | **d25**（24wv, mhp1%）| HP515/Atk193/Def30/Spd9/Luc11、mana276/19.75、grid 25/25、MegaFlame+MegaStorm(AoE)+GigaThunder+受動6、`acc_font_of_life`(hpR/s6) | **プラトー打破**。研究をマナ/速さ/運のゲートノード経由で開通 → Atk+72・12種の魔法（Mega AoE 群）・受動バフ6種 |
| 6 | **d28**（27wv, mhp4%）| HP555/Atk318/Def43/Spd11/Luc13、mana406/25.6、grid **6×6**、MegaFlame+MegaStorm+MegaDarkness+GigaFire+受動6、`wand_arch`(Atk+11)、研究196(mg32)、依頼19件完了 | tier3杖（→6×6）、orca 依頼チェーン開通（stP大量）|

**壁の推移: 11 → 16 → 20 → 20 → 25 → 28**

## 改善の判定

このセッションは改善（コード変更）を挟まない実地取り。改善ループ「最終到達点」（README）に対する裏取りとして:

- ⭕ **cold-start 成長ループが機能**。資源注入なし・6サイクルで d11→d28。1サイクルあたり平均 +2.8 深度、プラトー打破後は +3〜4。過去のモードB（`再検証9` で d10→14→18→20→21、`再検証18`）と比べても trajectory は上振れ〜同等で、S2〜S16 の調整が cold-start に効いている。
- ⭕ **cold-start の結晶詰み（D1）は再発せず**。貪欲でも詰まなかった。序盤の中結晶（dag_1/orca_1/dag_grind/glen_1 の報酬）→ glen で崩す → 炉/作業台/机/釜 Lv1 → 見習いの杖、までスムーズ。錬金釜Lv1 の t1 変換は回さず、炉Lv2＋釜Lv2 になってから溜めた素材を一気に結晶化（1回で S +200 超が2度）。
- ⭕ **「まず杖」導線（D2）**は機能。dag 連鎖先頭 `dag_wand`（CraftGear/Wand）が杖製作を後押しし、サイクル1 で 4×4 に到達。
- ⭕ **フェイルステート健在**。全6 push が decisive death（mhp 1〜6%）。不死バグの兆候なし。`WaveDamageCapFraction=0.85`＋`WaveClampMinStartFraction=0.55`＋`WaveHealCapFraction=0.5` が効いていて、強サステイン（font hpR/s6）でも延命バフ化しない。
- 🔺 **d20 プラトー（サイクル3→4）**。HP430→500・Atk82→121・`armor_plate`・`phoenix_charm` を足しても壁が d20 で動かなかった。原因は下記 O1。サイクル5 で「研究ツリーをマナ/速さ/運の小ノード経由で強引に開通」して突破。人力なら踏まないが、**貪欲エージェント（モードA/ハーネス）は Mana/Spd/Luc スキップ規則のせいでこのプラトーに嵌まる可能性**がある。

## 新規の構造的所見

### バランス / ツリー構造

- **O1（要注意）: 研究ツリーの深部フロンティアが Atk 一色。** 中盤（割当 ~70ノード）以降、`CanAllocate` で開くのは `BuildAtkColumns`（Atk 小ノード100本）ばかりで、Def/Hp ノードがフロンティアにほぼ出てこない。サイクル4→6 の research 割当は Atk +40 / Def +1 / Hp +1 という比率になった。結果、研究に投資するほどグラスキャノン化が進む（最終 HP555 / Atk318 / Def43）。深部の壁は一貫して「被弾の累積 > 回復の累積」＝Def/HP不足なのに、研究では埋められない。
  - モードA の「Atk が伸びない（D6）」所見と**逆の症状**に見えるが、根は同じ「Atk 小ノードが数で偏っている」。Def/Hp 小ノードを外周カラムにも散らすか、`BuildAtkColumns` の一部を Def/Hp カラムに置換すると、後半の研究投資が防御に回せる。
- **O2: マナ/速さ/運の小ノードが Atk/Def/Hp 深部への唯一のゲート。** 純粋な木なので、`nsp_*` や `node_*_e/b` の Mana/Spd/Luc を通らないと奥の主ステ・魔法（Mega AoE 群、Giga）に届かない。「Mana/Spd/Luc はスキップ」というモードA/手動の定石が、実は**魔法解放と主ステ拡張を止めている**。サイクル5 でここを開けた瞬間に Atk+72・魔法12種・受動バフ6種が一気に開き、d20→d25。ツリーを読むうえで重要。実際 Spd/Luc は腐っていない（Spd11 で発動 -12%、Luc13 で会心 +8%）。
- **O3: フラグメントの属性が厳格。** `MaterialCatalog.Key = type:attribute:name`。作業台Lv2・`wand_oak`・`wand_arch` は **`ElementFragment:Fire` 限定**で、ダンジョン産の Dark/Wind フラグメントでは支払えない。序盤は炎フラグの入手経路が「リーゼ 中×4→炎欠片×2」しか実質なく、ここで中結晶を食う。意図的なら OK だが、cold-start の炎フラグ faucet が細い自覚は要る。
- **O4: 施設 Lv2/Lv3 は特定 tier2 モンスター素材ゲート。** 炉Lv2=鉄の兜(d11)、釜Lv2=剛毛(d8)、作業台Lv2=錆びた短剣(d9)、机Lv2=古びた骨(d15/スケルトン)、作業台Lv3=竜人の鱗×2＋Dark欠片×3、机Lv3=古びた骨×2＋Light欠片×2。**farm 深度が施設進行を律速する**（d6 farm では Lv2 に上がれない）。研究机 Lv2 が古びた骨（d15）待ちなのは、序盤に研究コスト減が効かず地味に重い。
- **O5: orca の ReachDepth チェーンが DeliverItems で分断。** `orca_3`（スライムゼリー×3＋ゴブリンの牙×3＋大ネズミの尾×3＋番人の樹皮×3）が挟まっていて、深度は満たしていても浅層ドロップ（スライム/大ネズミ）を切らすと止まる。番人の樹皮（森の番人 d19）が本命ゲートのはずが、実際は**スライムゼリー1個足りずに d5 farm を挟む**という妙な足止めになった。keep 数のミスとも言えるが、深部を走っている人ほど浅層 t1 を持っていない。

### バグ

- なし。例外・error・warning ともゼロ（`read_console` types=[error,warning] で 0 件）。ソフトロック・進行不能なし。

### 導線 / UX（バグではない）

- 施設 `CanAdvance` が false のとき理由が返らない（O3 の炎フラグ不足を、`have` を手で数えて初めて特定できた）。ハブUIに「炎エレメントの欠片が2/3」のような不足表示があると、モードB 実行者も実プレイヤーも助かる。
- グリッドリサイズは Build へのフェーズ遷移でしか走らない。杖を作った直後は `grid.ApplyWandTierSize()` 明示が要る（既知）。

## 安定性

| 指標 | 値 |
|---|---|
| 出撃回数 | 約 24（farm 18 / push 6）|
| 総ウェーブ数 | 約 230 |
| 例外 | 0 |
| error / warning ログ | 0 |
| ソフトロック・詰み | 0 |
| 不死・フェイルステート消失 | 0（全 push が mhp 1〜6% で decisive death）|

## 次にやるなら（優先度順）

1. **O1: 研究ツリー深部に Def/Hp を戻す。** `ResearchGraph.BuildAtkColumns` の一部（例: 5本中2本）を Def/Hp カラムに置換、または外周 spoke の偶数=主ステ枠に Def/Hp を増やす。後半の研究投資が防御に回れば d20 以降の「累積被弾」壁が動く。リスク中（`ResearchGraphTests.NoTwoNodesOverlap` の再調整が要る）。
2. **O2 を逆手に取る。** モードA/ハーネスの経済エージェントに「Mana/Spd/Luc の小ノードも“奥へ進むため”に取る」ロジックを追加（現状の全スキップだと d20 プラトーに嵌まる）。あわせて手動手順書 `manual-run.md` の「ManaMax/ManaRegen/Luc はスキップ」を「奥のゲートは通す」に修正。
3. **O3: cold-start の炎フラグ faucet を1本足す。** 例: ダグの序盤オファーに「錆びた短剣×3 → 炎欠片×1」等（錆びた短剣は d9 で早い）。または作業台Lv2/`wand_oak` のフラグ要求を属性不問にする。
4. **O4: 研究机 Lv2 の古びた骨要求を、もう少し浅い t2（剛毛 d8 / 錆びた短剣 d9）に。** 序盤の研究コスト減が早く効く。
5. **O5: `orca_3` の浅層 t1 要求（スライムゼリー/大ネズミの尾）を落とすか、番人の樹皮のみに。** 深部プレイヤーが浅層 farm に戻される導線を消す。
6. ハブUI の施設アップグレード不足表示（何がいくつ足りないか）。

## 付録: 確認した数値とソース

- 開始値: HP130/Atk7/Def5/Spd5/Luc5、statP10、maxMana120/regen10、$80、grid 3×3、素材0、設備0、base-free 攻撃魔法10（`MagicSpawner.magicDataList` は 67、`DungeonManager.enemyPool` 40）。
- 入場料: 15G（所持金 <15G で無料、帰還時に戦利品から精算）。
- 施設 Lv1: 炉=小12+スライムゼリー4 / 作業台=小24+ゴブリンの牙4 / 机=小20+毒針4 / 釜=中2+大ネズミの尾4 / サークル=小30+スライムゼリー6。
- 施設 Lv2: 炉=中5+鉄の兜2 / 作業台=中4+炎欠片2+錆びた短剣2 / 机=中6+光欠片2+古びた骨2 / 釜=中3+剛毛2。
- 施設 Lv3: 作業台=大2+闇欠片3+竜人の鱗2 / 机=大? / 釜=大1+風欠片3+魔石の欠片3 / 炉=大1+中12+巨神の核1。
- 燃料値: 小=+1 / 中=+10（大=+100 と推定）。`FurnaceFuelPerAction`=1。`FurnaceSlots` Lv1=2(=cap200)/Lv2=3(=cap300)。
- 錬金釜産出（Lv2, `CauldronYieldMult`=1.4）: t1→小2〜3、t2→中1（属性 t2+ は対応属性の欠片）、t3→中2。`CauldronMaxTier` Lv1=1/Lv2=3/Lv3=5。
- ダメージ式（`BattleFormula`）: 敵1発 = `max(1, round(敵Atk × atkMult) − round(実効Def))` ← **Def は素引き（毎ヒット固定減）**。魔法1発 = `max(1, round((魔法d + 実効Atk)×(1+ダメ%/100)×属性倍率) − 敵Def)`。
- `WaveDamageCapFraction`=0.85（`WaveClampMinStartFraction`=0.55 未満開始のウェーブには不適用）、`WaveHealCapFraction`=0.5、`CritMultiplier`=1.5、`SpdCastMultiplier` = 1−(spd−5)×0.02（下限0.5）、`LucCritChance` = clamp(luc−5,0,50)%。
- 深度スケーリング（`EndlessWaveGenerator.ScalingFor`、実測）: d20 で hpMult 3.04 / atkMult 2.315 / defMult 1.952。d13〜d24 は線形で滑らか（**d20 前後に段差なし**＝プラトーはツリー構造由来でスケーリング由来ではない）。同時出現 `EnemyCountFor`: d16 まで3体、d17 から4体。抽選窓 `[Min,Max)`: d20 で [4,21)。
- 敵デビュー（`EnemyDataGenerator.OrderWeakToStrong` index ≒ 窓開放深度）: 8荒れイノシシ/剛毛, 9コボルト/錆びた短剣, 10毒グモ/蜘蛛の糸・毒腺, 11ゴブリン戦士/鉄の兜, 12ハーピー/風切羽, 13リザードマン/竜人の鱗, 15スケルトン/古びた骨, 19森の番人/番人の樹皮・古木の芯, 20洞窟トロル/トロルの生皮, 22オーガ/オーガの牙。
- gear（`GearCatalog`、実測）: `wand_apprentice` t1 Atk+3（小15+ゴブリンの牙3）→4×4 / `wand_oak` t2 Atk+6（中6+炎欠片2）→5×5 / `wand_arch` t3 Atk+11（大1+炎欠片3）→6×6。`armor_cloth` t1 Hp+20 / `armor_plate` t3 Hp+55（大1+竜人の鱗3）。`acc_regen_torc` t2 Hp+15・hpR/s3.5 / `acc_phoenix_charm` t3 Hp+30・hpW%20% / `acc_font_of_life` t3 Hp+25・hpR/s6。
- 研究机 Lv2: `ResearchCostMult`=0.78、`ResearchBonusMult`=1.3。
- 最終スナップショット: HP555/Atk318/Def43/Spd11/Luc13、mana406/25.6、hpR/s6、$175、S265、研究196(魔法32/67)、施設 炉2/作業台3/机2/釜2/サークル0、grid 6×6、依頼19件完了、装備 wand_arch / armor_plate / acc_font_of_life（Acc2 未開放）。
