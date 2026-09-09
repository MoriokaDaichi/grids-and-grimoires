# 開発ログ / セッション引き継ぎ

Claude Code セッションでの実装内容の記録。企画・数値の正本は
[Grids_and_Grimoires_開発資料.md](Grids_and_Grimoires_開発資料.md)、設計判断の経緯は
auto memory `game_design_grids_and_grimoires`（`~/.claude/projects/.../memory/`）にも
フェーズ単位で残っている。ここはコード寄りの実装サマリ。

---

## 現在の状態（2026-09-09 時点）

- **ブランチ**: `master`。`feat/battle-ui-core-loop` は PR #1 マージ済み（`7ff60e3`）で残置。
- **EditMode テスト**: 180/180 グリーン（`GridsAndGrimoires.EditModeTests`）。
- **Unity**: 6000.3.9f1 / URL シーン `Assets/Scenes/SampleScene.unity`。
- アセンブリ分割済み: `GridsAndGrimoires.Runtime`（Assets/Script）/ `.Editor`（Assets/Editor）/
  `.EditModeTests`（Assets/Tests/EditMode）。
- **数値は全て仮**（企画書 7 章の敵表・設備コスト等は未記載）。

### テストの回し方
- Editor 起動中: MCP `unity-mcp` の `run_tests`（EditMode, assembly `GridsAndGrimoires.EditModeTests`）。
- Editor 停止時: `Tools/unity-tests.ps1`（`Tools/unity-compile.ps1` はコンパイルチェックのみ）。
- **テストは Play モード中は起動不可**（先に停止する）。

### マスターデータ再生成（Unity メニュー、Edit モードで実行）
- `Grimoire > Generate Enemy Data` — 敵 40 体アセット（`Assets/EnemyData/`）
- `Grimoire > Generate Master Magic Data` — 魔法 67 種（`Assets/MasicData/`）
- `Grimoire > Generate Dungeon` — シーンの `DungeonManager.enemyPool` を 40 体に更新（実行後シーン保存）
- `Grimoire > Build Battle UI` — 戦闘 / ハイドアウト / 研究 / トレードの画面ルートを構築（Edit モードのみ）
- `Grimoire > Wipe Save` — `gg_save.json` 削除

---

## このセッションで実装したこと（新しい順）

### -11. 深部（d16→d25）設計：投資ラダー＋無限リソースループ（2026-09-09 その11）
`Docs/検証レポート/2026-09-09_深部設計_診断.md`。診断: 全投資（Atk238・研究251ノード）でも壁 d20＝
伸びしろが無い（投資先が Atk/HP しか無く tier3 装備・設備Lv3 が実質未到達、原因は同時4体×単体火力偏重）。

- **研究ツリーに Atk 小ノードの放射カラム 100本**（`ResearchGraph.BuildAtkColumns`）。小結晶コストで ring4→7 を
  直列。研究ノード 167→267。深部スケーリングに対する Atk の投資先を大幅に増やす。
- **A: 深部到達＝進行報酬**（`TraderCatalog`）: オルカの深度タスクを d15/d18/d20/d22/d25/d30 と刻み、
  恒久ステP を 5/5/6/8/8/12。「d15 に届けば次の数深度ぶんの体力が手に入る」階段。
- **B: tier3 装備・設備Lv3 を d13〜15 帯の素材で**（`HideoutCatalog`/`GearCatalog`）: 作業台Lv3 の建材
  `竜のうろこ`(d37)→`竜人の鱗`(d13)。armor_plate も同様。新レシピ 2種（竜騎の杖 Atk+16／竜鱗の胸甲 Hp+60、
  竜人の鱗製、オルカ d18/d22 で解禁）。
  → 単発「強キット注入」壁: d20 → **d23**（投資ラダー d8→d15→d20→d23 が機能）。
- **無限リソースループ**（ユーザー方針「中層で素材→大結晶をステに変換をくりかえし→深層／討伐タスク無限」）:
  - **大結晶→ステP 変換オファー**（オルカ・常時＝繰り返し可）: 大×2→+2 ／ ×5→+6 ／ 中×12→+1。
  - **繰り返し討伐タスク** `TraderTask.repeatable`（`TaskRules`/`TraderCatalog`/`TradeManager`/`SaveData`）:
    達成しても completedTaskIds に入らず、受取ごとに「前回受取からの累計撃破数」で進捗リセット。
    `dag_grind`（25体ごと→中×3＋ステP+1）／`orca_grind`（40体ごと→大×1＋ステP+2、深度15 で解放）。
    `SaveData.repeatableTaskBaselines`/`repeatableTaskClaims`（round-trip テスト）。
  - Mode A ×14: farm×3→push **8→11→13→…→15→14→15→17**（周を重ねるほど深くなる。Atk 21→109、
    研究ステ64、タスク受領19、14周で d8→d17 まだ climbing）。＝エンドレスの成長ループが成立。
- **C: 深部の同時出現数テーパー**（`EndlessWaveGenerator`）: 深部の壁の正体は「同時4体×単体火力偏重の
  throughput」で Atk/HP 投資では解けない。`EnemyCountTaperKneeDepth`(=15) より先は実効深度を勾配0.5 で
  寝かせ、d16〜28 の同時数を 4 体で保つ（素の式なら d21で5・d26で6）。膝まで（浅〜中盤）は完全不変。
  → 深部フル投資（A+B・15周相当）の壁: d23 → **d26**。「現実的な周回数（15周）で d25」を達成。
- **ダグの依頼チェーン並べ替え**（`TraderCatalog`）: 『森の番人 ×3』（d19）が dag_2 直後にあり中盤 faucet が
  壁の先で全ロックされていたのを、ID 据え置きでアクセス順（dag_4→5→7→3→6→8/9）に。
- EditMode 196 → 201 グリーン。ハーネス（`PlaytestAutopilot`）も研究割当・farm 周・杖導線・中結晶温存・
  大結晶→ステP・ステP 毎周消費 で人力プレイ級に（ただし B＝作業台Lv2/Lv3 は貪欲では未到達＝Mode A は d18止まり）。

**深部設計の到達点**: 投資ラダー cold-start素 d8 → 中盤 d15 → 研究フル d20 → 深部フル投資＋C **d26**。
無限成長ループ（繰り返し討伐タスク＋大結晶→ステP変換）で周回するほど深くなる。フェイルステートは
全投資レベルで decisive death（immortality を R1/R4 で2回検出・修正済み）。

### -10. 改善ループ続行：モードB手動ディープラン → R1/R2/D6 対応（2026-09-09 その10）
`gg-playtest` スキルのモードB（`execute_code` 手動ディープラン）で「-8.」の改善を再検証しながらの続行分。
詳細は `Docs/検証レポート/2026-09-09_再検証4_*.md` `_再検証5_*.md` と `_改善ループ_進捗.md`（サイクル3〜6）。

