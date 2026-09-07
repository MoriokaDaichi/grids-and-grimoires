# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

「Grids & Grimoires」— インベントリ・パズルで杖を組み上げる魔術師のダンジョン攻略オートバトラー（Unity 6000.3.9f1 / URLシーンは `Assets/Scenes/SampleScene.unity`）。

企画・データの正本は [Docs/Grids_and_Grimoires_開発資料.md](Docs/Grids_and_Grimoires_開発資料.md)。魔法マスターデータの数値・グリッド形状（8章）・クラフトコスト（6章）など、仕様に迷ったら必ずこのドキュメントを参照する。設計変更や実装ギャップの経緯は auto memory（`game-design-grids-and-grimoires`）にも記録されているので、大きな仕様追加の前に確認するとよい。

## ビルド・実行

Unity Editor (6000.3.9f1) で `Assets/Scenes/SampleScene.unity` を開いて再生するのが基本。

### テスト
EditMode テストが `Assets/Tests/EditMode/`（asmdef: `GridsAndGrimoires.EditModeTests`）にある。ランタイムコードは `Assets/Script/GridsAndGrimoires.Runtime.asmdef` / Editor拡張は `Assets/Editor/GridsAndGrimoires.Editor.asmdef` にアセンブリ分割済み（テストasmdefが `Assembly-CSharp` を参照できないため）。

- Editor起動中: MCP (`unity-mcp`) の `run_tests`（EditMode）で実行。
- Editor停止時: `Tools/unity-tests.ps1`（`Tools/unity-compile.ps1` はコンパイルチェックのみ）。

純ロジックは `BattleFormula`（ダメージ式・Spd/Luc）/ `ManaRules`（マナ消費・回復）/ `PassiveBonusCalculator`（パッシブ集計）/ `StatusEffectController`（状態異常タイマー）/ `EndlessWaveGenerator`（エンドレスの深度スケーリング・ウェーブ生成）/ `MaterialLedger`（素材集計）/ `ResearchGraph`（放射状スキルツリーのノード定義）＋`ResearchRules`（解放判定）/ `HideoutCatalog`＋`HideoutRules`（5設備の定義・効果・変換・抽選）／`GearCatalog`（製作装備）/ `TradeCatalog`（交換メニュー）/ `TraderCatalog`＋`TaskRules`（複数トレーダーと依頼タスク）に切り出してあり、シーン非依存でテストできる。

### Editor拡張（`Assets/Editor/`）
マスターデータやシーンUIはコードを直接編集するのではなく、Unityメニューから生成する運用。

- **Grimoire > Generate Master Magic Data**（[MagicDataGenerator.cs](Assets/Editor/MagicDataGenerator.cs)）: `Docs/`の4章・6章・8章のデータに基づき `Assets/MasicData/` 配下の `MagicData` アセットを一括生成・上書きする。
- **Grimoire > Generate Enemy Data**（[EnemyDataGenerator.cs](Assets/Editor/EnemyDataGenerator.cs)）: `Assets/EnemyData/` の敵4体（スライム/ゴブリン/大ネズミ/森の番人）＋属性・耐性・**モンスター固有ドロップ**を生成。ドロップは魔力結晶ではなく `MaterialType.SpecialItem`（`specialItemName`）＝スライムゼリー/ゴブリンの牙/大ネズミの尾/番人の樹皮・古木の芯。研究に使う結晶・欠片へはトレーダー（ダグ＝結晶化買取／リーゼ＝欠片・エレメント精製）で変換する。**数量・変換レートは全て仮バランス**。
- **Grimoire > Generate Dungeon**（[DungeonGenerator.cs](Assets/Editor/DungeonGenerator.cs)）: 開いているシーンの `DungeonManager.enemyPool` にエンドレスダンジョン用の敵プール（弱い順: スライム/大ネズミ/ゴブリン/森の番人）を設定し、旧 `waves` はクリアする。実行後シーン保存。
- **Grimoire > Populate MagicSpawner List**（[MagicSpawnerPopulator.cs](Assets/Editor/MagicSpawnerPopulator.cs)）: `MagicSpawner.magicDataList` を再登録する。実行後シーン保存（Ctrl+S）。
- **Grimoire > Build Battle UI**（[BattleUISceneBuilder.cs](Assets/Editor/BattleUISceneBuilder.cs)）: `SampleScene` に戦闘HUD/ウェーブ突破（脱出選択）/報酬/**ハイドアウトのハブ（`HideoutHubPanel`）**/研究（放射状スキルツリー `ResearchTreeView`、ルート名 `ResearchRoot`）/トレードの画面ルート、`GamePhaseManager` / `PlayerInventory` / `ResearchManager` / `HideoutManager` / `TradeManager` / `EnemyRoster` / `InventoryPanel` を構築する。ボタンはシーンの `MenuPanel` に既にあるものを流用＝`MenuPanel/Button`＝出撃（ラベルを「出撃」に）／`MenuPanel/Button (2)`＝トレード／`MenuPanel/Button (3)`＝ハイドアウト（`BindMenuButton` でラベル差し替え、onClick は `GamePhaseManager` が Awake で配線）。演出/行/ノードのプレハブ（`ResearchNode` 含む）も生成。旧・単体 `EnemyStatus` GameObject と旧・左下コーナーボタン（`HideoutButton`/`TradeButton`）は除去する。再実行可能。実行後シーン保存。
- **Grimoire > Wipe Save**（[SaveMenu.cs](Assets/Editor/SaveMenu.cs)）: `Application.persistentDataPath/gg_save.json` を削除。

