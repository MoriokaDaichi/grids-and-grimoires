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

純ロジックは `BattleFormula`（ダメージ式・Spd/Luc）/ `PassiveBonusCalculator`（パッシブ集計）/ `StatusEffectController`（状態異常タイマー）/ `MaterialLedger`（素材集計）/ `ResearchRules`（解放判定）/ `TradeCatalog`（交換メニュー）に切り出してあり、シーン非依存でテストできる。

### Editor拡張（`Assets/Editor/`）
マスターデータやシーンUIはコードを直接編集するのではなく、Unityメニューから生成する運用。

- **Grimoire > Generate Master Magic Data**（[MagicDataGenerator.cs](Assets/Editor/MagicDataGenerator.cs)）: `Docs/`の4章・6章・8章のデータに基づき `Assets/MasicData/` 配下の `MagicData` アセットを一括生成・上書きする。
- **Grimoire > Generate Enemy Data**（[EnemyDataGenerator.cs](Assets/Editor/EnemyDataGenerator.cs)）: `Assets/EnemyData/` の敵4体（スライム/ゴブリン/大ネズミ/森の番人）＋属性・耐性・暫定ドロップを生成。**数値は全て仮バランス**。
- **Grimoire > Generate Dungeon**（[DungeonGenerator.cs](Assets/Editor/DungeonGenerator.cs)）: 開いているシーンの `DungeonManager.waves` に敵グループ入りの5ウェーブ（仮）を設定。実行後シーン保存。
- **Grimoire > Populate MagicSpawner List**（[MagicSpawnerPopulator.cs](Assets/Editor/MagicSpawnerPopulator.cs)）: `MagicSpawner.magicDataList` を再登録する。実行後シーン保存（Ctrl+S）。
- **Grimoire > Build Battle UI**（[BattleUISceneBuilder.cs](Assets/Editor/BattleUISceneBuilder.cs)）: `SampleScene` に戦闘HUD/報酬/研究/トレードの画面ルート、`GamePhaseManager` / `PlayerInventory` / `ResearchManager` / `TradeManager` / `EnemyRoster` / `InventoryPanel` を構築し、出撃・研究・トレードボタンを配線する。旧・単体 `EnemyStatus` GameObject は除去する。再実行可能。実行後シーン保存。
- **Grimoire > Wipe Save**（[SaveMenu.cs](Assets/Editor/SaveMenu.cs)）: `Application.persistentDataPath/gg_save.json` を削除。

## アーキテクチャ

### 魔法データ（`MagicData` / `Assets/MasicData/`）
`MagicData`（[MagicData.cs](Assets/Script/MagicData.cs)）は `ScriptableObject` で、企画書の分類をそのまま反映した列挙型構造を持つ：`MagicCategory`（攻撃/状態異常/補助/バフActive/バフPassive）× `MagicAttribute`（炎/雷/風/光/闇）× `MagicRange`（単体/全体/なし）。加えて `StatusEffectType`、`BuffStat`、`shapeNodes`（パズル形状、中心を(0,0)とした相対座標）、`requiredMaterials`（解放コスト）を持つ。

### 実装済み / 未実装（2026-09 時点、ブランチ `feat/battle-ui-core-loop`）
- **動作**: 構築フェーズ（グリッド配置・回転・ドラッグ）、ステータス振り分けUI、**オートバトル**（攻撃/状態異常専用/補助/アクティブ・パッシブバフ、敵の反撃、状態異常5種、属性耐性、Spd=発動間隔短縮・Luc=会心）、**複数敵ウェーブ＋全体(AoE)魔法**（`EnemyRoster`）、**ダンジョン進行**（ウェーブ順送り・プレイヤーHP持ち越し）、**戦闘HUD**（代表敵HPバー・残り体数・ダメージ数字・状態異常アイコン・バフインジケータ・ウェーブ表示）、**報酬画面＋素材インベントリ＋JSON永続化**（`gg_save.json`）、**研究(Hideout)による魔法解放**（コスト0の魔法は初期解放、他は素材で解放）、**トレード画面**（結晶変換・欠片→エレメント・ステータスポイント購入）。
- **未実装 / 今後**: 戦闘HUDでのN体分の個別敵ウィジェット（現在は先頭生存個体＋残り体数のみ）、`PlayerStatus` のステータス振り分けのセーブ対象化、研究の本格的な分岐ツリー（現在はフラットな解放リスト）、Tarkov型ホームハブ（Character画面）、AoE以外の範囲パターン。
- **数値は全て仮**: 敵ステータス・属性耐性・ドロップ、状態異常の効果量/時間（`StatusEffectController` の定数）、バフ倍率（`BattleManager` の `PassiveStatPercentPerStage` 等）、Spd/Luc 係数（`BattleFormula`）、トレードレート（`TradeCatalog`）。企画書に記載が無く暫定。

### コアループの画面遷移（`GamePhaseManager`）
[GamePhaseManager.cs](Assets/Script/GamePhaseManager.cs) が `Build` / `Hideout`（研究）/ `Trade` / `Battle` / `Reward` を管理する（`buildPhaseObjects` と各画面ルートを `SetActive` で切替）。出撃ボタン→`StartSortie()`、研究ボタン→`GoToHideout()`、トレードボタン→`GoToTrade()`、各画面の戻る/帰還ボタン→`ReturnToBuild()`。`DungeonManager` の `OnDungeonCleared` / `OnDungeonFailed` を購読して報酬画面へ遷移する。`DungeonManager.Start()` での自動出撃は廃止済み（出撃は必ず `StartSortie()` 経由）。空杖で出撃した場合は `StartDungeon()` が `false` を返し構築画面に戻る。