- **R1（フェイルステート消失）**（`BattleFormula` / `PlayerStatus` / `EndlessWaveGenerator`）:
  モードB 1周目 push で「最弱キット（3×3・base2枚・杖なし・研究ゼロ）が深度18で無傷」を検出。
  `D5(0.6クランプ)＋D6a(0.25テーパー)＋サステイン回復` が乗算で深部の被弾圧力を消していた。
  対応: `WaveDamageCapFraction` 0.6→**0.85**、クランプを **ウェーブ開始HP≥`WaveClampMinStartFraction`(0.55)**
  のときだけ有効化（`PlayerStatus.waveStartHpFraction` を `BeginWave` で記録）、`AtkScalingTaperSlope` 0.25→**0.35**。
  再測: 最弱キット push が深度8で戦闘不能＝天井が戻った。テスト +2。
- **R2（cold-start の燃料デッドロック）**（`HideoutCatalog` / `HideoutManager`）:
  モードBで、魔力炉＋錬金釜＋作業台＋アクセ 建造直後に $30／小結晶20／燃料0 になり
  craft/research/transmute/upgrade が全部止まる（貪欲では詰み）ことを検出。`FurnaceBuildBonusFuel`(=25)
  を新設し `HideoutManager.Advance` で魔力炉 Lv0→Lv1 のとき `furnaceFuel` に加算。テスト +1。
- **D6（Atk が伸びない）**（`ResearchGraph`）: Atk 小ノードが Fire 枝＋Fortune 扇の一部しか小結晶で辿れず
  （他は Luc 経由か中結晶ゲート）、min-max でも実質4本で頭打ち（3〜5周で +2〜3）。
  `BuildSpoke("Fortune", …, Luc, Atk, …)` → `(…, Atk, Luc, …)`（座標・ID 不変）＋ `Amount(Atk)` 1→**2**（ラベル「攻+2」）。
  検証（無限結晶・min-max）: 研究による Atk 上昇 +4 → +8。テスト +1。
- **R3（設備Lv2 が中盤で建たない）**（`HideoutCatalog`）: 錬金釜Lv2 step1 `M(3)+剛毛×2+蜘蛛の糸×2` →
  `M(3)+剛毛×2`（d10 debut の蜘蛛の糸を外す）。錬金釜Lv2 は tier2+ 変換→エレメント欠片→作業台Lv2/
  研究机Lv2 の起点。テスト +1。
- **R4（深部でフェイルステートがまた消える）**（`BattleFormula` / `PlayerStatus`）: HP320＋再生のトルク
  だと深部（d17+）で毎秒回復が被弾を上回り続け、d21 でも死なずタイムアウト（R1 の高投資版）。
  `WaveHealCapFraction`(=0.5)／`WaveHealCap(maxHp)` を新設。`PlayerStatus.regenHealedThisWave` で
  `RegenHealth` の1ウェーブ累計をクランプ（`BeginWave`/`BattleReset` でリセット）。ウェーブ突破時の
  `HealWaveTick` は別枠。被弾上限 0.85 > 回復上限 0.5 なので深部は必ず削り勝てる。テスト +2。
- **ハーネス改良**（`PlaytestAutopilot`）: (a) 経済パスに研究割当（魔法＋Atk>Def>Hp>Spd 貪欲、Luc/マナskip）
  ＝SKILL.md の「研究未割当＝深度が低く出る主因」を解消。(b) `farmRuns`(=2)/`shallowFarmCap`(=4)＝
  最初の2周は深度4で回して低tier素材（スライムゼリー等）を確実に集める＝経済初動の分散を抑える。
  (c) `perRunRealTimeout` 150→260。
- **D7（4×4 に Giga＋強AoE 同居不可）**（`HideoutCatalog`）: 作業台Lv2 step1 の中結晶 `M(7)→M(4)`
  （他 Lv2 に合わせる。tier2杖＝5×5 グリッドの導線）。モードB で強キット注入して push → **5×5 に
  GigaFire＋GigaThunder＋MegaFire＋MegaThunder＋Fire が同居**、深度15 で decisive death を確認
  （`2026-09-09_再検証6_*.md`）。
- **ハーネス改良（`PlaytestAutopilot`）**: (a) 研究割当、(b) farm 周（最初の3周は深度7で回して素材集め）、
  (c) 杖の上位持ち替え＋杖材料の絶対保護、(d) ブートストラップ後の中結晶温存（Lv2 ゲート用）。
  → Mode A の cold-start push 到達推移が **旧「全周8」→ 8→11→12→13→15→16** と人力 再検証3（11→…→16）に一致。
- **導線バグ（`TraderCatalog`）**: ダグの連鎖で『森の番人 ×3』（debut 深度19）が dag_2 直後にあり、
  その先の dag_4（ゴブリンの牙×12→中結晶×3）以降が壁の“先”で全ロックされていた。ID 据え置きで
  並びだけアクセス順に（dag_4→dag_5→dag_7→dag_3→dag_6→dag_8/9）。テスト +1。
- **検証まとめ**: フェイルステートは 最弱キット d8 / 中キット d12〜15 / 強キット d15 でいずれも decisive death。
  EditMode **180 → 196 グリーン**（回帰なし）。Mode A 十数回＋Mode B 複数（〜30 run）で例外・ソフトロック・
  コンソール error/warning ゼロ。Mode A cold-start push の到達推移は 旧「全周8」→ **8→12→13→16**（人力 再検証3 相当）。
  検証レポートは `Docs/検証レポート/_ループ_*.md` `_再検証4〜6_*.md` `_改善ループ_進捗.md`。

### -9. 通しプレイ検証を Claude Code スキル化（2026-09-09 その9）
`.claude/skills/gg-playtest/`（`SKILL.md` ＋ `references/manual-run.md`）。cold-start 通しプレイ検証の手順を
スキルに固めた。モードA＝自動ハーネス（`Grimoire > Run Playtest` / `PlaytestDriver.Begin`、速い・回帰用・
到達深度の絶対値は低く出る）、モードB＝`execute_code` 手動ディープラン（人力に近い経済判断、絶対深度用、
`EditorApplication.update` 常駐ドライバ＋sortie/poll＋`RewardScreen.OnReturn` 明示帰還のスニペット集）。
レポート命名（`_ループ_*.md` / `再検証N_*.md`）・既知の構造的所見（D1〜D7）・確認済み数値も同梱。コード変更なし。

### -8. 改善ループ（自動）：通しプレイ検証ハーネス新設＋深部Atk／cold-start結晶詰み／バースト即死（2026-09-09 その8）
「検証レポートの指摘を改善 → すぐ通しプレイで検証 → レポート → また改善」を自動で回すセッション。
`Docs/検証レポート/_改善ループ_進捗.md` に経過、通しプレイ結果は `Docs/検証レポート/_ループ_*.md`。

