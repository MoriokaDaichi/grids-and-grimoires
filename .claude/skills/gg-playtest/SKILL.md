---
name: gg-playtest
description: Run a cold-start playthrough verification of Grids & Grimoires and produce a findings report. Use when asked to 「通しプレイ検証」「通し検証」「再検証」「playtest」「cold start N周」, to sanity-check a balance/loop change end-to-end, to confirm the core loop still has no exceptions/softlocks, or to feed the improvement loop (改善→検証→レポート→改善). Covers both the fast in-engine autopilot (Grimoire > Run Playtest) and the slower manual execute_code deep-run.
---

# 通しプレイ検証（Grids & Grimoires）

cold start（セーブ wipe 直後）から複数周プレイして、**到達深度の推移・道中HP余力・進行不能（詰み/例外）の有無**を測り、レポートに落とす作業。企画・数値の正本は [Docs/Grids_and_Grimoires_開発資料.md](../../../Docs/Grids_and_Grimoires_開発資料.md)、経緯は auto memory `game_design_grids_and_grimoires`。数値は全て仮。

## いつどっちを使うか

| | A. 自動ハーネス（`PlaytestAutopilot`）| B. 手動ディープラン（`execute_code` 駆動）|
|---|---|---|
| 速さ | 数分・ほぼ無人 | 1時間前後・ツール呼び 100+ |
| 用途 | 改善の回帰チェック／「壊れてないか」／相対比較／改善ループの各サイクル | 「-5.」級のバランス検証／**到達深度の絶対値**が要るとき／人力に近い経済判断が要るとき |
| 到達深度 | **絶対値は低く出る**（研究小ノード未割当・グリッド greedy パッキング）。深度8前後で頭打ち | 人力に近い（前例: 5周で 11→16）|
| 経済 | 貪欲エージェント（`runEconomy`）| 自分で判断（結晶配分・杖優先など）|

**まず A を回して健全性（例外ゼロ）と相対比較を取り、絶対深度や細かいバランスが要るときだけ B。**

## 前提（両モード共通）

- Unity Editor 起動中・**Edit モード**（Play 中はテストを回せない）。
- 検証前に EditMode グリーンを確認: MCP `run_tests`（assembly `GridsAndGrimoires.EditModeTests`）。検証後も再確認し、レポートに「N/N（不変）」と書く。
- シーンは `Assets/Scenes/SampleScene.unity`。`Grimoire > Build Battle UI` / `Generate Dungeon` 等が未実行なら先に流す（`enemyPool` が 40 体か、`MagicSpawner.magicDataList` が 67 か execute_code で確認）。
- コードを触った直後なら `is_compiling==false` を待ってから。

---

## モード A: 自動ハーネス

エディタ拡張 [PlaytestDriver.cs](../../../Assets/Editor/PlaytestDriver.cs)（`[InitializeOnLoad]`）＋ ランタイム [PlaytestAutopilot.cs](../../../Assets/Script/PlaytestAutopilot.cs)（再生時のみ生成、通常プレイでは不活性）。

### 走らせる

```
メニュー:  Grimoire > Run Playtest > Cold start x3   （= Begin(3, 25)）
           Grimoire > Run Playtest > Cold start x5   （= Begin(5, 30)）
MCPから :  execute_code →  PlaytestDriver.Begin(loops, depthCap);
```

`Begin` が: セーブ wipe → 再生モード突入 → `~PlaytestAutopilot` を差す → 完了時 `Docs/検証レポート/_ループ_<yyyy-MM-dd_HHmm>.md` に書き出し → 再生モードを抜ける。

### 完了を待つ / レポートを拾う

`execute_code` で `SessionState` を数十秒おきにポーリング（再生モード中はエディタが忙しいので気長に）:

```csharp
return "done=" + UnityEditor.SessionState.GetBool("gg_playtest_done", false)
     + " report=" + UnityEditor.SessionState.GetString("gg_playtest_report", "")
     + " playing=" + UnityEditor.EditorApplication.isPlaying;
```

`done==true` になったら `report` のパスを Read。中断（手動停止など）は `gg_playtest_active` が畳まれる。

### ノブ（`PlaytestAutopilot` のフィールド / `Begin` は loops・depthCap だけ）

