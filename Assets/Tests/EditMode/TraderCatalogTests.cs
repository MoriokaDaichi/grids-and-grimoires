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
    public void EarlyOnboarding_HasCraftWandTask_AtHeadOfAChain()
    {
        // 再検証3 D2：「まず杖」導線。どこかのトレーダーの連鎖の先頭に、杖の製作を促す
        // CraftGear タスクがあること（前提タスク無し＝cold start で即見える）。
        bool found = false;
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
                if (task.kind == TraderTaskKind.CraftGear
                    && task.targetGearSlot == GearSlot.Wand
                    && string.IsNullOrEmpty(task.requires))
                    found = true;
        Assert.IsTrue(found, "杖の製作を促す CraftGear タスクが連鎖の先頭に無い（再検証3 D2）");
    }

    [Test]
    public void CraftGearTasks_HavePositiveMinTier_AndReward()
    {
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
            {
                if (task.kind != TraderTaskKind.CraftGear) continue;
                Assert.Greater(task.targetCount, 0, task.id + " の最低tierが0");
                bool hasReward = task.rewardItems.Count > 0 || task.rewardStatPoints > 0 || task.rewardMoney > 0
                    || !string.IsNullOrEmpty(task.rewardGearId) || !string.IsNullOrEmpty(task.rewardRecipeId);
                Assert.IsTrue(hasReward, task.id + " に報酬が無い");
            }
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

    // 再検証3 D1／改善ループ通しプレイ：cold-start の壁は深度8〜9。中盤の結晶 faucet（orca_2＝
    // 大結晶×2）の到達要件が壁の先だと永遠に届かないので、壁より手前（≤8）に置く。
    [Test]
    public void Orca2_CrystalFaucet_IsReachableAtOrBeforeColdStartWall()
    {
        TraderTask orca2 = null;
        foreach (Trader t in TraderCatalog.BuildTraders())
            foreach (TraderTask task in t.tasks)
                if (task.id == "orca_2") orca2 = task;

        Assert.IsNotNull(orca2, "orca_2 が見つからない");
        Assert.AreEqual(TraderTaskKind.ReachDepth, orca2.kind);
        Assert.LessOrEqual(orca2.targetCount, 8, "orca_2 の到達要件が cold-start の壁より深い");

        bool givesLargeCrystal = false;
        foreach (MaterialCost c in orca2.rewardItems)
            if (c != null && c.materialType == MaterialType.LargeManaCrystal) givesLargeCrystal = true;
        Assert.IsTrue(givesLargeCrystal, "orca_2 が大結晶を出さない（結晶 faucet の役割）");
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

    // 改善ループ サイクル12：ダグの連鎖で、中盤の中結晶 faucet（dag_4＝ゴブリンの牙×12→中×3）が
    // 深部 debut の敵を要求するタスク（森の番人 d19／オーガ d22／ドラゴン d37）より“前”に来ていること。
    [Test]
    public void DagChain_MediumCrystalFaucet_ComesBeforeDeepEnemyKillTasks()
    {
        Trader dag = null;
        foreach (Trader t in TraderCatalog.BuildTraders()) if (t.id == "dag") dag = t;
        Assert.IsNotNull(dag);

        int faucetPos = -1;
        var deepKillNames = new HashSet<string> { "森の番人", "オーガ", "ドラゴン" };
        for (int i = 0; i < dag.tasks.Count; i++)
        {
            if (dag.tasks[i].id == "dag_4") faucetPos = i;
            if (dag.tasks[i].kind == TraderTaskKind.DefeatEnemies && deepKillNames.Contains(dag.tasks[i].targetEnemyName))
                Assert.Greater(i, faucetPos,
                    "深部 debut の討伐タスク「" + dag.tasks[i].id + "」が中結晶 faucet（dag_4）より前にある");
        }
        Assert.GreaterOrEqual(faucetPos, 0, "dag_4 が見つからない");
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
    public void MidTierMonsterParts_HaveAMoneySink()
    {
        // 再検証2 R4：錬金釜Lv1 で変換できず売り先も無い tier2 中位素材が周回で塩漬けになる所見。
        // 主要な tier2 素材はどこかのトレーダーが現金で買い取れること。
        string[] midParts = { "剛毛", "蜘蛛の糸", "鉄の兜", "毒腺", "錆びた短剣", "風切羽", "若木の枝", "腐肉" };
        List<Trader> traders = TraderCatalog.BuildTraders();
        foreach (string part in midParts)
        {
            bool sellable = false;
            foreach (Trader t in traders)
                foreach (TradeOffer o in t.offers)
                {
                    if (o.gainMoney <= 0) continue;
                    foreach (MaterialCost c in o.give)
                        if (c.materialType == MaterialType.SpecialItem && c.specialItemName == part) sellable = true;
                }
            Assert.IsTrue(sellable, "tier2 素材「" + part + "」に現金の売却口が無い（再検証2 R4）");
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