- **通しプレイ検証ハーネス（新規）** — 以降の改善サイクルの前提:
  - **[PlaytestAutopilot.cs](../Assets/Script/PlaytestAutopilot.cs)**（Runtime・再生時のみ生成、通常プレイでは不活性）:
    cold start から複数周を自動プレイ。ステP配分 → 攻撃魔法を greedy first-fit で配置 →
    出撃 → ウェーブ突破ごとに HP割合＋深度キャップで「進む/脱出」を自動判定 → 報酬帰還 →
    周のあいだに貪欲な経済（タスク受領／結晶の崩し／設備建造 魔力炉→錬金釜→作業台→研究机／
    燃料バッファ維持／杖・サステインアクセ製作／モンスター素材の変換〈未建造設備の建材は温存〉／
    Lv2強化）。到達深度・各深度の最小HP%・コンソール error/warning/exception を収集してレポート化。
  - **[PlaytestDriver.cs](../Assets/Editor/PlaytestDriver.cs)**（Editor・`[InitializeOnLoad]`）:
    メニュー `Grimoire > Run Playtest > Cold start x3 / x5`、または `PlaytestDriver.Begin(loops, depthCap)`。
    セーブ wipe → 再生モード → Autopilot 差し込み → `Docs/検証レポート/_ループ_<日時>.md` 書き出し → 再生停止。
  - ハーネスの限界: 経済は貪欲近似（人力の結晶配分・杖の手詰めより下手）／魔法配置は回転のみの
    first-fit／脱出判断はウェーブ間のみ／研究の小ノード割当は未駆動。**このため到達深度の絶対値は
    人力プレイより低く出る**（cold-start で深度8前後）。バグ検出とバランスの相対比較に使う。
- **D6-a（深部で敵Atkがプレイヤーを置き去りにする）**（`EndlessWaveGenerator`）:
  `AtkScalingTaperSlope`(=0.25) を新設。膝（`ScalingTaperKneeDepth`=10）までは HP/Def と同じ素の線形、
  膝から先だけ敵Atk倍率の伸びを一般テーパー(0.4)より寝かせる。再検証3でプレイヤーAtkが1周 10→17 しか
  伸びないのに敵Atkは +10%/実効深度で伸び続け、深部の「殲滅が遅い→被弾総量増／満タンから即死」を
  悪化させていた。浅〜中盤は不変。テスト +2。
- **D1（cold-start の結晶エンジンが建たない詰み）**（`HideoutCatalog`）:
  通しプレイで再現＝設備Lv1 の建材はほぼ**小結晶**（S12〜24）だが cold-start の結晶収入はタスク報酬の
  **中結晶**で、glen の「中→小」崩しを踏まないと錬金釜（＝素材→結晶のエンジン）が建たず貪欲プレイは
  詰む。錬金釜Lv1 の建材を `S(16)` → `M(2)` に変更（中結晶払い。M(2)=20 は S(16)=16 よりむしろ割高＝
  甘くはしない）。これで最初のタスク報酬で結晶エンジンを建てられる。テスト +1
  （`AlchemyCauldronLv1_IsPayableInMediumCrystals_ForColdStart`）。
- **D5／C2（ウェーブ内バースト即死）**（`BattleFormula` / `PlayerStatus` / `DungeonManager`）:
  「脱出」はウェーブ間でしか選べないので、満タン近くから1ウェーブで即死すると脱出判断が働かない。
  `BattleFormula.WaveDamageCap(maxHp)`＝`WaveDamageCapFraction`(=0.6)×maxHp を新設。
  `PlayerStatus` が現ウェーブの累計被ダメージ `damageThisWave` を持ち、`TakeDamage` でこの上限を
  超えるぶんを無効化する（`BeginWave` で `DungeonManager.AdvanceWave` からリセット）。
  100%未満のクランプなので、低HPで次ウェーブに入れば依然そのウェーブで倒れうる（死の無効化ではない）。
  `BattleManager`/`EnemyStatus` の再入ガードには触れない（`TakeDamage` 内で完結）。テスト +4。
- **D1 追い（orca_2 の結晶ゲート）**（`TraderCatalog`）: cold-start の壁は深度8〜9。中盤の結晶
  faucet `orca_2`（大結晶×2）の到達要件が深度10 だと壁の“先”で永遠に届かない。要件を 深度10→8 へ。
  テスト +1（`Orca2_CrystalFaucet_IsReachableAtOrBeforeColdStartWall`）。
- **通しプレイでの効果**（`_ループ_2026-09-09_*.md`。ハーネス自動プレイ・cold start ×5）:
  - v2baseline: 全周 深度8、5周中2周が戦闘不能、経済は 魔力炉＋研究机 止まり（錬金釜が建たず）。
  - 4改善後（D6a+D1+D5+orca2）: **5周すべて脱出（戦闘不能ゼロ）**、全4設備＋見習いの杖＋
    グリッド4×4＋魔法6枚、タスク受領 2→9、変換 0→79。d7 の最小HP% が 22〜58% → 43〜69% に改善、
    最大深度 9。**到達深度の絶対値（8前後）はハーネス限界**（研究小ノード割当なし・グリッド
    パッキングが下手）で頭打ち＝人力プレイの深度16 とは別物。相対比較として、cold-start の
    ブートストラップ不能・バースト即死・深部Atk置き去りはいずれも改善方向。
  - コンソール Exception/Error/Warning は全周ゼロ。
- EditMode 182 → 188 グリーン。

### -7. 再検証3のフィードバック反映：cold-start 結晶詰み／中結晶ゲート／5×5 導線／「まず杖」タスク（2026-09-09 その7）
`Docs/検証レポート/2026-09-09_再検証3_cold-start5周_総括.md` の「次にやるなら（優先度順）」から
D1・D3・D7 を実装（`HideoutCatalog` のみ）、加えて D2 の一次対応として「杖を作る」タスクを新設
（`TaskRules`/`TraderCatalog`/`TradeManager`）。テスト 172→180 グリーン。

- **D1（cold-start の結晶詰み）**: `FurnaceFuelPerAction(1)` を **2→1**。Lv1=2 だと錬金釜Lv1 の
  tier1 変換（`part + 燃料2 → 小結晶×2`）が完全に燃料中立で、有限な序盤タスク報酬の結晶を配分
  ミスすると回復不能に近かった（再検証3 は 1 トライ目が深度9でハードロック→wipe 再開）。Lv1=1 なら
  `part + 燃料1 → 小2` ＝**純増+1**になり、Lv1 設備だけで「素材→結晶」で経済を立て直せる。
  Lv2/Lv3 は元々 1 なので**中盤以降の推移は不変**（3周目の S277 ダンプ等は変わらない）。魔力炉の
  強化メリットはスロット（燃料バッファ上限）に残る。
  - テスト: `Furnace_SlotsGrow_FuelPerActionShrinks_WithLevel` → `Furnace_SlotsGrow_FuelPerActionStaysLean_WithLevel` に改称し、
    「Lv1 錬金釜×Lv1 魔力炉 で tier1 変換が純増」を `HideoutRules.Transmute` で直接検証。
    `HideoutRulesTests.Furnace_CapacityAndPowerGate` の燃料ゲート境界を 2→1 に更新。
- **D3（錬金釜Lv2 の中結晶ゲート）**: 錬金釜Lv2 step1 を `M(5)+剛毛×2+蜘蛛の糸×2` → **`M(3)`**。
  中結晶の中盤 faucet が無く、`M(5)` は「tier2素材を$146売る→$120で大結晶買う→glen で崩す」の
  細い一本道でしか賄えなかった（3周目の中盤ブレイクスルーがそこ頼み）。
  - テスト: `AlchemyCauldronLv2_MediumCrystalCost_StaysModest`（中結晶 ≤3）追加。