## アーキテクチャ

### 魔法データ（`MagicData` / `Assets/MasicData/`）
`MagicData`（[MagicData.cs](Assets/Script/MagicData.cs)）は `ScriptableObject` で、企画書の分類をそのまま反映した列挙型構造を持つ：`MagicCategory`（攻撃/状態異常/補助/バフActive/バフPassive）× `MagicAttribute`（炎/雷/風/光/闇）× `MagicRange`（単体/全体/なし）。加えて `StatusEffectType`、`BuffStat`、`shapeNodes`（パズル形状、中心を(0,0)とした相対座標）、`requiredMaterials`（解放コスト）、`manaCost`（発動マナ。生成時に `ManaRules.CastCost` を焼き込む）を持つ。

### 実装済み / 未実装（2026-09 時点、ブランチ `feat/battle-ui-core-loop`）
- **動作**: 構築フェーズ（グリッド配置・回転・ドラッグ）、ステータス振り分けUI、**オートバトル**（攻撃/状態異常専用/補助/アクティブ・パッシブバフ、敵の反撃、状態異常5種、属性耐性、Spd=発動間隔短縮・Luc=会心、**マナ消費＋自然回復**）、**複数敵ウェーブ＋全体(AoE)魔法**（`EnemyRoster`）、**エンドレスダンジョン**（深度1から無限、ウェーブ突破ごとに「深層へ進む／脱出」を選択、深度に応じて敵がスケール）、**戦闘HUD**（代表敵HPバー・残り体数・マナバー・ダメージ数字・状態異常アイコン・バフインジケータ・深度表示）、**報酬画面＋素材インベントリ＋JSON永続化**（`gg_save.json`）、**ハイドアウト（5設備：魔力炉/研究机/錬金釜/作業台/マジックサークルを素材で建造→Lv3まで強化。魔力炉の燃料が全設備アクションの前提）**、**研究の放射状スキルツリー**（ハブの研究机から入る。中心から5属性の枝、小ノード＝ステータス/マナ強化を経由して大ノード＝魔法を解放、ドラッグ/ズーム対応。小ノードは `PlayerStatus` に恒久ボーナス。研究机レベルでコスト減・ボーナス増）、**トレード画面**（4トレーダー＝結晶両替/エレメント精製/戦闘/蒐集、各自の交換メニュー＋依頼タスク＝納品/討伐/深度到達）。
- **未実装 / 今後**: 戦闘HUDでのN体分の個別敵ウィジェット（現在は先頭生存個体＋残り体数のみ）、`PlayerStatus` の手動ステータス振り分けのセーブ対象化（研究の小ノード・製作装備ぶんは永続化済み）、スキルツリーのレイアウト整形（自動極座標配置。大型化で重なりは解消したが手調整の余地あり）、ハイドアウトの見た目（等倍配置図・設備アイコン・演出）、装備のスロットUI・入替、マジックサークルの実時間経過をゲーム内に見せる演出、Character画面、AoE以外の範囲パターン。
- **数値は全て仮**: 敵ステータス・属性耐性・ドロップ、状態異常の効果量/時間（`StatusEffectController` の定数）、バフ倍率（`BattleManager` の `PassiveStatPercentPerStage` 等）、Spd/Luc 係数（`BattleFormula`）、マナ上限/回復/魔法コスト（`ManaRules`）、深度スケーリング（`EndlessWaveGenerator`）、研究の小ノードの増加量・コスト（`ResearchGraph`）、トレードレート（`TradeCatalog`）、トレーダーの依頼内容・報酬（`TraderCatalog`）。企画書に記載が無く暫定。

