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

                Assert.IsTrue(task.rewardItems.Count > 0 || task.rewardStatPoints > 0, task.id + " に報酬が無い");
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
}