- **D7（tier2 杖＝5×5 グリッドの導線）**: 作業台Lv2 step1 の `竜人の鱗×2`（リザードマン debut 深度13
  ＝バースト即死帯）→ 同 tier2・Fire の **`錆びた短剣×2`**（コボルト debut 深度9）。5周とも作業台Lv2 が
  建たず 4×4 のままで、GigaFire＋強AoE が同居できない壁が解けなかった。
  - テスト: `WorkbenchLv2_DoesNotRequireBurstBandDebutPart`（竜人の鱗 を要求しない）追加。
- **D2（「まず杖」導線）一次対応 ― 新タスク種別 `CraftGear`**（`TaskRules`/`TraderCatalog`/`TradeManager`）:
  研究より先にグリッドを広げる動機づけが無く、cold-start で有限な結晶を研究へ全振り→杖が買えず
  3×3 のまま火力が伸びない詰み方をしていた（レポート D1/D2）。トレーダー依頼で明示的に誘導する。
  - `TraderTaskKind.CraftGear`：`targetGearSlot` の装備を `targetCount`（＝最低tier）以上で所持していれば達成。
    消費なし（杖は手元に残る）。`TaskProgress.ownedGearTier`（スロット→所持中の最上位tier）を追加、
    `TradeManager` が `HideoutManager.OwnedGear` から都度導出（在庫数と同じく非永続）。
  - ダグの連鎖の**先頭**に `dag_wand`「作業台で杖を打つ（見習いの杖でよい）」→ 報酬 **中結晶×3**
    （D3 の中盤 faucet も兼ねる）。作業台Lv1 が前提なので序盤の一里塚になる。既存 `dag_1` が
    `dag_wand` を requires するように連鎖が1段ずれる（旧セーブで `dag_1` 完了済みだと、`dag_wand`
    受取までその行が「未解放」表示になる軽微なズレ。受取で解消。検証は毎回 wipe 開始なので実害なし）。
  - `TradePanel` は種別非依存（`TaskProgressText`＋`RewardText`）なので UI 変更不要。
  - テスト: `TaskRulesTests` に `CraftGear_*` 4本、`TraderCatalogTests` に
    `EarlyOnboarding_HasCraftWandTask_AtHeadOfAChain` / `CraftGearTasks_HavePositiveMinTier_AndReward` を追加。
- **今回見送り（要判断・別セッション）**:
  - **D5（ウェーブ内バースト即死）**: 被弾総量クランプは `BattleManager`/`EnemyStatus` の再入ガード規律に
    触れるため、専用の設計・検証が要る。
  - **D6（Atk が伸びない）**: 研究ツリーの Atk 小ノードを中結晶ゲートより手前へ、は `ResearchGraph` の
    レイアウト変更（極座標テーブル・重なり不変条件テスト）を伴うため次回。
  - **D2 の残り**: 構築画面側での能動的な提示（グリッドが狭いときの誘導表示など）はまだ。
    次の cold start 5周で `dag_wand` 導線が効いているか再計測。

### -6. 再検証3（cold start 5周・改善「-5.」の再計測）― 検証のみ、コード変更なし（2026-09-09 その6）
`Docs/検証レポート/2026-09-09_再検証3_cold-start5周_総括.md`。DEV_LOG「-5.」で先送りした「項目4」を実施。
自動ドライバ（`EditorApplication.update` 常駐＋貪欲経済エージェント）で cold start から連続5周。

- **到達深度: 11 → 12 → 15 → 14 → 16**（前回 再検証2 の 11→13→13→15→16 とほぼ一致）。**深度20 には届かない**。
- **安定性: 例外・進行不能バグ・コンソール error/warning ゼロ**（約40出撃・400ウェーブ超）。EditMode 172/172（不変）。
- 「-5.」の判定: **R2/R3（Lv2ゲート浅層化）＝⭕**（錬金釜Lv2 が `蜘蛛の糸×2`＝深度10 で建つ）、**R4（tier2売却口）＝⭕**（中盤ブートストラップの原資）、**R5（序盤中結晶）＝🔺**（1回きりで faucet にならない）。
- 残る壁（未対応・要判断）:
  - **D1: cold-start の結晶詰み**。錬金釜Lv1 の tier1 変換が完全に燃料中立（`CauldronYieldMult(1)=1.0` / `FurnaceFuelPerAction(1)=2` → part+小2→小2）。有限な序盤タスク報酬の結晶を配分ミスすると回復不能に近い（1回目のトライは深度9でハードロック→wipe再開）。黒字化は錬金釜Lv2＋魔力炉Lv2 の両方が要る（Lv2炉で `-1燃料+小2`＝純増+1）。
  - **D2: 「まず杖」導線が無い**（既知・未実装）。研究より先に見習いの杖（小15＋ゴブリンの牙×3）を作れるかが分岐点。
  - **D3: 錬金釜Lv2 の `中結晶×5`** は依然ゲート（中結晶の中盤faucetが無く、「tier2素材を$146売る→$120で大結晶買う→glenで崩す」の細道頼み）。
  - **D5: ウェーブ内バースト即死**（深度13+、farm 2回没収）。**D6: Atk が伸びない**（5周で 10→17）。**D7: 4×4 に GigaFire＋強AoE が同居不可**（作業台Lv2＝竜人の鱗 d13 待ちで 5×5 に届かず）。
- 提案（レポート「次にやるなら」）: `CauldronYieldMult(1)` 1.0→1.5 か `FurnaceFuelPerAction(1)` 2→1 で Lv1変換を薄く黒字に／「まず杖」導線／orca_2 の要件を深度10→8／錬金釜Lv2 の中結晶 ×5→×3。

### -5. 再検証2（cold start 5周）のフィードバック反映：Lv2 設備ゲート／tier2 素材の出口／序盤の中結晶（2026-09-09 その5）
`Docs/検証レポート/2026-09-09_再検証2_{1〜5}周目*.md`（改善「-4.」反映後の cold start 5周）。
バグ・ソフトロックはゼロ。到達深度は 11→16 と投資に比例して伸び、狙い（火力ゲート・即死の緩和）は達成。
残る所見はいずれも中盤の導線で、総括の「次にやるなら（優先度順）」の 1〜3 を実装。テスト 169→172。

- **R2/R3：Lv2 設備の連鎖ゲートを浅層寄りに**（`HideoutCatalog`）。錬金釜Lv2 の建材『古木の芯』は
  森の番人（debut 深度19）ドロップで、深度16 の壁で詰まるプレイヤーは永遠に建てられず、中結晶
  （`M(n)` 要求）と tier2 素材の出口が両方閉じるデッドロックだった。5設備すべての Lv2(step1) で：
  - モンスター素材の必要個数を ×3→×2（farm 1回で賄える量に）
  - 錬金釜Lv2：`M(7)+剛毛×3+古木の芯×2` → `M(5)+剛毛×2+蜘蛛の糸×2`（古木の芯 d19 → 蜘蛛の糸 d10。
    R4 の塩漬け素材を消費させる置き換え）
  - 魔力炉Lv2 `M(6)→M(5)`、研究机Lv2 `M(8)+古びた骨×4 → M(6)+古びた骨×2`、
    作業台Lv2 `M(10)+竜人の鱗×3 → M(7)+竜人の鱗×2`、サークルLv2 `M(12)+風切羽×3 → M(8)+風切羽×2`
  - Lv3(step2) は据え置き（endgame。深層素材 OK）。tier 予算テスト（step1≤tier2）は不変で通過。
  - テスト追加：`Lv2BuildSteps_UseSmallMonsterPartStacks`（step1 の SpecialItem は ≤2）、
    `AlchemyCauldronLv2_DoesNotRequireDeepDebutPart`（古木の芯 を要求しない）。