### コアループの画面遷移（`GamePhaseManager`）
[GamePhaseManager.cs](Assets/Script/GamePhaseManager.cs) が `Build` / `Hideout`（設備ハブ）/ `Research`（スキルツリー）/ `Trade` / `Battle` / `WaveClear`（脱出選択）/ `Reward` を管理する（`buildPhaseObjects` と各画面ルートを `SetActive` で切替）。出撃ボタン→`StartSortie()`、ハイドアウトボタン→`GoToHideout()`、ハブの「研究する」→`GoToResearch()`、スキルツリーの戻る→`CloseResearch()`（ハブへ）、トレードボタン→`GoToTrade()`、各画面の戻る/帰還ボタン→`ReturnToBuild()`。`DungeonManager` の `OnWaveCleared` を購読して `WaveClear` 画面へ、`OnDungeonCleared`（脱出）/ `OnDungeonFailed`（敗北）で報酬画面へ遷移する。`WaveClear` 画面の「深層へ進む」→`ContinueRun()`（→`dungeon.ContinueDeeper()`）、「脱出する」→`EscapeRun()`（→`dungeon.Escape()`）。`DungeonManager.Start()` での自動出撃は廃止済み（出撃は必ず `StartSortie()` 経由）。空杖で出撃した場合は `StartDungeon()` が `false` を返し構築画面に戻る。

### 永続化（`SaveManager` / `SaveData`）
単一JSON（`Application.persistentDataPath/gg_save.json`）。各システムは **`SaveManager.Load()` → 自分の領域だけ更新 → `SaveManager.Save()`** の順で他システムのフィールドを潰さないようにする（`PlayerInventory.Persist` / `ResearchManager.Save` / `TradeManager.SaveProgress` 参照）。（`PlayerInventory.Persist` / `ResearchManager.Save` / `HideoutManager.Save` / `TradeManager.SaveProgress` 参照）。`SaveData` = 素材スタック（`MaterialLedger`）＋研究で割り当てたノードID（`allocatedResearchNodes`。魔法＋"node_..."のステータスノード。`unlockedMagicIds` はその魔法サブセット）＋トレーダーのタスク進捗（達成タスクID／累計撃破数／敵種別ごとの撃破数／最深到達深度）＋ハイドアウト（`facilityLevels`／`furnaceFuel`／`magicCircleBrews`＝`BrewRecord`／`craftedGearIds`）。`Serialize`/`Deserialize` は純粋関数でテスト済み。

