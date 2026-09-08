using System.Collections.Generic;
using NUnit.Framework;

// TraderCatalog（複数トレーダーの定義）の構造検証。
public class TraderCatalogTests
{
    [Test]
    public void BuildTraders_ReturnsSeveralDistinctTraders()
    {
        List<Trader> traders = TraderCatalog.BuildTraders();
        Assert.GreaterOrEqual(traders.Count, 3);

        HashSet<string> ids = new HashSet<string>();
        foreach (Trader t in traders)
        {
            Assert.IsFalse(string.IsNullOrEmpty(t.id));
            Assert.IsFalse(string.IsNullOrEmpty(t.name));
            Assert.IsTrue(ids.Add(t.id), "trader id が重複: " + t.id);
        }
    }

    [Test]
    public void EverySpecialtyIsCovered()
    {
        List<Trader> traders = TraderCatalog.BuildTraders();
        HashSet<TraderSpecialty> specs = new HashSet<TraderSpecialty>();
        foreach (Trader t in traders) specs.Add(t.specialty);

        Assert.Contains(TraderSpecialty.Crystals, new List<TraderSpecialty>(specs));
        Assert.Contains(TraderSpecialty.Elements, new List<TraderSpecialty>(specs));
        Assert.Contains(TraderSpecialty.Combat, new List<TraderSpecialty>(specs));
        Assert.Contains(TraderSpecialty.Collector, new List<TraderSpecialty>(specs));
    }

    [Test]
    public void EveryTraderOffersSomethingOrHasTasks()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
            Assert.IsTrue(t.offers.Count > 0 || t.tasks.Count > 0, t.name + " が空");
    }

    [Test]
    public void TaskIdsAreGloballyUnique_AndKindsWellFormed()
    {
        HashSet<string> taskIds = new HashSet<string>();
        foreach (Trader t in TraderCatalog.BuildTraders())
        {
            foreach (TraderTask task in t.tasks)
            {
                Assert.IsFalse(string.IsNullOrEmpty(task.id));
                Assert.IsTrue(taskIds.Add(task.id), "task id が重複: " + task.id);
                Assert.AreEqual(t.id, task.traderId);

                if (task.kind == TraderTaskKind.DeliverItems)
                    Assert.Greater(task.deliverItems.Count, 0, task.id + " に納品物が無い");
                else
                    Assert.Greater(task.targetCount, 0, task.id + " の目標値が0");

                bool hasReward = task.rewardItems.Count > 0 || task.rewardStatPoints > 0 || task.rewardMoney > 0
                    || !string.IsNullOrEmpty(task.rewardGearId) || !string.IsNullOrEmpty(task.rewardRecipeId);
                Assert.IsTrue(hasReward, task.id + " に報酬が無い");

                // 納金は納品タスクだけに設定する（他種別では判定されない）
                if (task.kind != TraderTaskKind.DeliverItems)
                    Assert.AreEqual(0, task.deliverMoney, task.id + " は納品タスクでないのに deliverMoney がある");
            }
        }
    }

    [Test]
    public void CombatTrader_HasNamedKillTask()
    {
        bool found = false;
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
                if (task.kind == TraderTaskKind.DefeatEnemies && !string.IsNullOrEmpty(task.targetEnemyName))
                    found = true;
        Assert.IsTrue(found, "指定モンスター討伐タスクが1つも無い");
    }

    [Test]
    public void CollectorTrader_HasReachDepthTask()
    {
        bool found = false;
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
                if (task.kind == TraderTaskKind.ReachDepth) found = true;
        Assert.IsTrue(found, "深度到達タスクが1つも無い");
    }

    [Test]
    public void EverySpecialItemReferenced_IsKnownMonsterPart()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
        {
            foreach (TradeOffer o in t.offers)
            {
                foreach (MaterialCost c in o.give) AssertKnownPart(c, t.id + " offer give");
                foreach (MaterialCost c in o.receive) AssertKnownPart(c, t.id + " offer receive");
            }
            foreach (TraderTask task in t.tasks)
            {
                foreach (MaterialCost c in task.deliverItems) AssertKnownPart(c, task.id + " deliver");
                foreach (MaterialCost c in task.rewardItems) AssertKnownPart(c, task.id + " reward");
            }
        }
    }

    [Test]
    public void EveryNamedKillTask_TargetsAKnownEnemyOrIsGeneric()
    {
        // 敵名は EnemyDataGenerator の enemyName と一致している必要がある。ここでは
        // 代表的な名前が生きていることだけ担保する（アセット非依存で回せる範囲）。
        var known = new HashSet<string> { "森の番人", "スケルトン", "オーガ", "ドラゴン" };
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
                if (task.kind == TraderTaskKind.DefeatEnemies && !string.IsNullOrEmpty(task.targetEnemyName))
                    Assert.IsTrue(known.Contains(task.targetEnemyName), "未知の討伐対象: " + task.targetEnemyName);
    }

    [Test]
    public void TaskLines_RequiresPointsBackwardWithinSameTrader()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
        {
            HashSet<string> seen = new HashSet<string>();
            bool firstSeen = false;
            foreach (TraderTask task in t.tasks)
            {
                if (string.IsNullOrEmpty(task.requires))
                {
                    Assert.IsFalse(firstSeen, t.id + " に連鎖の先頭（requires 空）が複数ある: " + task.id);
                    firstSeen = true;
                }
                else
                {
                    Assert.IsTrue(seen.Contains(task.requires),
                        task.id + " の requires「" + task.requires + "」が同一トレーダーのより前のタスクを指していない");
                }
                seen.Add(task.id);
            }
            Assert.IsTrue(t.tasks.Count == 0 || firstSeen, t.id + " の連鎖に先頭が無い");
        }
    }

    [Test]
    public void TaskGearAndRecipeRewards_ResolveInGearCatalog()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
        {
            foreach (TraderTask task in t.tasks)
            {
                if (!string.IsNullOrEmpty(task.rewardGearId))
                    Assert.IsNotNull(GearCatalog.Get(task.rewardGearId),
                        task.id + " の rewardGearId「" + task.rewardGearId + "」が GearCatalog に無い");
                if (!string.IsNullOrEmpty(task.rewardRecipeId))
                {
                    GearDef g = GearCatalog.Get(task.rewardRecipeId);
                    Assert.IsNotNull(g, task.id + " の rewardRecipeId「" + task.rewardRecipeId + "」が GearCatalog に無い");
                    Assert.IsTrue(g.recipeGated, task.id + " の rewardRecipeId「" + task.rewardRecipeId + "」は recipeGated でない");
                }
            }
        }
    }

    [Test]
    public void EveryTrader_HasAMoneySellOffer()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
        {
            bool hasSell = false;
            foreach (TradeOffer o in t.offers)
                if (o.gainMoney > 0) hasSell = true;
            Assert.IsTrue(hasSell, t.name + " に売却（→お金）オファーが無い");
        }
    }

    [Test]
    public void MoneyOffers_HaveNonNegativeAmounts()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TradeOffer o in t.offers)
            {
                Assert.GreaterOrEqual(o.giveMoney, 0, t.id + " の giveMoney が負");
                Assert.GreaterOrEqual(o.gainMoney, 0, t.id + " の gainMoney が負");
            }
    }

    private static void AssertKnownPart(MaterialCost c, string where)
    {
        if (c == null || c.materialType != MaterialType.SpecialItem) return;
        Assert.IsTrue(MonsterPartCatalog.IsKnown(c.specialItemName),
            where + " の固有アイテム「" + c.specialItemName + "」が MonsterPartCatalog に無い");
    }
}