- **R4：tier2 中位素材の現金売却口を拡充**（`TraderCatalog`）。錬金釜Lv1 で変換不可・売り先も無く
  周回で塩漬けになる 8 素材（剛毛/蜘蛛の糸/鉄の兜/毒腺/錆びた短剣/風切羽/若木の枝/腐肉）を
  ダグの買取に `×3 → 18 G` で追加（レート仮、tier2 ≒ 6 G/個）。
  テスト追加：`MidTierMonsterParts_HaveAMoneySink`。
- **R5：序盤の中結晶・お金の供給を1本**（`TraderCatalog`）。cold start の中結晶が glen_1（小15→中3・1回）
  だけで研究/設備Lv2 が枯れで止まる所見。`glen_3`（小30+20G 預け）の報酬に中結晶×3 を追加、
  `dag_1`（討伐10）を 中結晶×3+30G → ×4+40G、`glen_1` の現金 20→30G。id は不変（セーブ互換）。
- **未対応（要判断）**: R2 の魔力炉Lv2 の `M(n)` は中結晶バッファ次第で依然重い可能性（cold start 再測で判断）、
  研究机Lv2/サークルLv2 の光・雷エレメント欠片（対応属性のモンスター素材が1体も無い＝リーゼ横断連鎖か
  サークル頼み）、「まず杖」導線、farm テンポの単調さ。次は改善反映後の cold start 5周で深度推移を再計測。

### -4. 再検証3周のフィードバック反映：N1（新規敵保証の副作用）／C1（グリッド火力ゲート）／C2（深部の即死）（2026-09-09 その4）
`Docs/検証レポート/2026-09-09_再検証_{1,2,3}周目*.md`（改善「-3.」の cold start 3周検証）。バグ・ソフトロックはゼロ、
指摘は全てバランス／導線。レポートの「次にやるなら（優先度順）」から N1・C1・C2 を実装。テスト 166→169。

- **問題（N1）**: `EnemyCountFor` は深度2〜5で1体/ウェーブ。そこへ `NewlyOpenedIndexFor` の debut 敵保証枠が
  唯一のスロットを固定するため、深度2〜5のウェーブが「debut 敵1種で完全固定」に。結果：
  (a) 最弱スライム（プール index0）が抽選から消え、スライムゼリー（魔力炉Lv1・マジックサークル・複数装備/タスクの
  コスト）が深度1の escape 周回でしか安定入手できない、(b) 浅層のウェーブ多様性・AoE の価値が消える。
- **対策**（`EndlessWaveGenerator`）:
  - `MinTwoEnemyDepth`(=2) を新設。`EnemyCountFor` は**深度2以降 最低2体**（`count = Max(count, 2)`）。
    従来カーブと変わるのは深度2〜5 のみ（1→2）。深度6以降は元々2体なので不変。
  - `PickIndices` の debut 保証は **RNG 枠を1つ以上残せるとき（`result.Count >= 2`）だけ**差し込む。
    ＝1体ウェーブは保証で潰さず通常抽選のまま（＝レポートの提案 (a)+(b) を両立）。
  - これで debut 深度でも「保証枠＝debut 敵／もう1枠＝RNG（最弱含む）」となり、スライム等が再び抽選対象に。
- **テスト**: `EnemyCountFor_IsAtLeastTwo_FromDepth2` / `PickIndices_DebutDepth_StillLetsWeakestRoll` /
  `PickIndices_SingleEnemyWave_DoesNotForceGuarantee` を追加。既存の
  `EnemyCountFor_GrowsEveryFiveDepths_AndCaps` / `PickIndices_AlwaysIncludesNewlyOpenedEnemy_AtDebutDepth` を新契約に更新。

**C1（グリッドが火力ゲート）— 初期グリッドを 3×3 に**（`MagicGridManager` / `SampleScene.unity`）
- `minGridSize` を 2→3。杖 tier → 1辺は `未製作3×3 / Lv1杖4×4 / Lv2以上5×5`（`WandTierToSize` の式は不変、
  クランプで Lv2/Lv3 は 5×5 に収束）。シーンの `MagicGridManager.minGridSize` も 3 に更新。
- 狙い：杖なしでも「基本＋1枚」が置け、**最初の杖で 4×4＝MegaFire＋Flame(AoE)＋単体が同時に載る**。
  3周とも「3×3 では単体特化か範囲特化の二択」で深度13頭打ちだったのを、tier1 杖の一段で解く。
- `MagicGridResizeTests.WandTierToSize_MapsTierToSideLength` を新マッピングに更新。

**C2（ウェーブ間 HP 無回復＋深部バーストで満タンから即死）— 2 方向で対処**
- **深層スケーリングをさらに寝かせる**（`EndlessWaveGenerator`）: `ScalingTaperKneeDepth` 12→10、
  `ScalingTaperSlope` 0.5→0.4。深度11以降の敵 HP/Atk 倍率の伸びを抑える（例：深度13 の HP 倍率 2.8→約2.6）。
  既存のテーパーテストは定数参照なので追従（膝までの素の線形・膝から先で伸び幅が縮む不変条件は不変）。
- **tier2 サステインの入手性を底上げ**（`TraderCatalog`）: 蒐集家オルカのタスクラインに `orca_2b`
  「深度 12 まで到達する」→ 報酬 `acc_regen_torc`（再生のトルク＝毎秒 +3.5 回復）を追加。
  作業台Lv2（竜人の鱗など tier2 素材待ちの長い連鎖）を経由せず、壁の直前で毎秒回復アクセが手に入る。
  `GrantGear` 経由なのでコスト・電力・作業台Lv 不問。既存 `orca_3`〜`orca_9` の id は不変（セーブ互換）。

- **未対応（要判断）**: N2（Lv2 設備の連鎖ゲート）、C1 の別案（Mega シェイプの 2×2 化）、
  panic 脱出が間に合わない即死ウェーブの被弾総量クランプ、farm テンポの単調さ。

### -3. 通し検証3周のフィードバック反映：入場料セーフティ／新規敵保証／サステイン装備（2026-09-09 その3）
`Docs/検証レポート/` の3周分（バグ・ソフトロックはゼロ、指摘は全てバランス／導線）から3点を実装。テスト 158→166。