### 永続化（`SaveManager` / `SaveData`）
単一JSON（`Application.persistentDataPath/gg_save.json`）。各システムは **`SaveManager.Load()` → 自分の領域だけ更新 → `SaveManager.Save()`** の順で他システムのフィールドを潰さないようにする（`PlayerInventory.Persist` / `ResearchManager.Save` 参照）。`SaveData` = 素材スタック（`MaterialLedger`）＋解放済み魔法ID。`Serialize`/`Deserialize` は純粋関数でテスト済み。

### オートバトル（`BattleManager` / `EnemyRoster` / `EnemyStatus` / `DungeonManager`）
- 敵は `EnemyRoster`（シーンシングルトン）がプール管理する複数体。`DungeonManager.waves`（`List<EnemyWave>`、無ければ `encounters` を1体ずつに変換）を順送りし、ウェーブ全滅で `roster.OnWaveDefeated` → 次ウェーブ。単体魔法は `roster.FirstAlive()`、全体(`MagicRange.AoE`)魔法は `roster.Living` 全員に命中。反撃は各 `EnemyStatus` が自分の `attackTimer` で独立に行う。
- **再入ガードが要**: ウェーブ全滅→次ウェーブへの切替が列挙中に連鎖するため、`BattleManager.casts` は `Clear()` せず `new List<>()` へ差し替え、`foreach` 前にリスト参照/`roster.Living` をスナップショット、`TakeDamage` 前に敵名/HPをローカルへ退避、AoE/反撃ループは `roster.Generation` の変化で打ち切る。`EnemyStatus` 側は自身の `battleGeneration` で tick適用ループを打ち切る。新しい処理を足すときはこの規律を崩さないこと。
- **戦闘UIの拡張点**: HUDやエフェクトはロジックに触らず、`public System.Action` イベントを購読して作る（[StatusUIManager](Assets/Script/StatusUIManager.cs) の `OnStatusChanged` 購読パターンの踏襲）。既存イベント: `PlayerStatus.OnDamaged` / `EnemyStatus.OnDamaged`・`OnStatusApplied`・`OnStatusExpired`・`OnStatusTick` / `EnemyRoster.OnRosterChanged`・`OnWaveDefeated` / `BattleManager.OnCastFired`・`OnAttackHit`・`OnBuffApplied`・`OnBuffExpired` / `DungeonManager.OnWaveChanged`・`OnDungeonCleared`・`OnDungeonFailed` / `PlayerInventory.OnInventoryChanged` / `ResearchManager.OnUnlocksChanged` / `TradeManager.OnTraded`。参考実装は [BattleHUD.cs](Assets/Script/UI/BattleHUD.cs) / [HideoutPanel.cs](Assets/Script/UI/HideoutPanel.cs)。
- **座標系の注意**: グリッドはY上方向が正だが、`Docs/`の8章データは行が下方向に増加する座標系なので符号が逆（`MagicDataGenerator.cs` の `Shape()` ヘルパーで変換済み）。

### インベントリパズル（杖のグリッド配置）
3つのスクリプトが協調して動く：

- [MagicGridManager.cs](Assets/Script/MagicGridManager.cs): グリッド（既定5×5）の状態を2次元配列で保持し、スクリーン/ワールド座標⇔グリッド座標の変換、配置可否判定（`CanPlace`）、はみ出し補正（`ClampToGrid`）を提供する。**座標系の注意**: グリッドはY上方向が正だが、`Docs/`の8章データは行が下方向に増加する座標系なので符号が逆になっている（`MagicDataGenerator.cs`の`Shape()`ヘルパーで変換済み）。
- [MagicSpawner.cs](Assets/Script/MagicSpawner.cs): 魔法一覧ボタン（`MagicGeneratorButton`）の生成と、クリックで生成されるピース（`MagicPieceUI`）のライフサイクル（配置/キャンセル/取り外し時のボタン復元）を仲介する。
- [MagicPieceUI.cs](Assets/Script/MagicPieceUI.cs): ドラッグ配置・クリック配置・Rキー回転（形状データを直接90度回転）・グリッド範囲内への自動補正を行う。ピース選択時は元の`MagicData`アセットを書き換えないよう`Instantiate`でコピーしてから使う。

3者はシーン内で `FindFirstObjectByType` により互いを参照するため、シーン上に `MagicGridManager` と `MagicSpawner` が単一ずつ存在する前提になっている。

### ステータス割り振りUI
[PlayerStatus.cs](Assets/Script/PlayerStatus.cs)（HP/Atk/Def/Spd/Lucとポイント振り分けのデータ・ロジック）と [StatusUIManager.cs](Assets/Script/StatusUIManager.cs)（テキスト/バー/ボタンの表示更新）は `OnStatusChanged` イベントで疎結合になっている。UIを増やす場合はこのイベント購読パターンを踏襲する。

### 汎用UI部品
[PanelSwitcher.cs](Assets/Script/PanelSwitcher.cs)（パネルの表示切り替え）、[ButtonHoverEffect.cs](Assets/Script/ButtonHoverEffect.cs)（ホバー時の枠線表示）は他のシステムと独立した単純なUIユーティリティ。
