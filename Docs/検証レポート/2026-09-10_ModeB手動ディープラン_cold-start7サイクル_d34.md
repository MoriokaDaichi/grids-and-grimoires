# 再検証 — モードB 手動ディープラン（cold-start 7サイクル）

- 日付: 2026-09-10
- ブランチ: `master`（改善ループ サイクル1〜31 の調整をすべて含む作業ツリー）
- 手法: モードB（`execute_code` 駆動）。常駐オートドライバ（WaveClear で進む/脱出、Reward で `RewardScreen.OnReturn` をリフレクション実行）＋手動経済判断。
- 方針: **資源注入なし**。ゲームAPI（ステ振り分け／建造・強化／研究割当／錬金釜変換／交換／依頼受領・受取／出撃）のみ。farm は脱出前提で戦利品確保、push だけ死んでよい。
- timeScale 40〜42。
- 同日の別レポート `2026-09-10_ModeB手動ディープライン_cold-start6サイクル_d28.md`（d11→d28）に対する再取り。今回は研究割当スコアを Hp/Def 寄せに変え、cold-start をやり直した。

## EditMode

| | 結果 |
|---|---|
| 検証前 | **215 / 215** グリーン（`GridsAndGrimoires.EditModeTests`）|
| 検証後（stop → RequestScriptReload → 再実行）| **215 / 215** グリーン（不変）|

## 到達深度の推移

| サイクル | 壁（push 終了深度）| 終了時キット | このサイクルで解放したもの |
|---|---|---|---|
| 1 | **d9**（retreat, mhp5%）| HP235/Atk13/Def9、grid **4×4**、base魔法5（Flame/Bolt/Fire/Thunder/Wind）、`wand_apprentice`、全4設備 Lv1、研究13(mg10) | 見習いの杖（→4×4）、炉/机/釜/作業台 Lv1 |
| 2 | **d12**（death, mhp1%）| HP360/Atk25/Def11、grid **5×5**、GigaFire+MegaFire+MegaThunder、`wand_oak`・`armor_cloth`・`acc_mending_band`、作業台Lv2＋釜Lv2、研究27(mg12) | tier2杖（→5×5）、作業台Lv2＋釜Lv2、MegaFire、GigaFire、`atr_Fire`（魔力増幅＝魔法ダメ%） |
| 3 | **d19**（death, mhp1%）| HP560/Atk27/Def30、grid 5×5、+MegaThunder、`acc_regen_torc`(hpR/s3.5)、炉Lv2、研究57(mg13) | 炉Lv2 → **t1 変換エンジン起動**（釜Lv2＋炉Lv2 で純増）、Vitality 扇（HP+165）、Thunder 攻撃特性（痛撃） |
| 4 | **d22**（death, mhp3%）| HP615/Atk56/Def31、grid **6×6**、`wand_arch`、`armor_plate`(Hp+55)、作業台Lv3＋机Lv2、研究77(mg13) | tier3杖（→6×6）、机Lv2、ManaRegen 扇（mana120→250） |
| 5 | **d31**（death, mhp6%）| HP985/Atk101/Def56/Spd15/Luc39、mana562、`armor_scale`(Hp+60)＋`acc_phoenix_charm`(Hp+30/wave20%)、研究194(mg29) | **タスク報酬カスケード**（$510・stP29・レシピ2種・大結晶多数）、117ノード研究パス、MegaFlame/MegaWind/MegaLight、Atk/Spd/Luc 受動バフ |
| 6 | **d31**（death, mhp0%）| HP1085/Atk110/Def61、研究223(mg37)、+GigaWind/MegaStorm/MegaDarkness/MegaDark | +29ノード、余剰$で大結晶を購入して研究に回す |
| 7 | **d34**（death, mhp1%）| HP1085/Atk113/Def97/Spd17/Luc44、mana588、釜Lv3、研究238(mg41/67)、BuffDamage 受動 Lv1/2 | 釜Lv3（CauldronMaxTier5）、BuffDamagePassive Lv1/2（魔法ダメ%）。**研究フロンティア枯渇**（`CanAllocate` が全 false になる） |

**壁の推移: 9 → 12 → 19 → 22 → 31 → 31 → 34**

## 改善の判定

このセッションは改善（コード変更）を挟まない実地取り。改善ループ「最終到達点」（README）と前回モードB（同日 d28、`再検証18` の d20 台）に対する裏取りとして:

- ⭕ **cold-start 成長ループが機能。資源注入なし・7サイクルで d9→d34**。前回モードB（d11→d28、6サイクル）を trajectory・絶対値ともに上回る。差は主に (a) 研究割当を Hp/Def 寄せにした、(b) farm 深度を上げて t2/t3 パーツを厚めに溜め transmute パスを大きくした、の2点。
- ⭕ **cold-start の結晶詰み（D1）は再発せず**。序盤は中結晶タスク報酬（`dag_wand`/`dag_grind`/`orca_2` 系）→ glen で小に崩す → 炉/机/釜/作業台 Lv1 → 見習いの杖、までスムーズ。錬金釜Lv1 の t1 変換は回さず、**炉Lv2＋釜Lv2 到達後**に溜めた素材を一気に結晶化（1パスで小 +360／+200 超が複数回）。
- ⭕ **「まず杖」導線（D2）**は機能。`dag_wand`（CraftGear/Wand）がサイクル1 で 4×4 到達を後押し。
- ⭕ **フェイルステート健在**。全7 push が decisive death（mhp 0〜6%）。不死・延命バフ化の兆候なし。`WaveDamageCapFraction`＋`WaveHealCapFraction` が効いていて、`acc_phoenix_charm`（wave 20% 回復）でも壁で普通に死ぬ。
- ⭕ **例外・error・warning ゼロ**（`read_console` types=[error,warning] で 0 件、全7サイクル通して）。ソフトロック・進行不能なし。
- 🔺 **d31 プラトー（サイクル5→6）**。HP985→1085・Atk101→110・魔法追加でも壁が d31 で二度動かず。サイクル7 で釜Lv3＋BuffDamage 受動＋Def をキャップ近くまで積んで d34 へ。原因は下記 O6（研究フロンティア枯渇）＋ N1（HP 手動キャップ）の合わせ技。

## 新規の構造的所見

### バグ（コード）