- **入場料セーフティ（後払い）**（`DungeonEconomy.EffectiveEntryFee(currentMoney)`）: 所持金が `BaseEntryFee`(15G)
  未満なら入場無料。タスク報酬が現物中心でお金が枯れ「潜れない」詰みに近づく所見への対策。
  `DungeonManager.CanAffordEntry` / `StartDungeon` が `EffectiveEntryFee` を使い、無料入場したぶんは
  `DungeonManager.entryFeeOwed`（=15）に記録。**帰還時（`RewardScreen`）に戦利品を安い順に1個ずつ自動売却して
  15G を精算**（`SettleEntryFeeFromLoot`：`pendingDrops` を直接減らし、`MoneyManager.Add(回収額)`→`TrySpend(15)` で
  実質相殺。戦利品が15G分に満たなければ不足ぶんは免除）。報酬画面に「入場料の精算：〇〇 ×N を売却（-15G）」の
  注記行（`DropRow.BindNote`）。死亡＝戦利品没収の run では精算なし（次 run 開始で `entryFeeOwed` リセット）。
  素材のゴールド価値は新設の `MaterialCatalog.GoldValue`（結晶1/10/100・欠片8・エレメント60・
  固有アイテムは `MonsterPartCatalog` tier で 3/10/20/60/150。トレーダー売却レート基準・仮）。
- **抽選窓に新規開放の敵を毎ウェーブ最低1体保証**（`EndlessWaveGenerator`）: `NewlyOpenedIndexFor(depth,poolCount)`
  ＝その深度で抽選窓の上限が前深度より増えたときの増分先頭（最強）インデックス、無ければ -1。
  `PickIndices` が新規開放敵を1枠に固定（末尾スロットを置換、重複時は何もしない）。
  毒針（大スズメバチ debut 深度4）・ゴブリンの牙（ゴブリン debut 深度6）等の「浅層の名前つき泥」の
  泥運の壁が、debut 深度を通るたび最低1体は湧くことで緩和される。窓がクランプ済み（プール消化後）は保証なし。
- **サステイン系アクセサリ10種**（`GearCatalog` / `PlayerStatus`）: 「ウェーブ間でHPが回復しない」＝
  最大HPより Def/殲滅速度が効く、という壁への直接対策としてアクセ枠に回復装備を追加。
  - `GearDef` に副効果フィールド `hpRegenPerSecond`（戦闘中の毎秒HP回復）／`healPerWaveFlat`（ウェーブ突破時の
    固定回復）／`healPerWavePercent`（同・最大HP割合）＋ `HasSustain`。
  - 10種：tier1 癒しのペンダント/繕いの腕輪/若葉の護符、tier2 命脈の指輪/再生のトルク/血石の首飾り/鼓動の護石、
    tier3 不死鳥の護符/生命の泉/永生のロケット。数値違い＋複合効果（鼓動＝毎秒+ウェーブ固定、永生＝3種全部）を含む。
    全て通常解禁（recipeGated でない）、主ステータスも保持。
  - `PlayerStatus`: `RegenHealth(dt)`（毎秒回復、int の端数は `hpRegenCarry` で持ち越し）／`HealWaveTick()`
    （ウェーブ突破時）／`ApplyGearDelta(GearDef, sign)`（主ステータス＋副効果を一括増減。装備の着脱で使う）／
    `OnHealed` イベント（HUD 拡張用、未配線）。`BattleReset` で carry リセット。
  - `BattleManager.Update` が `RegenHealth` も毎フレーム呼ぶ。`DungeonManager.HandleWaveDefeated` が
    `HealWaveTick()` を呼ぶ（WaveClear 画面に回復後HPが出る）。
  - `HideoutManager` の装備着脱4箇所を `ApplyResearchDelta(stat,amount)` → `ApplyGearDelta(gear,±1)` に統一。
  - `HideoutHubPanel` の作業台行は `GearEffectText(g)`＝主ステータス＋サステイン副効果を1行要約。
  - `GearCatalogTests` の Craftable 数 3→6 / 6→13、`SustainAccessories_AreWellFormed_AndAtLeastTen` 追加。
- **未対応（次周以降）**: 「まず杖」導線（今回は後回し）、TradePanel の前提未達タスク「納品可能」表示、
  `Transmute(part,times)` の times 非依存で燃料2固定、panic 脱出が間に合わない即死ウェーブ、farm テンポの単調さ。

### -2. スキルツリー整形 ／ 戦闘HUDのN体リスト ／ 深部バランス（2026-09-09 その2）
- **スキルツリーのレイアウト圧縮**（`0de5de2`）: ノードの重なりは元々無いが円盤が半径3560と
  過大だった。`Ring0Radius` 340→300 / `RingStep` 460→340（最外 ring7 の半径 3560→2680、約25%圧縮、
  最小エッジ間隔 53px 確保）。`ResearchTreeView` 既定ズーム 0.16→0.22。
  `ResearchGraphTests` に `NoTwoNodesOverlap` / `AllNodesFitInsideContentBounds` /
  `NodesInSameRingShareRadius` を追加＝今後ノードを足しても重なりを CI で検出。
- **戦闘HUDにウェーブ全個体の縦リスト**（`db6c2bc`）: `EnemyRowWidget`（新規・表示専用、名前＋HPバー＋
  HP数値、撃破で減光、代表個体は枠色）。`BattleHUD` が `enemyRowRoot`/`enemyRowPrefab` を持ち
  `OnRosterChanged` でプール生成・`Update` で毎フレーム流し込み・`OnDisable` で破棄。
  `BattleUISceneBuilder` が prefab 生成＋`BattleRoot` 左に `EnemyRowRoot`(VerticalLayoutGroup)。
  実機（Play）で 5 体ウェーブ表示を確認。代表個体パネルは従来通り。
- **深部の難度カーブを寝かせる**（`fc511c2`）: `EndlessWaveGenerator.ScalingFor` に taper 導入。
  `ScalingTaperKneeDepth`(=12) までは従来の (depth-1) と完全一致、そこから先は勾配
  `ScalingTaperSlope`(=0.5)。`defMult` は `DefMultCap`(=2.5) で頭打ち（絶対防御力は窓の入れ替えで上昇）。
  `DepthsPerExtraEnemy` 4→5（同時被弾＝バーストが最大の死因）。深度20 で hpMult 3.85→3.33・
  同時 5→4 体、深度30 で 5.35→4.08。浅〜中盤（1〜13）は不変。テスト 155→158。

### -1. WIP のコミット整理 ＋ 手動ステータス振り分けの永続化（2026-09-09）
- **未コミットだった WIP を検証して 3 コミットに整理**（EditMode 147/147 で確認）:
  - `fix: 日本語フォントの欠字を動的フォールバックで補完` — `Assets/Fonts/NotoSansJP-Light Dynamic SDF.asset`
    を追加し TMP Settings の `m_fallbackFontAssets` へ登録。静的アトラス未収録の漢字（商/傭/態/電/復/秒 等）が
    □ にならず動的ラスタライズで出る。`HideoutHubPanel` の ✓/✎ 記号を素の「（所持済）」「（レシピ未取得）」に。
  - `fix: 戦闘終了時にダメージ数字が画面へ残る不具合` — `DamageNumber.OnDisable` で自己 Destroy、
    `BattleHUD` が戦闘開始/終了で `damageNumberRoot` 配下を掃除（auto memory `gg-phase-root-coroutine-orphan`）。
  - `feat: 画面下部バーの「トレーダー」「隠れ家」ボタンを遷移に配線` — `GamePhaseManager` に
    `extraHideoutButtons` / `extraTradeButtons`、`BattleUISceneBuilder` が下部バー `Image/Button (7)(8)` を配線。