`loops` 周回数 / `depthCap` これ以上潜らない / `timeScale`(=20) / `escapeHpFraction`(=0.35 ウェーブ突破時これ未満で脱出) / `perRunRealTimeout`(=150s 保険) / `runEconomy`(=true 周またぎの貪欲経済)。細かく変えたいときは `PlaytestDriver.OnPlayModeChanged` の `ap.xxx=` を一時的に足すか、`Begin` の直後にコード注入。

### ハーネスがやること / やらないこと

やる: ステP配分（HP:Def:Atk = 4:3:3）→ 攻撃魔法を小さい形状順に greedy first-fit（回転4方向）→ 出撃 → ウェーブ突破ごとに `escapeHpFraction`／`depthCap` で進む/脱出 → 報酬帰還（`RewardScreen.OnReturn` を SendMessage）→ 周またぎ経済（タスク受領／結晶の 大→中→小 崩し／設備建造 魔力炉→錬金釜→作業台→研究机／燃料バッファ維持／杖・サステインアクセ製作／モンスター素材の変換〈未建造設備・未製作装備の建材は温存〉／Lv2 強化）。error/warning/exception を集計。

**やらない**（＝レポートに毎回書く限界）: 研究スキルツリーの**小ノード割当**（＝ステ/マナ研究が伸びない → 到達深度が低く出る主因）／グリッドの**最適パッキング**（GigaFire＋大型AoE を 4×4 に同居できない）／マジックサークル（実時間待ち）／**ウェーブ内**の事前脱出（1ウェーブ即死は `WaveDamageCap` で緩和されるが脱出判断は間に合わない）。

---

## モード B: 手動ディープラン（`execute_code` 駆動）

人力に近い経済判断で cold start N 周。手順とスニペット集は **[references/manual-run.md](references/manual-run.md)** に全部ある（自動ドライバの常駐、sortie/poll、報酬の明示帰還、配置、経済ブートストラップ、既知の落とし穴）。要旨:

1. `manage_editor stop` → `Grimoire/Wipe Save`（menu）→ `manage_editor play`。
2. `EditorApplication.update` に常駐ドライバを1本差す（WaveClear で進む/脱出、Reward で `RewardScreen.OnReturn` をリフレクション実行 → `PlayerPrefs` に結果）。
3. 1周 = ①ステ配分＋魔法配置 → ②farm 数回（`gg_cap` 低め・`gg_esc` 高めで脱出、素材を溜める）→ ③経済パス（建造/変換/研究割当/交換/タスク）→ ④push（`gg_cap`=99・`gg_esc`≈0.1 で壁まで）→ ⑤スナップショット。
4. 各周: 開始キット・到達壁・何が解放されたか・詰み/例外を記録。
5. 終了後: `manage_editor stop` → EditMode 再確認 → レポート。

**必ず守る**:
- 資源注入しない（ゲームAPI＝振り分け/建造/研究割当/変換/交換/依頼受領/出撃 のみ）。
- 戦利品は死亡で没収。farm は脱出前提、push だけ死んでよい。
- `RewardScreen` は `OnReturn`（private・要リフレクション）を呼ばないと戦利品が入らない。`ReturnToBuild()` だけだと素材ゼロのまま進む。
- グリッドのリサイズは Build への**フェーズ遷移**でしか走らない。杖を作った直後は `grid.ApplyWandTierSize()` を明示的に。
- `execute_code` の `replay` はヒストリが ~30 を超えると別エントリを叩くことがある。**小さいスニペットは毎回貼り直す**。
- `Time.timeScale` は 25〜45。それ以上は挙動が怪しくなる。

---

## レポート

置き場: `Docs/検証レポート/`

| 命名 | 用途 |
|---|---|
| `_ループ_<yyyy-MM-dd_HHmm>.md` | モード A の自動出力（`PlaytestDriver` が書く）|
| `YYYY-MM-DD_再検証N_...周目.md` / `..._総括.md` | モード B の周ごと＋総括（手書き）|
| `_改善ループ_進捗.md` | 改善ループ（改善→検証→レポート→改善）の進捗・キュー・ログ |
| `README.md` | フォルダの索引（各レポートの役割＋最終到達点）|

古い生ログ（`_ループ_*.md`）と初期ベースライン（`2026-09-09_通し検証_*` ほか）は `Docs/検証レポート/_アーカイブ/` に退避済み。数値は `_改善ループ_進捗.md` に転記済み。

