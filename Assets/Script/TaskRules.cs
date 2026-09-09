using System;
using System.Collections.Generic;

// トレーダーのタスク種別。
public enum TraderTaskKind
{
    DeliverItems,   // 指定素材の納品
    DefeatEnemies,  // 敵の討伐（種類指定または累計）
    ReachDepth,     // 指定深度への到達
    CraftGear,      // 指定スロットの装備を製作・所持する（targetGearSlot / targetCount=最低tier）
}

// トレーダーが出す1件のタスク。TradeOffer と同じくプレーンクラス（SO化はしない）。
// requires を辿ってタスクライン（前提クリアで次が出現する連鎖）を成す。
public class TraderTask
{
    public string id;                                        // 一意ID（セーブの達成記録キー）
    public string traderId;
    public string title;
    public string description;
    public string requires;                                  // 前提タスクID（空なら連鎖の先頭）。完了済みのときだけ解放。
    public TraderTaskKind kind;
    public List<MaterialCost> deliverItems = new List<MaterialCost>(); // DeliverItems: 納品物（達成時に消費）
    public int deliverMoney;                                  // DeliverItems: 併せて納めるお金（受取時に消費）
    public string targetEnemyName;                            // DefeatEnemies: 対象の敵名（空なら種類問わず）
    public int targetCount;                                   // DefeatEnemies: 討伐数 / ReachDepth: 深度 / CraftGear: 最低tier
    public GearSlot targetGearSlot;                           // CraftGear: 対象の装備スロット
    public List<MaterialCost> rewardItems = new List<MaterialCost>();
    public int rewardStatPoints;
    public int rewardMoney;                                   // 報酬のお金
    public string rewardGearId;                               // 報酬で完成品を直接付与する装備ID（GearCatalog）
    public string rewardRecipeId;                             // 報酬で作業台レシピを解禁する装備ID（GearCatalog）
    public bool rewardUnlocksAccessorySlot;                   // 報酬で2つ目のアクセサリー装備枠を開放する（HideoutManager）

    // 繰り返し受注できるタスク（DefeatEnemies・種類指定なしのみ対応）。達成しても completedTaskIds には
    // 入らず、受取ごとに「前回受取時からの累計撃破数」を基準に進捗をリセットする。無限に資源を稼げる導線。
    public bool repeatable;
}

// タスク進捗の判定に必要な値（純粋関数に渡す）。
public struct TaskProgress
{
    public int enemiesDefeated;          // 累計撃破数
    public int bestDepth;               // 到達最深
    public int money;                   // 現在の所持金（納金タスクの判定用）
    public Func<string, int> enemyKills; // 敵名 → 累計撃破数
    public Func<MaterialCost, int> inventoryCount; // 素材 → 所持数
    public Func<GearSlot, int> ownedGearTier; // 装備スロット → 所持中の最上位 tier（無ければ0。CraftGear の判定用）
}

// タスクの達成判定と進捗テキスト（純粋関数）。シーン非依存で EditMode テストできる。
public static class TaskRules
{
    // 現在の進捗でタスクの達成条件を満たしているか。
    public static bool IsComplete(TraderTask task, TaskProgress p)
    {
        if (task == null) return false;

        switch (task.kind)
        {
            case TraderTaskKind.DefeatEnemies:
                if (!string.IsNullOrEmpty(task.targetEnemyName))
                    return p.enemyKills != null && p.enemyKills(task.targetEnemyName) >= task.targetCount;
                return p.enemiesDefeated >= task.targetCount;

            case TraderTaskKind.ReachDepth:
                return p.bestDepth >= task.targetCount;

            case TraderTaskKind.CraftGear:
                return p.ownedGearTier != null && p.ownedGearTier(task.targetGearSlot) >= Math.Max(1, task.targetCount);

            case TraderTaskKind.DeliverItems:
                if (task.deliverItems == null || task.deliverItems.Count == 0) return false;
                if (p.inventoryCount == null) return false;
                foreach (MaterialCost c in task.deliverItems)
                    if (c != null && p.inventoryCount(c) < c.amount) return false;
                if (task.deliverMoney > 0 && p.money < task.deliverMoney) return false;
                return true;

            default:
                return false;
        }
    }

    // 一覧表示用の進捗テキスト（例: "討伐 7 / 10"、"深度 3 / 5"、"納品待ち"）。
    public static string ProgressText(TraderTask task, TaskProgress p)
    {
        if (task == null) return "";

        switch (task.kind)
        {
            case TraderTaskKind.DefeatEnemies:
            {
                int cur = !string.IsNullOrEmpty(task.targetEnemyName)
                    ? (p.enemyKills != null ? p.enemyKills(task.targetEnemyName) : 0)
                    : p.enemiesDefeated;
                string who = string.IsNullOrEmpty(task.targetEnemyName) ? "討伐" : task.targetEnemyName + " 討伐";
                return who + " " + Math.Min(cur, task.targetCount) + " / " + task.targetCount;
            }
            case TraderTaskKind.ReachDepth:
                return "深度 " + Math.Min(p.bestDepth, task.targetCount) + " / " + task.targetCount;

            case TraderTaskKind.CraftGear:
                return IsComplete(task, p) ? "製作ずみ" : "作業台で製作する";

            case TraderTaskKind.DeliverItems:
            {
                if (IsComplete(task, p)) return "納品可能";
                return task.deliverMoney > 0 ? "素材と " + task.deliverMoney + "G を集める" : "素材を集める";
            }

            default:
                return "";
        }
    }
}