- **手動ステータス振り分けをセーブ対象化**（`af81af3`）:
  - `SaveData`: `playerStatsSaved` / `savedStatsPoint` / `manualStat{Hp,Atk,Def,Spd,Luc}`。研究の小ノード・
    製作装備ぶんは含めない（各 Manager が起動時に `ApplyResearchDelta` で再適用するため。含めると二重加算）。
  - **[PlayerStatSave.cs](../Assets/Script/PlayerStatSave.cs)（新規・純ロジック）**: `SaveData` の当該領域だけを
    読み書きする `Read` / `Write`。`PlayerStatSaveTests` 5 本（round-trip・他フィールド非破壊・JSON 経由）。
  - `PlayerStatus`: `AddStat` / `AddStatsPoint` で手動加算量を積算し `Persist`（Load→自領域→Save）、
    `Start` でシーン初期値へ加算して復元。`AddStat("HP")` は `currentHp` も追随（`ApplyResearchDelta` と挙動統一）。
  - テスト 147→152 グリーン。

### 0. トレーダーのタスクライン ＋ お金（ゴールド）経済
- **お金（ゴールド）**: `MoneyManager`（シーンシングルトン、`PlayerInventory` と同型）。`SaveData.money` /
  `moneyInitialized`（開始所持金＝仮 80G を1度だけ付与）。`OnMoneyChanged` を購読する `MoneyLabel`（構築画面・
  トレード画面ヘッダ）。`BattleUISceneBuilder` が `MoneyManager` を生成し所持金ラベルを配線。
- **ダンジョン入場料**: `DungeonEconomy.EntryFee()`＝仮 15G。`DungeonManager.StartDungeon()` が `MoneyManager`
  から徴収、`CanAffordEntry()`。払えないと出撃せず構築画面に留まる。`MoneyManager` がシーンに無ければ無料。
- **タスクライン**: `TraderTask.requires`（前提タスクID）で連鎖化。`TraderCatalog.Chain(...)` が定義順に前タスク
  IDを埋める。各トレーダー 8〜9 段（グレン/リーゼ/ダグ/オルカ）。`TradeManager.IsTaskUnlocked` /
  `VisibleTasks`（解放済み＋次の未解放1件）。`TradePanel` は未解放を「未解放」グレー行＋`前提: 〜` で表示。
- **タスク報酬の拡張**: `rewardMoney` / `rewardGearId`（`HideoutManager.GrantGear` で完成品直接付与）/
  `rewardRecipeId`（`HideoutManager.UnlockRecipe`）。納品タスクは `deliverMoney`（納金）も消費。
- **作業台レシピ解禁制**: `GearDef.recipeGated`、`SaveData.unlockedGearRecipes`。上位装備 6 種を新設
  （`wand_runed`/`acc_sigil`/`armor_warded`/`wand_stormcaller`/`armor_aegis`/`acc_orb`）。`GearCatalog.Craftable`
  はレシピ品を除外、`CraftableWithRecipes(level, unlocked)` を追加。ハブUIは未解放を「✎レシピ未取得」で表示。
- **交換オファーの拡張**: `TradeOffer.giveMoney` / `gainMoney`。各トレーダーに素材→お金の売却口と
  お金→素材/ステP の買取を追加。`TradeManager.CanTrade/TryTrade` が `MoneyManager` を出し入れ。
- **テスト**: `DungeonEconomyTests` 新規、`TaskRulesTests`（納金）/`TraderCatalogTests`（連鎖整合・gear/recipe
  参照・売却口）/`SaveManagerTests`（money/recipe ラウンドトリップ）/`GearCatalogTests`（レシピゲート）を追記。
- **ツール修正**: `Tools/unity-common.ps1` — `-runTests` のときは `-quit` を付けない（Unity 6 でテスト前に
  終了して結果が出ない問題）。これで `Tools/unity-tests.ps1` が正しく走る。

### 1. 杖グリッドのサイズを作業台の杖 tier で可変化（`19c395b`）
- [MagicGridManager.cs](../Assets/Script/MagicGridManager.cs): 固定 5×5 → `HideoutManager.OwnedGear` から
  所持している杖（`GearSlot.Wand`）の最上位 tier を取り、`minGridSize(2) + tier` を 1 辺に
  （クランプ `maxGridSize=5`）。
  - **未製作 2×2 / 作業台Lv1の杖 3×3 / Lv2 4×4 / Lv3 5×5**
  - `Start` と `GamePhaseManager.OnPhaseChanged`（Build 入場）で `ApplyWandTierSize()`。
  - `SetGridSize(w,h)` はサイズ変更時のみ配列を作り直し、`RectTransform.sizeDelta` /
    `GridLayoutGroup.constraintCount` / マス目背景（シーンの 25 セル子）の表示数を更新。
  - サイズが変わったときだけ `MagicSpawner.ResetBoard()`（新規: 生成済みピース全破棄＋ボタン復元）＋
    `OnGridResized` 発火。同サイズなら盤面維持（戦闘往復で消えない）。
- テスト: `MagicGridResizeTests`（5 本）。

### 2. 敵 40 体化＋序盤バランス調整（`5328f09`）
- [EnemyDataGenerator.cs](../Assets/Editor/EnemyDataGenerator.cs): 4 体 → **40 体**。
  弱い順の正本 `EnemyDataGenerator.OrderWeakToStrong`（`DungeonGenerator` も流用）。
  既存 4 体（Slime/Goblin/GiantRat/ForestGuard）は FileId とドロップ名を維持しつつ数値再調整。
  テーマ: 森の小物→森の中型→遺跡洞窟→魔性上位→ボス級深淵。
- **抽選窓の導入（重要）**: 旧 `EndlessWaveGenerator.PickIndices` は `[min, poolCount)` から一様抽選で、
  40 体プールだと深度 1 で最強敵が出うる不具合。新設 `MaxPoolIndexExclusiveFor(depth,poolCount)` =
  `StartingChoices(2) + (depth-1)*EnemiesUnlockedPerDepth(1)` クランプ poolCount。
  `MinPoolIndexFor` は `(depth-WeakCutoffLagDepths(7))/DepthsPerPoolShift(3)` を maxExclusive-1 で
  クランプ。窓 `[min,maxExclusive)` から抽選。**深度 1＝最弱 2 体**、深度 n で n+1 体目まで解禁。
- スケーリング緩和: HpGrowth 0.18→0.15 / AtkGrowth 0.12→0.10 / DefGrowth 0.08→0.07 /
  DepthsPerExtraEnemy 3→4。
- プレイヤー初期値（シーンの `PlayerStatus`）: hp 100→130 / atk 5→7 / statsPoint 5→10 /
  maxMana 100→120 / manaRegen 8→10。`ManaRules.BaseMaxMana` 100→120・`DefaultRegenPerSecond` 8→10。
- Tier1 魔法強化（`MagicDataGenerator`、`singleIntervals`/`aoeIntervals` を `float[]` 化）:
  単体 dmg {5,10,20}→{7,13,24} / interval {3,5,7}→{2.8,4,6}、全体 dmg {3,6,12}→{4,8,15} /
  interval {5,7,9}→{4.5,6,8}。`ManaRules.CastCost` 式は不変。