### オートバトル（`BattleManager` / `EnemyRoster` / `EnemyStatus` / `DungeonManager`）
- 敵は `EnemyRoster`（シーンシングルトン）がプール管理する複数体。`DungeonManager` は `enemyPool`（弱い順）から `EndlessWaveGenerator` で深度に応じたウェーブを生成し、`roster.SpawnWave(enemies, scaling)` で `WaveScaling`（HP/Atk/Def 倍率）を適用する。ウェーブ全滅で `roster.OnWaveDefeated` → `DungeonManager.HandleWaveDefeated` が戦利品を積み、`battleManager.EndBattle()` で戦闘を止めて `OnWaveCleared` を発火（自動では次ウェーブに進まない）。「深層へ進む」で `ContinueDeeper()` → 次深度の `AdvanceWave()`、「脱出」で `Escape()` → `OnDungeonCleared`。単体魔法は `roster.FirstAlive()`、全体(`MagicRange.AoE`)魔法は `roster.Living` 全員に命中。反撃は各 `EnemyStatus` が自分の `attackTimer` で独立に行う。
- **マナ**: `BattleManager.Update` が毎フレーム `playerStatus.RegenMana(dt)`。発動タイミングで `playerStatus.HasMana(ManaRules.CastCost(data))` を判定し、不足なら `OnManaStarved` を発火して `ManaRules.StarvedRetryDelay` 後に再試行（発動間隔は消費しない）。足りれば `SpendMana` してから発動。マナはHPと同様ダンジョン内で持ち越し（`BattleReset` で全回復）。
- **再入ガードが要**: ウェーブ全滅→次ウェーブへの切替が列挙中に連鎖するため、`BattleManager.casts` は `Clear()` せず `new List<>()` へ差し替え、`foreach` 前にリスト参照/`roster.Living` をスナップショット、`TakeDamage` 前に敵名/HPをローカルへ退避、AoE/反撃ループは `roster.Generation` の変化で打ち切る。`EnemyStatus` 側は自身の `battleGeneration` で tick適用ループを打ち切る。新しい処理を足すときはこの規律を崩さないこと。
- **戦闘UIの拡張点**: HUDやエフェクトはロジックに触らず、`public System.Action` イベントを購読して作る（[StatusUIManager](Assets/Script/StatusUIManager.cs) の `OnStatusChanged` 購読パターンの踏襲）。既存イベント: `PlayerStatus.OnDamaged`・`OnManaChanged` / `EnemyStatus.OnDamaged`・`OnStatusApplied`・`OnStatusExpired`・`OnStatusTick` / `EnemyRoster.OnRosterChanged`・`OnWaveDefeated` / `BattleManager.OnCastFired`・`OnAttackHit`・`OnBuffApplied`・`OnBuffExpired`・`OnManaStarved` / `DungeonManager.OnWaveChanged`（深度, -1=エンドレス, 表示名）・`OnWaveCleared`・`OnEnemyDefeated`・`OnDungeonCleared`・`OnDungeonFailed` / `PlayerInventory.OnInventoryChanged` / `ResearchManager.OnUnlocksChanged` / `TradeManager.OnTraded`・`OnTasksChanged`。`HideoutManager.OnHideoutChanged`（設備レベル/燃料/捧げもの/製作の変化）も同様。参考実装は [BattleHUD.cs](Assets/Script/UI/BattleHUD.cs) / [HideoutHubPanel.cs](Assets/Script/UI/HideoutHubPanel.cs)。
- **座標系の注意**: グリッドはY上方向が正だが、`Docs/`の8章データは行が下方向に増加する座標系なので符号が逆（`MagicDataGenerator.cs` の `Shape()` ヘルパーで変換済み）。

### ハイドアウト：5設備（`HideoutCatalog` / `HideoutRules` / `HideoutManager` / `HideoutHubPanel` / `GearCatalog`）
[HideoutCatalog.cs](Assets/Script/HideoutCatalog.cs) が5設備（`FacilityKind` = ManaFurnace/ResearchDesk/AlchemyCauldron/Workbench/MagicCircle）の建造Lv1＋強化Lv2/Lv3の素材コストと、レベル別の効果パラメータを純粋データで持つ。[HideoutRules.cs](Assets/Script/HideoutRules.cs) はコストのスケール・マジックサークルの待ち時間/完成判定/結果レア度の決定論抽選・錬金釜の変換表・魔力炉の容量/稼働判定など純粋関数（EditModeテスト済み）。
- **魔力炉**: 全設備アクション（建造/強化/研究割当/製作/変換/捧げ/受取）の前提。魔力結晶を投入して燃料（結晶価値換算）に変え、1アクションごとに `FurnaceFuelPerAction(level)` を消費。魔力炉自体の建造/強化だけは電力不要。スロット数＝燃料バッファ上限（`FurnaceSlots(level) × 100`）。
- **研究机**: `HideoutManager.ResearchUnlocked`（Lv1+）で研究を解禁。`ResearchManager` は `HideoutManager.Instance` があれば `CanAllocate` で研究机の有無・電力を確認し、コストを `ResearchCostMult(level)` で軽減（`EffectiveCost`／`NodeCostText`）、小ノードのボーナスを `ResearchBonusMult(level)` で増加（割当時＋起動時再適用）。ハイドアウトがシーンに無ければ従来通り（nullガード）。
- **錬金釜**: モンスター素材 → 結晶/エレメント（`HideoutRules.Transmute`、`CauldronYieldMult(level)` で産出増）。
- **作業台**: [GearCatalog.cs](Assets/Script/GearCatalog.cs) の装備9種（杖/防具/アクセ ×3tier）を製作。tier ≤ 作業台Lv で解禁。所有＝装備中扱いで `PlayerStatus.ApplyResearchDelta` に恒久ボーナス（同スロットの旧装備は置換＝ボーナスを差し引き）。`craftedGearIds` を永続化、起動時に再適用。
- **マジックサークル**: 手持ちアイテムを1つ捧げる → `BrewRecord`（開始UTC秒／待ち時間／入力レア度）。待ち時間は入力レア度が高いほど長く（4〜12時間）、サークルLvで短縮。実時間（`DateTime.UtcNow`）で完成判定し、受取で `RollRarity`＋`CircleReward` の決定論抽選結果をインベントリへ。
- `HideoutManager`（シーンシングルトン）が設備レベル・燃料・捧げもの・製作装備を `SaveData`（`facilityLevels`／`furnaceFuel`／`magicCircleBrews`／`craftedGearIds`）へ「Load→自領域だけ更新→Save」で永続化。`OnHideoutChanged` を発火。
- `HideoutHubPanel`（`HideoutRoot` に付く）が5設備カード（建造/強化ボタン＋設備別の機能行）と燃料バーを1縦スクロールに動的生成（プレハブ不使用、`OnHideoutChanged`／`OnInventoryChanged` で再構築）。研究机の「研究する」→`GamePhaseManager.GoToResearch()`。
- **数値は全て仮**（企画書に設備コスト・効果・レート・待ち時間の記載なし）。

