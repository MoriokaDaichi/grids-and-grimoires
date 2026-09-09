using System;
using System.Collections.Generic;

// JSONセーブの1レコード。JsonUtility は Dictionary を扱えないためリストで持つ。
[Serializable]
public class MaterialStack
{
    public int materialType;      // (int)MaterialType
    public int attribute;         // (int)MagicAttribute（欠片/エレメントのみ意味を持つ）
    public string specialItemName; // 固有アイテムのみ
    public int count;
}

// JsonUtility は Dictionary を扱えないため、文字列→整数のペアをリストで持つ。
[Serializable]
public class StringIntPair
{
    public string key;
    public int value;
}

// マジックサークルで進行中の「捧げもの」1件。
[Serializable]
public class BrewRecord
{
    public double startUnixSeconds; // 開始時刻（UTC, Unix秒）
    public float hours;             // 完成までの時間
    public int inputRarity;         // (int)ItemRarity（捧げたアイテムのレア度）
    public string inputLabel;       // 表示用（捧げたアイテム名）
}

// セーブデータ全体。各システムは「読み込み→自分の領域だけ更新→書き込み」で他システムのフィールドを保つこと。
[Serializable]
public class SaveData
{
    public List<MaterialStack> materials = new List<MaterialStack>();

    // 研究で解放済みの魔法ID（= MagicData のアセット名）。コスト0の魔法は常に解放扱いなので含めない。
    // allocatedResearchNodes の魔法サブセットと同期。他システムの互換用に残す。
    public List<string> unlockedMagicIds = new List<string>();

    // 研究スキルツリーで割り当て済みのノードID（魔法ノード＋"node_..."のステータスノード）。
    public List<string> allocatedResearchNodes = new List<string>();

    // トレーダーのタスク進捗
    public List<string> completedTaskIds = new List<string>();
    public int lifetimeEnemyKills;
    public int bestDungeonDepth;
    public List<StringIntPair> enemyKillCounts = new List<StringIntPair>();
    // 繰り返しタスク（repeatable）の「前回受取時の累計撃破数」。key = taskId。
    public List<StringIntPair> repeatableTaskBaselines = new List<StringIntPair>();
    // 繰り返しタスクを何回受け取ったか（表示用・報酬スケール用）。key = taskId。
    public List<StringIntPair> repeatableTaskClaims = new List<StringIntPair>();

    // ハイドアウト：設備レベル（key = (FacilityKind).ToString(), value = 0..3）
    public List<StringIntPair> facilityLevels = new List<StringIntPair>();
    // 魔力炉に溜まっている燃料（魔力結晶の価値換算）
    public long furnaceFuel;
    // マジックサークルで進行中の捧げもの
    public List<BrewRecord> magicCircleBrews = new List<BrewRecord>();
    // 作業台で製作済み（＝所有）の装備ID。所有していても装備中とは限らない。
    public List<string> craftedGearIds = new List<string>();
    // いま装備しているの装備ID（スロットごとに最大1個。空なら未記載）。
    // PlayerStatus への恒久ボーナスは「装備中」のぶんだけ。空/旧セーブは craftedGearIds から移行（1スロット1個だったため全て装備）。
    public List<string> equippedGearIds = new List<string>();
    // 作業台で解禁済みの装備レシピID（recipeGated な GearDef はこれが無いと製作できない）
    public List<string> unlockedGearRecipes = new List<string>();
    // 2つ目のアクセサリー装備枠を開放済みか（トレーダーのタスク報酬で開放）
    public bool accessorySlot2Unlocked;

    // お金（ゴールド）。トレード・タスク納金・ダンジョン入場料で使う。
    public int money;
    // 初回起動で開始所持金を1度だけ付与するためのフラグ
    public bool moneyInitialized;

    // プレイヤーの手動ステータス振り分け（StatusUIの＋ボタンで振ったぶん＋タスク報酬で増えたステP）。
    // 研究の小ノード／製作装備ぶんは ResearchManager / HideoutManager が起動時に別途再適用するので、
    // ここには含めない（含めると二重加算になる）。PlayerStatus が担当。
    public bool playerStatsSaved;   // 一度でも手動振り分け or ステP増減を保存したか
    public int savedStatsPoint;     // 残りステータスポイント
    public int manualStatHp;        // AddStat("HP") で加算した累計量
    public int manualStatAtk;
    public int manualStatDef;
    public int manualStatSpd;
    public int manualStatLuc;
}