- 実機確認: 2×2 グリッド＋基本単体 2 本で深度 1〜5 をほぼ無傷で踏破（道中無回復で HP 130→119）。

### 3. 敵 40 体化に伴う経済まわりの整備（`b15544b`）
- **[MonsterPartCatalog.cs](../Assets/Script/MonsterPartCatalog.cs)（新規・Runtime・純データ）**:
  敵 40 体のドロップ 43 種の正本。各 `{name, tier(1〜5), attribute}`。
  `EnemyDataGenerator` の `Part(...)` 名と 1 対 1（生成器が未登録名を `Debug.LogWarning`）。
- **[HideoutRules.cs](../Assets/Script/HideoutRules.cs) `Transmute`（錬金釜）を汎用化**:
  ハードコードを廃し `MonsterPartCatalog` の tier/属性で決定。
  属性持ち tier2+ → 対応属性の欠片（per: t2-3=1 / t4=2 / t5=3）／
  それ以外 → t1:小×2・t2:中×1・t3:中×2・t4:大×1・t5:大×3。未登録は t1 相当。
  既存 3 ケース（ゴブリンの牙/番人の樹皮/古木の芯）は結果不変。
- `HideoutRules.RarityOf`（マジックサークル）の SpecialItem 判定を tier ベースに
  （t1 Common / t2-3 Uncommon / t4 Rare / t5 Epic）。
- **[TraderCatalog.cs](../Assets/Script/TraderCatalog.cs) 追加**: リーゼ（属性素材→欠片/エレメント精製）
  ・ダグ（中〜深層素材の結晶化買取＋討伐タスク: スケルトン8/オーガ5/ドラゴン3）
  ・オルカ（深層買取＋深度15/25到達＋遺跡蒐集＋「深淵の証」）。計 offers 42 / tasks 19。
  討伐対象名は `EnemyData.enemyName` と一致必須。
- 参照整合性テスト（トレーダー・ハイドアウトが参照する全 SpecialItem が `MonsterPartCatalog` に
  登録済みか）を追加。

### 4. ハイドアウト建造コストの tier を段階化（`1e1902f` → `d680614`）
- ルール: **Lv1=tier1 のみ / Lv2=tier1〜2 / Lv3=上限なし（深層 tier4-5 可）**。
- 研究机 Lv1 = 毒針（大スズメバチ, tier1）×4。
  Lv3 の深層素材: 魔力炉=巨神の核 / 研究机=世界樹の若枝 / 作業台=竜のうろこ / サークル=命の宝珠。
- `HideoutCatalogTests.BuildCosts_StayWithinDepthBudget_PerStep` が上限 `{1,2,5}` を検証。

### 5. 錬金釜のレベルで変換できる素材 tier を制限（`b28fe1c`）
- **Lv1=tier1 / Lv2=tier1〜3 / Lv3=tier1〜5**。
- `HideoutCatalog.CauldronMaxTier(level)` = 0/1/3/5、`CauldronLevelForTier(tier)` = 必要レベル。
- `HideoutRules.CanCauldronProcess(level, tier)`（純関数）。
  `HideoutManager.CanTransmute` が `MonsterPartCatalog.TierOf` と併せて判定。
  `HideoutManager.CauldronMaxTier` プロパティも公開。
- [HideoutHubPanel.cs](../Assets/Script/UI/HideoutHubPanel.cs) `BuildCauldronSection` を刷新:
  旧＝5 素材ハードコード → 所持しているモンスター素材を tier 昇順に全列挙。
  未解放 tier は「… 錬金釜Lv{N}で解放」のグレー行。

---

## セッション前からの実装（要点のみ、詳細は memory / CLAUDE.md）

- 構築フェーズ（インベントリパズル: 配置/回転/ドラッグ）、ステータス振り分け UI。
- オートバトル（攻撃/状態異常/補助/バフ、敵反撃、状態異常 5 種、属性耐性、Spd/Luc、マナ）。
- 複数敵ウェーブ＋AoE（`EnemyRoster`）、エンドレスダンジョン（深度無限・脱出選択）。
- 戦闘 HUD、報酬画面＋素材インベントリ＋JSON 永続化（`gg_save.json`）。
- ハイドアウト 5 設備（魔力炉/研究机/錬金釜/作業台/マジックサークル）を素材で建造→Lv3 強化。
  魔力炉の燃料が全設備アクションの前提。
- 研究の放射状スキルツリー（167 ノード、円盤状レイアウト、ドラッグ/ズーム）。
- トレード画面（4 トレーダー = タブ、各自「交換／依頼」サブタブ）。
- コアループ画面遷移は [GamePhaseManager.cs](../Assets/Script/GamePhaseManager.cs)
  （Build / Hideout / Research / Trade / Battle / WaveClear / Reward）。

---

## 純ロジッククラス（シーン非依存・EditMode テスト対象）

| クラス | 役割 |
|---|---|
| `BattleFormula` | ダメージ式・Spd/Luc 係数 |
| `ManaRules` | マナ消費・回復 |
| `PassiveBonusCalculator` | パッシブ集計 |
| `StatusEffectController` | 状態異常タイマー |
| `EndlessWaveGenerator` | 深度スケーリング・ウェーブ生成・**抽選窓** |
| `MaterialLedger` | 素材集計 |
| **`MonsterPartCatalog`** | 敵 40 体のドロップ = 固有素材の正本（tier/属性） |
| `ResearchGraph` / `ResearchRules` | スキルツリー定義 / 解放判定 |
| `HideoutCatalog` / `HideoutRules` | 5 設備の定義・効果・変換・**tier ゲート**・抽選 |
| `GearCatalog` | 製作装備（通常 9 種＋`recipeGated` 6 種） |
| `TradeCatalog` | 旧・交換メニュー（`StandardOffers`） |
| `TraderCatalog` / `TaskRules` | 複数トレーダーとタスクライン（`requires` 連鎖・報酬に素材/お金/装備/レシピ/ステP） |
| `DungeonEconomy` | ダンジョン入場料 |
| **`PlayerStatSave`** | 手動ステータス振り分けの `SaveData` 読み書き（研究/装備ぶんは含めない） |

---

## 未整備 / 今後の候補

- 新 36 体それぞれ個別のトレーダー変換オファー（今は代表選定にとどめている）。
- 深部バランスは taper でカーブを寝かせたが、**実プレイでの検証は未実施**（研究/装備/グリッド拡大を
  積んだ状態で深度 15〜30 を踏破できるか。数値は依然すべて仮）。
- トレーダーの顔アイコン Sprite を `TradePanel.traderIcons` にインスペクタで割り当て（枠は用意済み）。
- フォント欠字は動的フォールバック（NotoSansJP-Light Dynamic SDF）で補完済み。未収録漢字が
  出た場合はフォールバック未適用の TMP か、フォールバック側にも無い字。要現物確認。
- スキルツリーのさらなる手調整（重なり無し・円盤状にはなっている）、
  ハイドアウトの見た目（配置図・アイコン・演出）、装備スロット UI・入替、
  マジックサークルの実時間経過の可視化、Character 画面、AoE 以外の範囲パターン。
- 戦闘 HUD の N 体個別ウィジェットは実装済み（`EnemyRowWidget`）。位置/サイズは仮、要見た目調整。