モード B レポートに必ず入れる: 方針（資源注入なし）／EditMode N/N（検証前後）／**到達深度の推移表**（周 × 壁 × キット × 解放）／改善の判定（⭕/🔺/❌＋根拠）／新規の構造的所見（バグではなくバランス/導線は分けて）／安定性（出撃数・ウェーブ数・例外/error/warning の有無）／次にやるなら（優先度順）／付録: 確認した数値とソース。

ハーネス（モード A）レポートには **限界の但し書き**（研究小ノード未割当・パッキング下手・ウェーブ間脱出のみ）を毎回。到達深度の絶対値を人力プレイと並べない。

---

## 既知の構造的所見（新しい run で「解けたか」を必ずチェック）

再検証3（2026-09-09）以降の未解決 or 対応中。詳細は `Docs/検証レポート/2026-09-09_再検証3_cold-start5周_総括.md` と DEV_LOG「-6.〜-8.」。

- **D1 cold-start の結晶詰み**: 設備Lv1 の建材が小結晶中心なのに序盤収入は中結晶。錬金釜Lv1 の tier1 変換は `FurnaceFuelPerAction` 次第で燃料中立〜微益。配分ミスで回復不能に近づく。対応: 錬金釜Lv1 を中結晶払いに（`M(2)`）、`FurnaceFuelPerAction(1)` 2→1、orca_2 の結晶ゲートを深度10→8。→ **新 run で「貪欲でも詰まないか」を確認**。
- **D2 「まず杖」導線**: 研究より先に見習いの杖を作るべき、を誘導する仕組み。一次対応 = ダグ連鎖の先頭に `dag_wand`（`TraderTaskKind.CraftGear`、報酬 中結晶×3）。構築画面での能動的提示はまだ。
- **D3 錬金釜Lv2 の中結晶ゲート**: `M(5)`→`M(3)` 済み。中結晶の中盤 faucet がまだ細い。
- **D5 / C2 ウェーブ内バースト即死**: `BattleFormula.WaveDamageCap`(=0.6×maxHp) を `PlayerStatus.TakeDamage` に。1ウェーブで最大HPの60%超は削れない（死の無効化ではない）。
- **D6 Atk が伸びない**: D6-a = 深部の敵Atk倍率を寝かせる（`EndlessWaveGenerator.AtkScalingTaperSlope=0.25`）済み。D6-b（研究ツリーの Atk 小ノードを中結晶ゲートより手前へ、`ResearchGraph` 変更・リスク高）は未。
- **D7 4×4 に GigaFire＋強AoE が同居不可**: tier2 杖＝5×5 の導線として作業台Lv2 step1 を `竜人の鱗×2`(d13)→`錆びた短剣×2`(d9) 済み。
- **ハーネス自身の宿題**: 研究小ノード割当を足す（絶対深度も測れるように）／グリッド greedy パッキング改善。

## 確認しておくと早い数値

- `HideoutCatalog`: `CauldronYieldMult` Lv1=1.0/Lv2=1.4/Lv3=1.9、`FurnaceFuelPerAction` Lv1=1(旧2)/Lv2=1/Lv3=1、`FurnaceSlots` Lv1=2/Lv2=3/Lv3=4。
- tier1 変換の産出（`HideoutRules.Transmute`）= 小結晶×2/個。tier2→中×1、tier3→中×2、tier4→大×1、tier5→大×3。属性持ち tier2+ は対応属性の欠片。
- 敵 debut（`EnemyDataGenerator.OrderWeakToStrong` / 窓）: スライム d1 / コウモリ d2 / 大ネズミ d3 / スズメバチ d4 / キノコ人間 d5 / ゴブリン(ゴブリンの牙) d6 / 森オオカミ d7 / 荒れイノシシ(剛毛 t2) d8 / コボルト(錆びた短剣 t2) d9 / 毒グモ(蜘蛛の糸 t2) d10 / ゴブリン戦士(鉄の兜 t2) d11。
- 入場料 15G（所持金<15Gなら無料・帰還時に戦利品から精算）。開始値 HP130/Atk7/Def5/Spd5/Luc5、ステP10、maxMana120/regen10、80G。
- 純ロジック（EditMode 可）: `BattleFormula` / `ManaRules` / `EndlessWaveGenerator` / `EnemyDataGenerator.OrderWeakToStrong` / `HideoutRules`+`HideoutCatalog` / `MonsterPartCatalog` / `ResearchGraph`+`ResearchRules` / `TraderCatalog`+`TaskRules` / `MaterialCatalog` / `DungeonEconomy` / `PlayerStatSave`。