- **N1（要修正）: 手動ステータス振り分けの HP/Atk が合計値ハードキャップで無言死蔵。**
  [PlayerStatus.cs:317-338](../../Assets/Script/PlayerStatus.cs#L317-L338) の `AddStat` は `case "HP": if (hp < HP_MAX)` / `case "Atk": if (atk < OTHER_MAX)` で、**`HP_MAX=1000` / `OTHER_MAX=100`**（[L63-64](../../Assets/Script/PlayerStatus.cs#L63-L64)）を超えていると **`statsPoint` を消費せず、加算もしない**。判定対象は研究・装備込みの**合計値**（`hp` / `atk` フィールドそのもの。研究は `ApplyResearchDelta`、装備は `ApplyGearDelta` 経由でキャップ無視のまま合計を押し上げる）。
  - 深部ランでは研究の Hp ノード（Vitality 扇＋属性ライン `_b`）だけで HP が 1000 を超え、`atr`/Atk カラムで Atk が 100 を超える。今回はサイクル5 時点で HP1085 / Atk101。以降、**タスク報酬でまとめて入る大量ステP（1度に stP29、`orca` の 大結晶→stP 交換で更に +30 相当）の HP/Atk 割り当て分が全部空振り**。`AddStatsPoint` は上限なく受けるので "詰み" にはならないが、報酬が黙って蒸発する。
  - Def/Spd/Luc は 100 まで有効（今回 Def は 97 まで積めた＝キャップ張り付き寸前）。
  - ライブ確認: `statsPoint=5` の状態で `AddStat("HP")`／`AddStat("Atk")` を叩いても `statsPoint` も `hp`/`atk` も不変、`AddStat("Def")` は `def 94→95` で `statsPoint 5→4`。
  - 直し方の案: (a) キャップを撤廃 or 大幅緩和（HP 3000／other 300 程度）、(b) キャップ判定を「手動加算分（`manualHp`/`manualAtk`）」に対して行う、(c) キャップ到達時は `statsPoint` を消費しない代わりに UI で「これ以上振れない」を明示。少なくとも**消費せず死蔵**（=見た目上ポイントが増え続けるのに反映されない）は避けたい。

### バランス / ツリー構造

- **O6（今回の主プラトー要因）: 研究フロンティアが res≈238/277 で枯れる。** 研究机 Lv2 のまま（Lv3 は `世界樹の若枝×2` ＝深層ドロップ待ち）だと、サイクル7 で `foreach ResearchGraph.Nodes → CanAllocate` が**全部 false** になり、小結晶 1343 個が使い道なく余った。res238（魔法41/67・特性・扇・カラムはほぼ取り切り）から先は、机Lv3 か、Giga/上位魔法ノードの `requiredMaterials`（未所持の上位フラグ/エレメント）ゲートで止まる。研究が「投資したくても投資できない」状態になり、火力/耐久の伸びが gear と釜Lv3 頼みになる。
- **O1（再現）: 研究深部フロンティアが Atk/mana 寄り。** Hp/Def 寄せのスコアリングにしても、Vitality 扇と属性 `_b`（Hp）を取り切ると frontier が Atk カラム・ManaRegen/ManaMax 扇しか出さなくなる。最終 HP1085 / Atk113 / **Def97（キャップ）** / mana588。深部の壁は一貫して「累積被弾 > 回復」なのに、研究では Def/Hp をこれ以上伸ばせない（N1 で手動 HP も無理）。
- **O2（再確認・重要）: マナ/速さ/運の小ノードが上位魔法・主ステ深部への唯一のゲート。** 純粋な木なので `nsp_*`/`node_*_e/b` の Mana/Spd/Luc を通らないと Mega AoE 群・Giga・受動バフに届かない。サイクル5 でここを開けた瞬間に 117ノード（Atk+45・魔法12種・受動バフ多数）が一気に開き d22→d31。**「Mana/Spd/Luc はスキップ」の定石は捨てるべき**。今回のスコアリングは Mana/Spd/Luc も 50 点で取りに行かせた（Hp74/Def72/Atk58 の下）。手順書 `manual-run.md` の当該行は要修正（前回レポートでも指摘済み）。
- **O7（新規・軽微）: 属性 t2 パーツの transmute が属性フラグ faucet として非常に太い。** 竜人の鱗（Fire）・風切羽（Wind）等を釜Lv2+ で回すと対応属性フラグが大量産出（今回 Fire 38・Wind 55 が余った）。前回レポート O3（cold-start の炎フラグ faucet が細い）は、**釜Lv2 到達後は解消**して見える。逆に言うと釜Lv2 前（＝作業台Lv2 の `炎欠片×2` ゲート）だけが細い。
- **O5（前回指摘）は今回は非発生。** `orca_3` の浅層 t1 要求（スライムゼリー/大ネズミの尾/ゴブリンの牙 ×3＋番人の樹皮×3）は、farm が浅層も踏む運用＋keep 数管理で普通に通った。深部だけを直行する人だと踏む余地は残る。

### 導線 / UX（バグではない）

- 施設 `CanAdvance`／`CanCraft`／research `CanAllocate` が false のとき理由が返らない。O6 の「フロンティア枯渇」も、全ノードを走査して初めて分かった。ハブUI／研究画面に「次に開くには〈研究机Lv3〉／〈○○のフラグ〉が要る」表示があると、モードB 実行者も実プレイヤーも助かる。
- `AddStatsPoint` が上限なく受ける一方 `AddStat("HP"/"Atk")` がキャップで無反応（N1）。ステ振りUIで + ボタンが押せてしまう／押しても増えないなら、UX 的にも要ケア。

## 安定性

| 指標 | 値 |
|---|---|
| 出撃回数 | 約 24（farm 17 / push 7）|
| 総ウェーブ数 | 約 230 |
| 例外 | 0 |
| error / warning ログ | 0 |
| ソフトロック・詰み | 0 |
| 不死・フェイルステート消失 | 0（全 7 push が mhp 0〜6% で decisive death）|

## 次にやるなら（優先度順）

1. **N1 を直す。** 手動ステの HP/Atk キャップ（1000／100）を、深部ランの研究到達値を踏まえて緩和 or 手動加算分基準に変更 or 少なくとも「消費せず死蔵」を止める。タスク報酬のステP（`rewardStatPoints`・`orca` の 大結晶→stP 交換）が中盤以降ほぼ HP/Atk に振れない現状は、報酬設計が空振りしている。
2. **O6: 研究フロンティアの "枯れ" を無くす。** 研究机 Lv3 のゲート素材（`世界樹の若枝` 等）を浅めの t2 に、または上位魔法ノードの `requiredMaterials` を釜/トレーダーで賄える範囲に。res238 で頭打ちだと、深部で研究に投資しても壁（累積被弾）が動かない。
3. **O1: 研究深部に Def/Hp を戻す。** `ResearchGraph.BuildAtkColumns` の一部を Def/Hp カラムに置換、または外周 spoke 偶数枠に Def/Hp を増やす。frontier が Atk/mana 一色だと、Hp/Def 寄せのスコアリングでも防御が伸ばせない。（`ResearchGraphTests.NoTwoNodesOverlap` の再調整が要る）
4. **O2: `manual-run.md` と モードA/ハーネスの経済エージェントを「Mana/Spd/Luc の小ノードも奥へ進むゲートとして取る」に更新。** 全スキップだと d20〜31 のプラトーに嵌まる。
5. **O7 を踏まえ、釜Lv2 前の炎フラグ faucet（作業台Lv2 / `wand_oak` の `炎欠片×2`）だけ緩める** or 属性不問に。釜Lv2 以降はフラグは余るので、細いのは本当に序盤の一点だけ。
6. ハブUI／研究画面の「次に開くのに何が足りないか」表示。

## 付録: 確認した数値とソース

- 開始値: HP130/Atk7/Def5/Spd5/Luc5、statP10、maxMana120/regen10、$80、grid 3×3、素材0、設備0、base-free 攻撃魔法10（`MagicSpawner.magicDataList`=67、`DungeonManager.enemyPool`=40）。
- 入場料 15G（所持金<15Gで無料、帰還時に戦利品から精算）。
- 手動ステ上限: `HP_MAX=1000` / `OTHER_MAX=100`（[PlayerStatus.cs:63-64](../../Assets/Script/PlayerStatus.cs#L63-L64)）。`AddStat` は合計値がこれ以上だと `statsPoint` 未消費で no-op（[L317-338](../../Assets/Script/PlayerStatus.cs#L317-L338)）。HP は +10/pt、他は +1/pt。
- 施設 Lv1: 炉=小12+スライムゼリー4 / 作業台=小24+ゴブリンの牙4 / 机=小20+毒針4 / 釜=中2+大ネズミの尾4。全設備アクション（建造以外＝強化/研究割当/製作/変換）に魔力炉の燃料 1/action。
- 施設 Lv2: 炉=中5+鉄の兜2(d11) / 作業台=中4+炎欠片2+錆びた短剣2(d9) / 机=中6+光欠片2+古びた骨2(d15) / 釜=中3+剛毛2(d8)。
- 施設 Lv3: 作業台=大2+闇欠片3+竜人の鱗2(d13) / 釜=大1+風欠片3+魔石の欠片3(d~24) / 机=大2+中20+世界樹の若枝2(深層) / 炉=大1+中12+巨神の核1(深層)。
- transmute 産出（釜Lv2, `CauldronYieldMult`=1.4）: t1→小2〜3、t2→中1（属性 t2+ は対応属性フラグ）、t3→中2。`CauldronMaxTier` Lv1=1/Lv2=3/Lv3=5。`Transmute(part,times)` は **times 非依存で 1アクション＝燃料 1**（＝まとめ変換が燃料効率よい）。
- gear（実測）: `wand_apprentice` t1 Atk+3（小15+ゴブリンの牙3）→4×4 / `wand_oak` t2 Atk+6（中6+炎欠片2）→5×5 / `wand_arch` t3 Atk+11（大1+**闇欠片3**）→6×6。`armor_cloth` t1 Hp+20 / `armor_plate` t3 Hp+55（大1+竜人の鱗3）/ `armor_scale` t3 Hp+60（大1+闇欠片3+竜人の鱗3、レシピは dag タスク報酬）。`acc_mending_band` t1 Hp+10・hpR/s1.5 / `acc_regen_torc` t2 Hp+15・hpR/s3.5（orca_2b 報酬で現物付与）/ `acc_phoenix_charm` t3 Hp+30・healPerWavePercent0.2。
- タスク報酬カスケード（サイクル5）: farm で累計撃破/最深深度が一気に閾値越え → `dag_2/3/5/7/grind`・`orca_3/4/5/5b/6/grind`・`glen_2` 等が同時 claim 可 → $+400 超・stP+29・レシピ `armor_scale`/`armor_aegis`・大結晶多数。
- 深度スケーリング（`EndlessWaveGenerator`）: `ScalingTaperKneeDepth`=10 以降は勾配 0.4 で寝る、`DefMultCap`=2.5。壁は d31→d34 とも「累積被弾 > 回復」で mhp 0〜1% の decisive death。
- プレイヤー最終派生ステ（研究特性）: spellPowerPercent 36 / armorPierce 3 / percentDamageReduction 0.24 / thornsPercent 0.6 / lastStandReduction 0.15 / waveBarrierPercent 0.08 / critMultBonus 0.5 / castHastePercent 0.1 / executeBonusPercent 0.2。
- 最終スナップショット（サイクル7 終了）: HP1085/Atk113/Def97/Spd17/Luc44、mana588/43.2、healPerWavePercent0.2、$75、S143、研究238/277（魔法41/67）、施設 炉2/作業台3/机2/釜3/サークル0、grid 6×6、依頼19/41 完了、装備 wand_arch / armor_scale / acc_phoenix_charm（Acc2 未開放）。