### 研究：放射状スキルツリー（`ResearchGraph` / `ResearchRules` / `ResearchManager` / `ResearchTreeView`）
[ResearchGraph.cs](Assets/Script/ResearchGraph.cs) が企画書5章の系統＋「深淵のスキルツリー」構想に沿って **167ノード**（大＝魔法67 / 小＝ステータス系100）を極座標（`ring` / `angleDeg`、`Ring0Radius=340` / `RingStep=460`、最外 ring7≈3560・円盤状）で定義する。中心から5属性の枝が放射状に伸び、各枝は 単体 基本→[小e]→[小a]→メガ→[小c]→ギガ／全体 基本→[小b]→メガ→[小d]→ギガ／状態異常特化←単体メガ／アクティブバフ←単体基本→パッシブ Lv1→2→3／属性バフ←単体メガ／付与率バフ←状態異常特化（＝魔法枝の小ノードは 25）。加えて属性ラインの**間**の角度（36/108/180/252/324°）へ、小ノード15個ずつの **「扇」5枚**（`BuildSpoke`＝75ノード。`SpokeRing`/`SpokeAngleOffset`/`SpokeParentIndex` の静的テーブルで、centerAngle±10°・ring0〜7 の楔に末広がりに枝分かれ。ライン枝と同じ深さで止め、全体が円盤状になるよう隙間を埋める。偶数index=主 / 奇数index=副ステータスで全種を網羅）。各ノードの親は1つ（純粋な木）。`ResearchStat`＝Hp/Atk/Def/Spd/Luc/ManaMax/ManaRegen。
- **大ノード**＝魔法（id は `MagicData` 名、コストは `requiredMaterials`）。**小ノード**＝ステータス系（id は `"node_..."`、コストはノード定義が保持）。
- `ResearchManager` はノードIDベース。`NodeState`（Allocated / Allocatable / Locked）、`CanAllocate` / `Allocate`（親が割り当て済み かつ 素材を賄える）。小ノード割り当て時＆起動時（`Start`）に `PlayerStatus.ApplyResearchDelta` で恒久ボーナスを反映する（`PlayerStatus` の値はセーブされないため毎起動再適用）。`SaveData.allocatedResearchNodes` に全ノードIDを永続化し、`unlockedMagicIds` は魔法サブセットとして同期（旧セーブからの移行対応）。
- `ResearchTreeView`（Viewport に付く）が `ResearchGraph` から実行時にノード（`ResearchNodeWidget` 大150px/小92px、状態で色分け）とエッジを生成。**ノード背景 Image は `raycastTarget=true` 必須**（false だとクリックが全部パン用の Viewport に吸われる）。ツリーが巨大（167ノード、最外 ring7≈3560、円盤状）なのでドラッグでパン・ホイールでズーム前提（初期 0.16、`MinZoom=0.08` で全景／`MaxZoom=1.3`）、`content` は 10000²。ノードを1回クリックで選択＋詳細（効果/コスト/状態）、選択済みの取得可ノードをもう一度クリック（または右下 [取得] ボタン）で割り当て。`ResearchNode.prefab` は builder が生成。

