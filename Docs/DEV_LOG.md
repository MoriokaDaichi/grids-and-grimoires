# 開発ログ / セッション引き継ぎ

Claude Code セッションでの実装内容の記録。企画・数値の正本は
[Grids_and_Grimoires_開発資料.md](Grids_and_Grimoires_開発資料.md)、設計判断の経緯は
auto memory `game_design_grids_and_grimoires`（`~/.claude/projects/.../memory/`）にも
フェーズ単位で残っている。ここはコード寄りの実装サマリ。

---

## 現在の状態（2026-09-09 時点）

- **ブランチ**: `master`。`feat/battle-ui-core-loop` は PR #1 マージ済み（`7ff60e3`）で残置。
- **EditMode テスト**: 152/152 グリーン（`GridsAndGrimoires.EditModeTests`）。
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
- 深部（深度 15+ で基本魔法だけ）の実バランス調整 ＝ 研究/装備/グリッド拡大の出番。
- トレーダーの顔アイコン Sprite を `TradePanel.traderIcons` にインスペクタで割り当て（枠は用意済み）。
- フォント欠字は動的フォールバック（NotoSansJP-Light Dynamic SDF）で補完済み。未収録漢字が
  出た場合はフォールバック未適用の TMP か、フォールバック側にも無い字。要現物確認。
- 戦闘 HUD の N 体個別ウィジェット、スキルツリーのレイアウト整形、
  ハイドアウトの見た目（配置図・アイコン・演出）、装備スロット UI・入替、
  マジックサークルの実時間経過の可視化、Character 画面、AoE 以外の範囲パターン。
