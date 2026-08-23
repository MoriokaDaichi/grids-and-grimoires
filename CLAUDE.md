# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

「Grids & Grimoires」— インベントリ・パズルで杖を組み上げる魔術師のダンジョン攻略オートバトラー（Unity 2022.3.20f1 / URLシーンは `Assets/Scenes/SampleScene.unity`）。

企画・データの正本は [Docs/Grids_and_Grimoires_開発資料.md](Docs/Grids_and_Grimoires_開発資料.md)。魔法マスターデータの数値・グリッド形状（8章）・クラフトコスト（6章）など、仕様に迷ったら必ずこのドキュメントを参照する。設計変更や実装ギャップの経緯は auto memory（`game-design-grids-and-grimoires`）にも記録されているので、大きな仕様追加の前に確認するとよい。

## ビルド・実行

このプロジェクトにコマンドラインのビルド/テストスクリプトは無い。Unity Editor (2022.3.20f1) で `Assets/Scenes/SampleScene.unity` を開いて再生する。

### Editor拡張（`Assets/Editor/`）
マスターデータはコードを直接編集するのではなく、Unityメニューから生成する運用になっている。

- **Grimoire > Generate Master Magic Data**（[MagicDataGenerator.cs](Assets/Editor/MagicDataGenerator.cs)）: `Docs/`の4章・6章・8章のデータに基づき `Assets/MasicData/` 配下の `MagicData` アセットを一括生成・上書きする。魔法の数値やグリッド形状を変更する場合は、このジェネレーターの定義データを編集してから実行する。
- **Grimoire > Populate MagicSpawner List**（[MagicSpawnerPopulator.cs](Assets/Editor/MagicSpawnerPopulator.cs)）: 開いているシーンの `MagicSpawner.magicDataList` に `Assets/MasicData/` 内の全 `MagicData` を並び替えて再登録する。`SampleScene` を開いた状態で実行し、実行後はシーンを保存（Ctrl+S）する。

## アーキテクチャ

### 魔法データ（`MagicData` / `Assets/MasicData/`）
`MagicData`（[MagicData.cs](Assets/Script/MagicData.cs)）は `ScriptableObject` で、企画書の分類をそのまま反映した列挙型構造を持つ：`MagicCategory`（攻撃/状態異常/補助/バフActive/バフPassive）× `MagicAttribute`（炎/雷/風/光/闇）× `MagicRange`（単体/全体/なし）。加えて `StatusEffectType`、`BuffStat`、`shapeNodes`（パズル形状、中心を(0,0)とした相対座標）、`requiredMaterials`（解放コスト）を持つ。`damage`/`interval`/`effect` 系フィールドはデータとしては存在するが、オートバトル・状態異常・スキルツリー・トレードのロジックは未実装（UIとパズル配置のみ動作する）。

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