### 複数トレーダーと依頼タスク（`TraderCatalog` / `TaskRules` / `TradeManager`）
[TraderCatalog.cs](Assets/Script/TraderCatalog.cs) が専門分野ごとに4トレーダー（両替商グレン＝結晶／精霊使いリーゼ＝エレメント／傭兵ギルド ダグ＝戦闘／蒐集家オルカ＝深層）を定義し、各自 `TradeOffer` の交換メニューと `TraderTask` の依頼を持つ。タスク種別は `DeliverItems`（納品・受取時に素材消費）／`DefeatEnemies`（累計 or 種類指定の討伐数）／`ReachDepth`（最深到達）。`TaskRules.IsComplete/ProgressText` が純粋関数で判定。ダグ／リーゼはモンスター固有ドロップ（ゴブリンの牙 等）→結晶・欠片・エレメントの変換オファーを持ち、これがドロップ→研究素材の橋渡しになる（ダグに牙の納品タスク、オルカに各素材の蒐集タスク）。`TradeManager` は `DungeonManager.OnEnemyDefeated`・`OnWaveChanged` を購読して撃破数・最深深度を集計し、`SaveData`（達成タスクID／累計撃破数／敵種別ごとの撃破数／最深深度）へ永続化する。`TradePanel` は**トレーダーごとのタブ**（上部に横並び、各タブに顔アイコン用の `Image` 枠。アイコンは `TradePanel.traderIcons`＝`{traderId, Sprite}` 配列で割り当て。builder が traderId を先埋め）＋トレーダー内の「交換／依頼」**サブタブ**で切り替える。行はコード生成でテキスト折り返し・高さ可変（旧 `HideoutEntry` プレハブ／`BuildScrollPanel` は廃止）。

### インベントリパズル（杖のグリッド配置）
3つのスクリプトが協調して動く：

- [MagicGridManager.cs](Assets/Script/MagicGridManager.cs): グリッド（既定5×5）の状態を2次元配列で保持し、スクリーン/ワールド座標⇔グリッド座標の変換、配置可否判定（`CanPlace`）、はみ出し補正（`ClampToGrid`）を提供する。**座標系の注意**: グリッドはY上方向が正だが、`Docs/`の8章データは行が下方向に増加する座標系なので符号が逆になっている（`MagicDataGenerator.cs`の`Shape()`ヘルパーで変換済み）。
- [MagicSpawner.cs](Assets/Script/MagicSpawner.cs): 魔法一覧ボタン（`MagicGeneratorButton`）の生成と、クリックで生成されるピース（`MagicPieceUI`）のライフサイクル（配置/キャンセル/取り外し時のボタン復元）を仲介する。
- [MagicPieceUI.cs](Assets/Script/MagicPieceUI.cs): ドラッグ配置・クリック配置・Rキー回転（形状データを直接90度回転）・グリッド範囲内への自動補正を行う。ピース選択時は元の`MagicData`アセットを書き換えないよう`Instantiate`でコピーしてから使う。

3者はシーン内で `FindFirstObjectByType` により互いを参照するため、シーン上に `MagicGridManager` と `MagicSpawner` が単一ずつ存在する前提になっている。

### ステータス割り振りUI
[PlayerStatus.cs](Assets/Script/PlayerStatus.cs)（HP/Atk/Def/Spd/Luc＋マナ（`maxMana`/`currentMana`/`manaRegenPerSecond`）とポイント振り分けのデータ・ロジック）と [StatusUIManager.cs](Assets/Script/StatusUIManager.cs)（テキスト/バー/ボタンの表示更新）は `OnStatusChanged` イベントで疎結合になっている。マナの増減は `OnManaChanged` でも通知する。UIを増やす場合はこのイベント購読パターンを踏襲する。

### 汎用UI部品
[PanelSwitcher.cs](Assets/Script/PanelSwitcher.cs)（パネルの表示切り替え）、[ButtonHoverEffect.cs](Assets/Script/ButtonHoverEffect.cs)（ホバー時の枠線表示）は他のシステムと独立した単純なUIユーティリティ。
