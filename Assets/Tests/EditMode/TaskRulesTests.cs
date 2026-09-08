using System.Collections.Generic;
using NUnit.Framework;

// TaskRules（トレーダーのタスク達成判定、純粋関数）の検証。
public class TaskRulesTests
{
    private static MaterialCost Small(int n)
    {
        return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n };
    }

    [Test]
    public void DefeatEnemies_Generic_CompletesAtThreshold()
    {
        TraderTask t = new TraderTask { kind = TraderTaskKind.DefeatEnemies, targetCount = 10 };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { enemiesDefeated = 9 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { enemiesDefeated = 10 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { enemiesDefeated = 25 }));
    }

    [Test]
    public void DefeatEnemies_Named_UsesPerNameCounter()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.DefeatEnemies, targetEnemyName = "森の番人", targetCount = 3,
        };
        var kills = new Dictionary<string, int> { { "森の番人", 2 }, { "スライム", 99 } };
        TaskProgress p = new TaskProgress { enemyKills = n => kills.TryGetValue(n, out int v) ? v : 0 };
        Assert.IsFalse(TaskRules.IsComplete(t, p));

        kills["森の番人"] = 3;
        Assert.IsTrue(TaskRules.IsComplete(t, p));
    }

    [Test]
    public void ReachDepth_CompletesWhenBestDepthReaches()
    {
        TraderTask t = new TraderTask { kind = TraderTaskKind.ReachDepth, targetCount = 5 };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { bestDepth = 4 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { bestDepth = 5 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { bestDepth = 12 }));
    }

    [Test]
    public void DeliverItems_CompletesWhenInventoryHasAll()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.DeliverItems,
            deliverItems = new List<MaterialCost> { Small(15) },
        };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 14 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 15 }));
    }

    [Test]
    public void DeliverItems_MultiItem_RequiresEvery()
    {
        MaterialCost lightFrag = new MaterialCost { materialType = MaterialType.ElementFragment, attribute = MagicAttribute.Light, amount = 3 };
        MaterialCost darkFrag = new MaterialCost { materialType = MaterialType.ElementFragment, attribute = MagicAttribute.Dark, amount = 3 };
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.DeliverItems,
            deliverItems = new List<MaterialCost> { lightFrag, darkFrag },
        };

        // 光だけ足りる → 未達成
        TaskProgress partial = new TaskProgress
        {
            inventoryCount = c => c.attribute == MagicAttribute.Light ? 5 : 0,
        };
        Assert.IsFalse(TaskRules.IsComplete(t, partial));

        TaskProgress full = new TaskProgress { inventoryCount = c => 5 };
        Assert.IsTrue(TaskRules.IsComplete(t, full));
    }

    [Test]
    public void DeliverItems_WithDeliverMoney_RequiresBothItemsAndMoney()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.DeliverItems,
            deliverItems = new List<MaterialCost> { Small(10) },
            deliverMoney = 50,
        };

        // 素材は足りるがお金が足りない
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 10, money = 40 }));
        // 両方足りる
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 10, money = 50 }));
        // お金は足りるが素材が足りない
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 9, money = 999 }));
    }

    [Test]
    public void ProgressText_DeliverWithMoney_MentionsGold()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.DeliverItems,
            deliverItems = new List<MaterialCost> { Small(10) },
            deliverMoney = 30,
        };
        Assert.AreEqual("素材と 30G を集める", TaskRules.ProgressText(t, new TaskProgress { inventoryCount = c => 0, money = 0 }));
        Assert.AreEqual("納品可能", TaskRules.ProgressText(t, new TaskProgress { inventoryCount = c => 10, money = 30 }));
    }

    [Test]
    public void DeliverItems_EmptyList_NeverComplete()
    {
        TraderTask t = new TraderTask { kind = TraderTaskKind.DeliverItems, deliverItems = new List<MaterialCost>() };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { inventoryCount = c => 999 }));
    }

    [Test]
    public void IsComplete_NullTask_False()
    {
        Assert.IsFalse(TaskRules.IsComplete(null, new TaskProgress()));
    }

    [Test]
    public void CraftGear_CompletesWhenOwnedTierMeetsTarget()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.CraftGear, targetGearSlot = GearSlot.Wand, targetCount = 1,
        };
        // 杖なし → 未達成
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { ownedGearTier = s => 0 }));
        // 杖あり（tier1）→ 達成
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { ownedGearTier = s => s == GearSlot.Wand ? 1 : 0 }));
        // 他スロットの装備は関係ない
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { ownedGearTier = s => s == GearSlot.Armor ? 3 : 0 }));
    }

    [Test]
    public void CraftGear_RespectsMinimumTier()
    {
        TraderTask t = new TraderTask
        {
            kind = TraderTaskKind.CraftGear, targetGearSlot = GearSlot.Wand, targetCount = 2,
        };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress { ownedGearTier = s => 1 }));
        Assert.IsTrue(TaskRules.IsComplete(t, new TaskProgress { ownedGearTier = s => 2 }));
    }

    [Test]
    public void CraftGear_NullAccessor_False()
    {
        TraderTask t = new TraderTask { kind = TraderTaskKind.CraftGear, targetGearSlot = GearSlot.Wand, targetCount = 1 };
        Assert.IsFalse(TaskRules.IsComplete(t, new TaskProgress()));
    }

    [Test]
    public void ProgressText_CraftGear_ReflectsOwnership()
    {
        TraderTask t = new TraderTask { kind = TraderTaskKind.CraftGear, targetGearSlot = GearSlot.Wand, targetCount = 1 };
        Assert.AreEqual("作業台で製作する", TaskRules.ProgressText(t, new TaskProgress { ownedGearTier = s => 0 }));
        Assert.AreEqual("製作ずみ", TaskRules.ProgressText(t, new TaskProgress { ownedGearTier = s => 1 }));
    }

    [Test]
    public void ProgressText_ClampsToTarget()
    {
        TraderTask kill = new TraderTask { kind = TraderTaskKind.DefeatEnemies, targetCount = 10 };
        Assert.AreEqual("討伐 10 / 10", TaskRules.ProgressText(kill, new TaskProgress { enemiesDefeated = 40 }));

        TraderTask depth = new TraderTask { kind = TraderTaskKind.ReachDepth, targetCount = 5 };
        Assert.AreEqual("深度 3 / 5", TaskRules.ProgressText(depth, new TaskProgress { bestDepth = 3 }));
    }
}
